// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;
using System.Drawing;
using System.Windows.Forms;
using ModbusMonitor.Services;
using ModbusMonitor.Infrastructure;

namespace ModbusMonitor.Forms
{
    // partial表示这些文件共同组成一个MonitorForm类，并不是三个窗体。
    public partial class MonitorForm
    {
        private void InitializeWindow()
        {
            // 配置文件很小，在构建窗口前读取并验证；首次运行使用默认值。
            settings = SettingsStore.Load(out string? configurationWarning);

            Text = "Modbus RTU / TCP 温度监控";
            ClientSize = new Size(800, 650);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.None;

            Label modeLabel = new Label();
            modeLabel.Text = "通信方式：";
            modeLabel.SetBounds(20, 24, 85, 25);

            modeBox.SetBounds(110, 20, 160, 30);
            modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modeBox.Items.Add("Modbus RTU");
            modeBox.Items.Add("Modbus TCP");
            modeBox.SelectedIndex = 0;

            connectionLabel.SetBounds(290, 12, 350, 48);
            settingsButton.Text = "参数设置";
            settingsButton.SetBounds(650, 16, 130, 34);
            settingsButton.Click += SettingsButton_Click;

            startButton.Text = "开始监控";
            startButton.SetBounds(20, 70, 140, 40);

            stopButton.Text = "停止监控";
            stopButton.SetBounds(180, 70, 140, 40);

            countButton.Text = "点我计数";
            countButton.SetBounds(340, 70, 140, 40);

            timeoutCheckBox.Text = "模拟读取超时";
            timeoutCheckBox.SetBounds(510, 75, 220, 30);

            temperatureLabel.Text = "最近温度：-- ℃";
            temperatureLabel.SetBounds(20, 135, 750, 30);

            statusLabel.Text = "状态：尚未开始";
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.SetBounds(20, 175, 750, 45);

            sampleCountLabel.Text = "有效采样次数：0";
            sampleCountLabel.SetBounds(20, 230, 320, 30);

            clickCountLabel.Text = "计数按钮点击次数：0";
            clickCountLabel.SetBounds(360, 230, 390, 30);

            // 提示文字由当前配置生成，不再写死为每1秒。
            explanationLabel.SetBounds(20, 275, 620, 30);

            openFolderButton.Text = "打开保存目录";
            openFolderButton.SetBounds(650, 270, 130, 32);
            openFolderButton.Click += OpenFolderButton_Click;

            saveFailureCheckBox.Text = "模拟CSV保存失败";
            saveFailureCheckBox.SetBounds(510, 301, 260, 25);

            Label targetLabel = new Label();
            targetLabel.Text = "目标温度（℃）：";
            targetLabel.SetBounds(20, 325, 140, 30);

            targetInput.SetBounds(165, 323, 110, 30);
            targetInput.Minimum = 0;
            targetInput.Maximum = 100;
            targetInput.DecimalPlaces = 1;
            targetInput.Increment = 0.5m;
            targetInput.Value = 35.0m;

            writeButton.Text = "写入目标温度";
            writeButton.SetBounds(310, 320, 180, 38);

            cacheLabel.Text = "曲线缓存：0 / 100条";
            cacheLabel.SetBounds(510, 326, 260, 25);

            // 使用页签，在原有窗口内同时容纳曲线和日志。
            // 本版本使用ScottPlot.WinForms 5.1.59，需要先安装对应NuGet包。
            autoScaleCheckBox.Text = "自动适应曲线范围";
            autoScaleCheckBox.SetBounds(510, 350, 240, 25);
            autoScaleCheckBox.Checked = true;
            storageLabel.SetBounds(20, 380, 760, 46);
            storageLabel.AutoEllipsis = true;
            displayTabs.SetBounds(20, 430, 760, 200);
            TabPage chartPage = new TabPage("实时曲线");
            TabPage logPage = new TabPage("运行日志");

            temperatureChart.Dock = DockStyle.Fill;
            chartPage.Controls.Add(temperatureChart);
            InitializeChart();

            logList.Dock = DockStyle.Fill;
            logList.HorizontalScrollbar = true;
            logList.IntegralHeight = false;
            logPage.Controls.Add(logList);

            displayTabs.TabPages.Add(chartPage);
            displayTabs.TabPages.Add(logPage);

            Controls.AddRange(new Control[]
            {
                modeLabel,
                modeBox,
                connectionLabel,
                settingsButton,
                startButton,
                stopButton,
                countButton,
                timeoutCheckBox,
                temperatureLabel,
                statusLabel,
                sampleCountLabel,
                clickCountLabel,
                explanationLabel,
                targetLabel,
                targetInput,
                writeButton,
                cacheLabel,
                openFolderButton,
                saveFailureCheckBox,
                storageLabel,
                autoScaleCheckBox,
                displayTabs
            });

            // ---------- 事件只订阅一次 ----------

            modeBox.SelectedIndexChanged += ModeBox_SelectedIndexChanged;
            startButton.Click += StartButton_Click;
            stopButton.Click += StopButton_Click;
            countButton.Click += CountButton_Click;
            writeButton.Click += WriteButton_Click;
            autoScaleCheckBox.CheckedChanged += (_, _) =>
            {
                // 取消勾选后不再每次自动调整范围，可自由缩放和拖动。
                // 再勾选时，立即显示当前缓存的全部点。
                if (autoScaleCheckBox.Checked && samples.Count > 0)
                {
                    temperatureChart.Plot.Axes.AutoScale();
                    temperatureChart.Refresh();
                }
            };

            timer.Interval = settings.SampleIntervalMs;
            UpdateSamplingDescription();
            timer.Tick += Timer_Tick;

            UpdateConnectionLabel();
            UpdateControls();

            AddLog("程序启动，请选择通信方式并启动对应的模拟设备。");
            AddLog($"自动保存目录：{AppPaths.RootDirectory}");
            UpdateStorageStatus();
            AddLog($"配置文件：{SettingsStore.FilePath}");
            if (configurationWarning != null)
            {
                AddLog(configurationWarning);
                Shown += (_, _) => MessageBox.Show(this,
                    configurationWarning + "\n请在参数设置中检查后，再开始监控。",
                    "配置读取提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
