using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModbusMonitor.Models;
using ModbusMonitor.Infrastructure;

namespace ModbusMonitor.Services
{
    internal static class SettingsStore
    {
        public static string FilePath => Path.Combine(AppPaths.RootDirectory, "settings.json");
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // 只保留会影响正常运行的基本检查，两种入口都调用同一个方法。
        public static void Validate(AppSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.SerialPortName))
                throw new InvalidDataException("串口名称不能为空。");
            if (s.BaudRate < 300 || s.BaudRate > 921600)
                throw new InvalidDataException("本Demo波特率范围为300～921600。");
            if (!Enum.IsDefined(typeof(Parity), s.Parity) || s.DataBits != 8 ||
                (s.StopBits != StopBits.One && s.StopBits != StopBits.Two))
                throw new InvalidDataException("RTU需要8个数据位、有效校验位和1或2个停止位。");
            if (string.IsNullOrWhiteSpace(s.TcpHost) || !IPAddress.TryParse(s.TcpHost, out _))
                throw new InvalidDataException("请输入合法的IP地址。");
            if (s.TcpPort < 1 || s.TcpPort > 65535)
                throw new InvalidDataException("TCP端口范围为1～65535。");
            if (s.UnitId < 1 || s.UnitId > 247)
                throw new InvalidDataException("本Demo单元标识范围为1～247。");
            if (s.InputRegisterAddress < 0 || s.InputRegisterAddress > 65535 ||
                s.HoldingRegisterAddress < 0 || s.HoldingRegisterAddress > 65535)
                throw new InvalidDataException("寄存器协议地址范围为0～65535。");
            if (s.SampleIntervalMs < 200 || s.SampleIntervalMs > 60000)
                throw new InvalidDataException("采集间隔范围为200～60000毫秒。");
            if (s.TimeoutMs < 200 || s.TimeoutMs > 30000)
                throw new InvalidDataException("通信超时范围为200～30000毫秒。");
            if (!double.IsFinite(s.AlarmTemperature) || s.AlarmTemperature < 0 || s.AlarmTemperature > 100)
                throw new InvalidDataException("本Demo报警阈值范围为0～100℃。");
        }

        public static AppSettings Load(out string? warning)
        {
            warning = null;
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();

                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded == null) throw new InvalidDataException("配置对象为空。");
                Validate(loaded);
                return loaded;
            }
            catch (Exception ex)
            {
                warning = $"配置读取失败：{ex.Message}\n本次使用默认设置，原文件没有改动。";
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            Validate(settings);
            Directory.CreateDirectory(AppPaths.RootDirectory);
            string json = JsonSerializer.Serialize(settings, Options);
            // 配置保存当前整套设置，使用覆盖，不使用追加。
            // 本学习版不做临时文件替换/自动备份；生产项目再补。
            File.WriteAllText(FilePath, json, Encoding.UTF8);
        }
    }
}
