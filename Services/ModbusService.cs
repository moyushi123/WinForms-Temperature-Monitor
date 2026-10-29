// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using NModbus;
using NModbus.Serial;
using ModbusMonitor.Models;

namespace ModbusMonitor.Services
{
    // 通信服务不引用窗体或控件，由窗体在Task.Run中调用。
    internal static class ModbusService
    {
        // ---------- 后台通信 ----------
        // 下面这些方法不访问窗体控件。

        public static double ReadTemperature(
            CommunicationMode mode, AppSettings config, bool simulateTimeout)
        {
            if (simulateTimeout)
            {
                Thread.Sleep(3000);
                throw new TimeoutException("手动模拟读取超时");
            }

            return ExecuteWithMaster(mode, config, master =>
            {
                // 地址是协议中从0开始的地址，不是30001/40001形式的显示编号。
                ushort[] values = master.ReadInputRegisters(
                    (byte)config.UnitId, (ushort)config.InputRegisterAddress, 1);
                return values[0] / 10.0;
            });
        }

        public static double WriteAndReadTarget(
            CommunicationMode mode, AppSettings config, ushort rawValue)
        {
            return ExecuteWithMaster(mode, config, master =>
            {
                // 写入和读回使用同一条连接，
                // 并且整个过程都占用isBusy。
                master.WriteSingleRegister(
                    (byte)config.UnitId, (ushort)config.HoldingRegisterAddress, rawValue);

                ushort[] values = master.ReadHoldingRegisters(
                    (byte)config.UnitId, (ushort)config.HoldingRegisterAddress, 1);

                if (values[0] != rawValue)
                {
                    throw new InvalidOperationException(
                        $"读回值不一致：要求{rawValue}，实际{values[0]}");
                }

                return values[0] / 10.0;
            });
        }

        // operation代表“连接建立后，要执行的工作”。
        // 它接收一个IModbusMaster，最后返回一个double。
        private static double ExecuteWithMaster(
            CommunicationMode mode,
            AppSettings config,
            Func<IModbusMaster, double> operation)
        {
            ModbusFactory factory = new ModbusFactory();

            if (mode == CommunicationMode.Rtu)
            {
                using SerialPort port = new SerialPort(
                    config.SerialPortName,
                    config.BaudRate,
                    config.Parity,
                    config.DataBits,
                    config.StopBits);

                port.ReadTimeout = config.TimeoutMs;
                port.WriteTimeout = config.TimeoutMs;
                port.Handshake = Handshake.None;

                port.Open();

                using IModbusSerialMaster master =
                    factory.CreateRtuMaster(port);

                ConfigureMaster(master, config);

                // 执行本次读取，或者写入并读回。
                // operation返回后，using会释放本次通信资源。
                return operation(master);
            }
            else
            {
                using TcpClient client = new TcpClient();

                // 给建立TCP连接单独设置超时。
                // Modbus的ReadTimeout不会控制连接阶段。
                using CancellationTokenSource connectTimeout =
                    new CancellationTokenSource(config.TimeoutMs);

                try
                {
                    // 当前方法由Task.Run调用，运行在后台线程。
                    // 在这里等待连接，不会阻塞WinForms的UI线程。
                    client.ConnectAsync(
                            config.TcpHost,
                            config.TcpPort,
                            connectTimeout.Token)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("TCP连接超时");
                }

                using IModbusMaster master = factory.CreateMaster(client);

                ConfigureMaster(master, config);

                return operation(master);
            }
        }

        private static void ConfigureMaster(IModbusMaster master, AppSettings config)
        {
            master.Transport.ReadTimeout = config.TimeoutMs;
            master.Transport.WriteTimeout = config.TimeoutMs;

            // 教学阶段关闭自动重试，便于观察一次请求的结果。
            master.Transport.Retries = 0;
        }

    }
}
