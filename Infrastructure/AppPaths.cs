// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;
using System.IO;

namespace ModbusMonitor.Infrastructure
{
    // 配置和日志共用的根目录，保持与单文件版本一致。
    internal static class AppPaths
    {
        public static string RootDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModbusMonitor");
    }
}
