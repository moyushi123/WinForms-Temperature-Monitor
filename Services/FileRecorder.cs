using System;
using System.Globalization;
using System.IO;
using System.Text;
using ModbusMonitor.Infrastructure;

namespace ModbusMonitor.Services
{
    // 小型学习Demo的本地文件保存。所有调用都来自UI线程，按顺序执行。
    // 每次采集只写一条记录；高频采集或慢磁盘场景再改为后台队列。
    internal sealed class FileRecorder
    {
        public int SavedSamples { get; private set; }
        public int Failures { get; private set; }
        public string LastError { get; private set; } = "";

        public void SaveSample(DateTime time, string mode,
            double temperature, bool simulateFailure)
        {
            try
            {
                if (simulateFailure)
                    throw new IOException("手动模拟CSV保存失败");

                string folder = Path.Combine(AppPaths.RootDirectory, "Samples");
                Directory.CreateDirectory(folder);
                string date = time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string path = Path.Combine(folder, $"samples_{date}.csv");

                bool needHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                string timeText = time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                string valueText = temperature.ToString("F1", CultureInfo.InvariantCulture);
                string content = $"{timeText},{mode},{valueText}" + Environment.NewLine;
                if (needHeader)
                    content = "Time,Mode,TemperatureC" + Environment.NewLine + content;

                File.AppendAllText(path, content, Encoding.UTF8);
                SavedSamples++; // 写入成功后才增加，发生异常则不会执行。
            }
            catch (Exception ex)
            {
                Failures++;
                LastError = $"CSV保存失败：{ex.Message}";
                // 不再抛回采集代码，避免把保存失败误报为读取设备失败。
            }
        }

        public void WriteLog(DateTime time, string message)
        {
            try
            {
                string folder = Path.Combine(AppPaths.RootDirectory, "Logs");
                Directory.CreateDirectory(folder);
                string date = time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string path = Path.Combine(folder, $"run_{date}.txt");
                string timeText = time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                message = message.Replace("\r", " ").Replace("\n", " ");
                File.AppendAllText(path,
                    $"{timeText}  {message}" + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Failures++;
                LastError = $"TXT保存失败：{ex.Message}";
                // 不再次写错误日志；窗体负责显示这个错误。
            }
        }
    }
}
