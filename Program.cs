// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;
using System.Windows.Forms;
using ModbusMonitor.Forms;

namespace ModbusMonitor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MonitorForm());
        }
    }
}
