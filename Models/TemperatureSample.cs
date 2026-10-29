// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;

namespace ModbusMonitor.Models
{
    public class TemperatureSample
    {
        // 上位机成功收到本次温度的时间。
        public DateTime Time { get; set; }

        // 换算后的温度，单位℃。
        public double Temperature { get; set; }

        // 单调计时用于横轴位置，避免系统时钟校准导致曲线倒退。
        public double ElapsedSeconds { get; set; }

        // true表示这个点和上一个点之间不画连线，例如读取失败后恢复。
        public bool BreakBefore { get; set; }
    }
}
