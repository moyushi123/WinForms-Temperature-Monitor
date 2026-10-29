// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;
using System.Drawing;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModbusMonitor.Models;
using ModbusMonitor.Services;

namespace ModbusMonitor.Forms
{
    internal sealed class SettingsForm : Form
    {
        public AppSettings? SavedSettings { get; private set; }

        private readonly TextBox portNameInput = new TextBox();
        private readonly NumericUpDown baudInput = NumberInput(300, 921600);
        private readonly ComboBox parityInput = EnumInput<Parity>();
        private readonly NumericUpDown dataBitsInput = NumberInput(8, 8);
        private readonly ComboBox stopBitsInput = new ComboBox();
        private readonly TextBox hostInput = new TextBox();
        private readonly NumericUpDown tcpPortInput = NumberInput(1, 65535);
        private readonly NumericUpDown unitInput = NumberInput(1, 247);
        private readonly NumericUpDown inputAddressInput = NumberInput(0, 65535);
        private readonly NumericUpDown holdingAddressInput = NumberInput(0, 65535);
        private readonly NumericUpDown intervalInput = NumberInput(200, 60000);
        private readonly NumericUpDown alarmInput = NumberInput(0, 100);
        private readonly NumericUpDown timeoutInput = NumberInput(200, 30000);
        private readonly TableLayoutPanel table = new TableLayoutPanel();
        private readonly Button saveButton = new Button();
        private readonly Button cancelButton = new Button();
        private bool isSaving;

        public SettingsForm(AppSettings current)
        {
            Text = "上位机参数设置";
            ClientSize = new Size(620, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;

            Label description = new Label
            {
                Text = "设置仅影响上位机，不写入设备目标温度。\n地址填写从0开始的协议地址；温度换算仍约定为原始值÷10。",
                Dock = DockStyle.Top,
                Height = 62,
                Padding = new Padding(12, 10, 12, 0)
            };

            table.Dock = DockStyle.Fill;
            table.AutoScroll = true;
            table.ColumnCount = 2;
            table.Padding = new Padding(12, 4, 12, 4);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            portNameInput.MaxLength = 32;
            hostInput.MaxLength = 100;
            stopBitsInput.DropDownStyle = ComboBoxStyle.DropDownList;
            stopBitsInput.Items.Add(StopBits.One);
            stopBitsInput.Items.Add(StopBits.Two);
            alarmInput.DecimalPlaces = 1;
            alarmInput.Increment = 0.5m;

            // 只把旧值显示在控件上，不直接更改传进来的配置对象。
            portNameInput.Text = current.SerialPortName;
            baudInput.Value = current.BaudRate;
            parityInput.SelectedItem = current.Parity;
            dataBitsInput.Value = current.DataBits;
            stopBitsInput.SelectedItem = current.StopBits;
            hostInput.Text = current.TcpHost;
            tcpPortInput.Value = current.TcpPort;
            unitInput.Value = current.UnitId;
            inputAddressInput.Value = current.InputRegisterAddress;
            holdingAddressInput.Value = current.HoldingRegisterAddress;
            intervalInput.Value = current.SampleIntervalMs;
            alarmInput.Value = (decimal)current.AlarmTemperature;
            timeoutInput.Value = current.TimeoutMs;

            AddRow("RTU：串口名称", portNameInput);
            AddRow("RTU：波特率", baudInput);
            AddRow("RTU：校验位（Even=偶校验）", parityInput);
            AddRow("RTU：数据位（固定8）", dataBitsInput);
            AddRow("RTU：停止位（One=1位）", stopBitsInput);
            AddRow("TCP：IP地址", hostInput);
            AddRow("TCP：端口", tcpPortInput);
            AddRow("单元标识 / 站号", unitInput);
            AddRow("实际温度：输入寄存器地址", inputAddressInput);
            AddRow("目标温度：保持寄存器地址", holdingAddressInput);
            AddRow("采集间隔（毫秒，200～60000）", intervalInput);
            AddRow("上位机报警阈值（℃）", alarmInput);
            AddRow("通信超时（毫秒，200～30000）", timeoutInput);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            cancelButton.Text = "取消";
            cancelButton.Size = new Size(100, 34);
            cancelButton.DialogResult = DialogResult.Cancel;
            saveButton.Text = "保存并应用";
            saveButton.Size = new Size(130, 34);
            saveButton.Click += SaveButton_Click;
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(saveButton);

            Controls.Add(table);
            Controls.Add(buttons);
            Controls.Add(description);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private static NumericUpDown NumberInput(decimal min, decimal max)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = 0
            };
        }

        private static ComboBox EnumInput<T>() where T : struct, Enum
        {
            ComboBox box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (T value in Enum.GetValues<T>()) box.Items.Add(value);
            return box;
        }

        private void AddRow(string name, Control input)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.Controls.Add(new Label
            {
                Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            input.Dock = DockStyle.Fill;
            table.Controls.Add(input, 1, row);
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            if (isSaving) return;
            try
            {
                // 每次点击保存都收集成新对象；取消时根本不执行这里。
                AppSettings candidate = new AppSettings
                {
                    SerialPortName = portNameInput.Text.Trim().ToUpperInvariant(),
                    BaudRate = (int)baudInput.Value,
                    Parity = (Parity)parityInput.SelectedItem!,
                    DataBits = (int)dataBitsInput.Value,
                    StopBits = (StopBits)stopBitsInput.SelectedItem!,
                    TcpHost = hostInput.Text.Trim(),
                    TcpPort = (int)tcpPortInput.Value,
                    UnitId = (int)unitInput.Value,
                    InputRegisterAddress = (int)inputAddressInput.Value,
                    HoldingRegisterAddress = (int)holdingAddressInput.Value,
                    SampleIntervalMs = (int)intervalInput.Value,
                    AlarmTemperature = (double)alarmInput.Value,
                    TimeoutMs = (int)timeoutInput.Value
                };

                SettingsStore.Validate(candidate);
                isSaving = true;
                table.Enabled = saveButton.Enabled = cancelButton.Enabled = false;
                saveButton.Text = "正在保存…";

                // 文件写入在后台执行。失败则进入catch，当前配置不替换。
                await Task.Run(() => SettingsStore.Save(candidate));
                SavedSettings = candidate;
                isSaving = false;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"设置未应用：{ex.Message}", "设置错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                isSaving = false;
                if (!IsDisposed)
                {
                    table.Enabled = saveButton.Enabled = cancelButton.Enabled = true;
                    saveButton.Text = "保存并应用";
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // 已提交保存后，不能再点取消或叉号制造“取消了但文件已改变”的歧义。
            if (isSaving) e.Cancel = true;
        }
    }
}
