// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModbusMonitor.Models;
using ModbusMonitor.Services;
using ModbusMonitor.Infrastructure;

namespace ModbusMonitor.Forms
{
    public partial class MonitorForm : Form
    {
        // 整套设置只在停止监控且通信空闲时替换。
        // 保存设置时替换整个对象，通信过程中不修改这份设置。
        private AppSettings settings = new AppSettings();
        private readonly Button settingsButton = new Button();
        private readonly Label explanationLabel = new Label();

        // ---------- 界面控件 ----------

        private readonly ComboBox modeBox = new ComboBox();
        private readonly Label connectionLabel = new Label();

        private readonly Button startButton = new Button();
        private readonly Button stopButton = new Button();
        private readonly Button countButton = new Button();

        private readonly Label temperatureLabel = new Label();
        private readonly Label statusLabel = new Label();
        private readonly Label sampleCountLabel = new Label();
        private readonly Label clickCountLabel = new Label();

        private readonly CheckBox timeoutCheckBox = new CheckBox();
        private readonly ListBox logList = new ListBox();
        private readonly TabControl displayTabs = new TabControl();
        // 使用全限定名称，避免ScottPlot.Color与System.Drawing.Color冲突。
        private readonly ScottPlot.WinForms.FormsPlot temperatureChart =
            new ScottPlot.WinForms.FormsPlot();
        private readonly CheckBox autoScaleCheckBox = new CheckBox();
        private bool hasChartData = false;

        // 学习版使用简单的本地文本追加，每次写入量很小。
        // 不使用后台队列；磁盘慢时写文件可能短暂影响UI。
        private readonly FileRecorder recorder = new FileRecorder();
        private readonly Label storageLabel = new Label();
        private readonly Button openFolderButton = new Button();
        private readonly CheckBox saveFailureCheckBox = new CheckBox();
        private long shownStorageFailures = 0;
        private readonly Label cacheLabel = new Label();

        // 只保存最近100条有效采样。失败不会添加0℃或重复上一温度。
        private readonly List<TemperatureSample> samples = new List<TemperatureSample>();
        private readonly Stopwatch sampleClock = Stopwatch.StartNew();
        private bool breakBeforeNextSample = true;

        private readonly NumericUpDown targetInput = new NumericUpDown();
        private readonly Button writeButton = new Button();

        private readonly System.Windows.Forms.Timer timer =
            new System.Windows.Forms.Timer();

        // ---------- 窗体状态 ----------

        // 原来的isReading改名为isBusy：
        // 它不仅表示读取，也表示写入和读回验证正在进行。
        // 这个变量只在UI线程中访问。
        private bool isBusy = false;

        private bool isMonitoring = false;
        private bool isClosing = false;

        private int sampleCount = 0;
        private int clickCount = 0;

        // 只在UI线程中检查，用于避免关闭后更新控件。
        private bool CanUpdateUi =>
            !isClosing && !IsDisposed && !Disposing;

        public MonitorForm()
        {
            // 布局代码在同一个partial类的另一个文件中。
            InitializeWindow();
        }

        // ---------- 通信方式与界面状态 ----------

        // 这个方法读取控件，只允许在UI线程调用。
        private CommunicationMode GetSelectedMode()
        {
            return modeBox.SelectedIndex == 0
                ? CommunicationMode.Rtu
                : CommunicationMode.Tcp;
        }

        private void UpdateConnectionLabel()
        {
            string endpoint = GetSelectedMode() == CommunicationMode.Rtu
                ? $"{settings.SerialPortName} / {settings.BaudRate} / {settings.Parity} / {settings.DataBits} / {settings.StopBits}"
                : $"{settings.TcpHost}:{settings.TcpPort}";
            connectionLabel.Text = endpoint +
                $"\n单元{settings.UnitId}；输入地址{settings.InputRegisterAddress}；保持地址{settings.HoldingRegisterAddress}";
        }

        private void ModeBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateConnectionLabel();

            // 两种连接可能对应两台设备，切换时不能混用历史数据。
            samples.Clear();
            sampleClock.Restart();
            breakBeforeNextSample = true;
            cacheLabel.Text = "曲线缓存：0 / 100条";
            hasChartData = false;
            UpdateChart();

            // 切换设备后，不继续显示上一设备的温度。
            temperatureLabel.Text = "最近温度：-- ℃";
            sampleCount = 0;
            sampleCountLabel.Text = "有效采样次数：0";

            statusLabel.Text = "状态：通信方式已切换，尚未开始";
            statusLabel.ForeColor = Color.DimGray;

            AddLog($"已切换到{modeBox.SelectedItem}。");
        }

        // 在一个地方统一决定按钮能不能使用，
        // 避免各个事件分别恢复按钮时漏掉某个状态。
        private void UpdateControls()
        {
            if (!CanUpdateUi)
                return;

            // 监控中或某次通信未完成，都不允许切换。
            modeBox.Enabled = !isMonitoring && !isBusy;
            settingsButton.Enabled = !isMonitoring && !isBusy;

            startButton.Enabled = !isMonitoring && !isBusy;
            stopButton.Enabled = isMonitoring;

            writeButton.Enabled = !isBusy;
            targetInput.Enabled = !isBusy;
        }

        // ---------- 参数设置 ----------

        private void UpdateSamplingDescription()
        {
            explanationLabel.Text =
                $"采集间隔{settings.SampleIntervalMs}ms；报警≥{settings.AlarmTemperature:F1}℃；忙时跳过新请求。";
        }

        private void SettingsButton_Click(object? sender, EventArgs e)
        {
            if (isClosing || isMonitoring || isBusy)
                return;

            // 对话框只读取旧值填充控件，不直接修改settings。
            // 它内部先验证并保存成功，才返回OK和独立的新配置对象。
            using SettingsForm dialog = new SettingsForm(settings);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SavedSettings == null)
                return;

            settings = dialog.SavedSettings;
            timer.Interval = settings.SampleIntervalMs;
            UpdateConnectionLabel();
            UpdateSamplingDescription();

            // 参数可能切换到其他设备或数据地址，应用后开始一段新曲线。
            // 仅清空界面缓存，不删除磁盘上的CSV/TXT历史。
            samples.Clear();
            sampleClock.Restart();
            sampleCount = 0;
            hasChartData = false;
            breakBeforeNextSample = true;
            sampleCountLabel.Text = "有效采样次数：0";
            cacheLabel.Text = "曲线缓存：0 / 100条";
            temperatureLabel.Text = "最近温度：-- ℃";
            UpdateChart();
            statusLabel.Text = "状态：设置已保存并应用，监控保持停止";
            statusLabel.ForeColor = Color.ForestGreen;
            AddLog($"设置已保存并应用：间隔{settings.SampleIntervalMs}ms，报警阈值{settings.AlarmTemperature:F1}℃；未向设备写入目标温度。");
        }

        // ---------- 开始 / 停止监控 ----------

        private void StartButton_Click(object? sender, EventArgs e)
        {
            if (isClosing || isMonitoring || isBusy)
                return;

            isMonitoring = true;
            // 停止再开始时保留历史，但不跨越停止期间连线。
            breakBeforeNextSample = true;
            UpdateControls();

            statusLabel.Text = "状态：监控已启动，等待下一次采集";
            statusLabel.ForeColor = Color.DimGray;

            timer.Start();
            AddLog($"开始监控：{modeBox.SelectedItem}。");
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            timer.Stop();
            isMonitoring = false;

            UpdateControls();

            statusLabel.Text = isBusy
                ? "状态：已停止监控，等待当前通信结束"
                : "状态：已停止监控";

            statusLabel.ForeColor = Color.DimGray;

            // 当前通信可能是读取，也可能是写入。
            // 停止监控不会撤销已经发出的写入请求。
            AddLog(isBusy
                ? "停止自动采集；当前通信继续完成，采集结果不再显示，写入不会撤销。"
                : "停止监控。");
        }

        private void CountButton_Click(object? sender, EventArgs e)
        {
            clickCount++;
            clickCountLabel.Text = $"计数按钮点击次数：{clickCount}";
        }

        // ---------- 定时采集 ----------

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (isClosing || !isMonitoring || isBusy)
                return;

            // 在await之前设为忙碌，避免下一次Tick重复启动。
            isBusy = true;

            // 在UI线程取出本次使用的设置。
            // 后台只使用这些普通变量，不访问任何控件。
            CommunicationMode mode = GetSelectedMode();
            AppSettings currentSettings = settings; // 本轮通信使用同一份设置。
            bool simulateTimeout = timeoutCheckBox.Checked;

            UpdateControls();

            statusLabel.Text = "状态：正在读取，界面仍可操作";
            statusLabel.ForeColor = Color.DimGray;
            AddLog("开始一次后台读取。");

            try
            {
                double temperature = await Task.Run(
                    () => ModbusService.ReadTemperature(mode, currentSettings, simulateTimeout));

                if (!CanUpdateUi)
                    return;

                if (!isMonitoring)
                {
                    AddLog("读取已结束，但监控已停止，本次结果不显示。");
                    return;
                }

                sampleCount++;
                sampleCountLabel.Text = $"有效采样次数：{sampleCount}";

                // 只有读取成功、窗口存在、监控仍在进行，才记录采样。
                SaveSample(temperature, mode);
                ShowTemperature(temperature);
                AddLog($"读取成功：{temperature:F1} ℃，曲线缓存：{samples.Count}条。");
            }
            catch (Exception ex)
            {
                if (!CanUpdateUi)
                    return;

                if (isMonitoring)
                {
                    statusLabel.Text = $"状态：读取失败，{ex.Message}";
                    statusLabel.ForeColor = Color.Red;
                }

                // 下一次成功采样不与失败前的点相连。
                breakBeforeNextSample = true;

                // TCP读取超时不一定是TimeoutException，
                // 因此统一记录实际异常信息。
                AddLog($"读取失败：{ex.Message}");
            }
            finally
            {
                isBusy = false;

                if (CanUpdateUi)
                {
                    UpdateControls();

                    if (!isMonitoring)
                    {
                        statusLabel.Text = "状态：已停止监控";
                        statusLabel.ForeColor = Color.DimGray;
                    }
                }
            }
        }

        // ---------- 写入目标温度 ----------

        private async void WriteButton_Click(object? sender, EventArgs e)
        {
            if (isClosing || isBusy)
                return;

            // 在UI线程取出设置和输入值。
            CommunicationMode mode = GetSelectedMode();
            AppSettings currentSettings = settings; // 本轮通信使用同一份设置。
            decimal requestedTemperature = targetInput.Value;
            ushort rawValue = (ushort)(requestedTemperature * 10m);

            isBusy = true;
            UpdateControls();

            statusLabel.Text = "状态：正在写入并读回目标温度";
            statusLabel.ForeColor = Color.DimGray;

            AddLog($"准备写入目标温度：{requestedTemperature:F1} ℃。");

            try
            {
                double actualTarget = await Task.Run(
                    () => ModbusService.WriteAndReadTarget(mode, currentSettings, rawValue));

                if (!CanUpdateUi)
                    return;

                statusLabel.Text =
                    $"状态：目标温度已验证为{actualTarget:F1} ℃";
                statusLabel.ForeColor = Color.ForestGreen;

                AddLog(
                    $"写入并读回验证成功：目标温度{actualTarget:F1} ℃。");
            }
            catch (Exception ex)
            {
                if (!CanUpdateUi)
                    return;

                statusLabel.Text = "状态：写入或读回失败，设备目标值尚未确认";
                statusLabel.ForeColor = Color.Red;

                AddLog(
                    $"写入或读回验证失败：{ex.Message}；设备最终值尚未确认。");
            }
            finally
            {
                isBusy = false;

                if (CanUpdateUi)
                {
                    UpdateControls();

                    // 保留写入成功或失败的提示，
                    // 不在这里用“已停止监控”覆盖操作结果。
                }
            }
        }

        // ---------- 保存有效采样 ----------

        private void SaveSample(double temperature, CommunicationMode mode)
        {
            // 本方法在Timer_Tick的await之后、UI线程上执行。
            // 缓存操作和控件绘图都在UI线程，不需要跨线程修改集合。
            double elapsed = sampleClock.Elapsed.TotalSeconds;
            bool hasLongGap = samples.Count > 0 &&
                elapsed - samples[samples.Count - 1].ElapsedSeconds >
                settings.SampleIntervalMs / 1000.0 * 2.5;

            TemperatureSample sample = new TemperatureSample
            {
                Time = DateTime.Now,
                Temperature = temperature,
                ElapsedSeconds = elapsed,
                // 超过配置采集间隔的2.5倍视为长间隔；显式失败/停止也会断线。
                BreakBefore = breakBeforeNextSample || hasLongGap
            };
            samples.Add(sample);

            // 曲线和CSV共用同一条记录的时间，不在后台重新取时间。
            // 保存方法内部处理文件错误，采集成功不等于保存成功。
            recorder.SaveSample(sample.Time,
                mode == CommunicationMode.Rtu ? "RTU" : "TCP",
                sample.Temperature, saveFailureCheckBox.Checked);

            if (samples.Count > 100)
                samples.RemoveAt(0);

            breakBeforeNextSample = false;
            cacheLabel.Text = $"曲线缓存：{samples.Count} / 100条";

            // 用缓存构造绘图数组，在UI线程更新ScottPlot。
            UpdateChart();
        }

        // ---------- 界面显示 ----------

        private void ShowTemperature(double temperature)
        {
            temperatureLabel.Text = $"最近温度：{temperature:F1} ℃";

            // 上位机报警阈值从配置读取，与设备的目标温度相互独立。
            if (temperature >= settings.AlarmTemperature)
            {
                statusLabel.Text = "状态：温度超限";
                statusLabel.ForeColor = Color.Red;
            }
            else
            {
                statusLabel.Text = "状态：温度正常";
                statusLabel.ForeColor = Color.ForestGreen;
            }
        }

        private void AddLog(string message)
        {
            DateTime time = DateTime.Now;
            AddUiLog(time, message);
            recorder.WriteLog(time, message);
            UpdateStorageStatus();
        }

        // 保存系统本身的错误只显示在界面，不再尝试写入同一个日志文件。
        // 否则可能出现“写日志失败 -> 再写错误日志 -> 再失败”的循环。
        private void AddUiLog(DateTime time, string message)
        {
            logList.Items.Add($"{time:HH:mm:ss}  {message}");

            if (logList.Items.Count > 200)
            {
                logList.Items.RemoveAt(0);
            }

            logList.TopIndex = logList.Items.Count - 1;
        }

        // ---------- 文件保存状态 ----------

        private void UpdateStorageStatus()
        {
            long failures = recorder.Failures;
            storageLabel.Text =
                $"本次运行：CSV已保存 {recorder.SavedSamples} 条；保存失败 {failures} 项。";

            if (failures > 0)
            {
                // 故障恢复后也保留失败次数，不暗示之前漏存的数据已补回。
                storageLabel.ForeColor = Color.Red;
                storageLabel.Text += $"\n最近保存错误：{recorder.LastError}";
            }
            else
            {
                storageLabel.ForeColor = Color.ForestGreen;
                storageLabel.Text += "\n文件按日期自动追加；CSV保存计数不会随切换通信方式清零。";
            }

            if (failures > shownStorageFailures)
            {
                AddUiLog(DateTime.Now,
                    $"文件保存出现错误（累计{failures}项）：{recorder.LastError}。采集与保存分别统计。" );
                shownStorageFailures = failures;
            }
        }

        private async void OpenFolderButton_Click(object? sender, EventArgs e)
        {
            if (isClosing)
                return;
            openFolderButton.Enabled = false;
            try
            {
                // 首次运行时目录可能尚未创建。
                await Task.Run(() => Directory.CreateDirectory(AppPaths.RootDirectory));
                if (!CanUpdateUi)
                    return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppPaths.RootDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                if (CanUpdateUi)
                    AddUiLog(DateTime.Now, $"无法打开目录：{ex.Message}");
            }
            finally
            {
                if (CanUpdateUi)
                    openFolderButton.Enabled = true;
            }
        }

        // ---------- 窗口关闭 ----------

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;

            isClosing = true;
            isMonitoring = false;
            timer.Stop();
            AddLog("程序关闭。");
            // 当前已经成功保存的文本不需要再排队刷新。
            // 尚未完成的设备读取不再更新UI，也不计作有效采样。
            // 关闭不代表撤销已向设备发出的写入命令。
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            timer.Tick -= Timer_Tick;
            timer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
