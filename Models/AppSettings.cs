// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System.IO.Ports;

namespace ModbusMonitor.Models
{
    // 普通配置属性。编辑时创建新对象，不直接修改正在使用的设置。
    public sealed class AppSettings
    {
        public string SerialPortName { get; set; } = "COM10";
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = System.IO.Ports.Parity.Even;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;
        public string TcpHost { get; set; } = "127.0.0.1";
        public int TcpPort { get; set; } = 1502;
        public int UnitId { get; set; } = 1;
        public int InputRegisterAddress { get; set; } = 0;
        public int HoldingRegisterAddress { get; set; } = 0;
        public int SampleIntervalMs { get; set; } = 1000;
        public double AlarmTemperature { get; set; } = 35.0;
        public int TimeoutMs { get; set; } = 2000;
    }
}
