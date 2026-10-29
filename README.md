# Modbus温度监控：简单分文件版

## 运行

1. 解压到新的文件夹，保留旧项目作参考。
2. 用Visual Studio打开 `ModbusMonitor.csproj`，等待NuGet还原后按F5。
3. 需要.NET 8 SDK和“.NET桌面开发”工作负载。包版本已经写在项目文件里。
4. 使用此前已经运行成功的RTU/TCP模拟器；本压缩包不包含模拟器项目。

这是源码项目，不是已经发布的exe。不要把旧的完整Program.cs再加入新项目。
窗口由代码创建，没有Designer文件，不需要新增Form1。

## 架构定位

这是WinForms界面、服务、数据模型按职责分开的入门项目，不是MVVM，也不是企业级完整分层架构。
没有ViewModel、绑定命令、依赖注入容器。等WPF章节再正式学习MVVM。
不要为了看起来“像初学者”故意制造缺陷；保留必要检查，把暂时不需要的复杂机制去掉即可。

| 文件 | 修改什么时去找它 |
| --- | --- |
| Program.cs | 程序入口，只负责启动窗口 |
| Forms/MonitorForm.cs | 开始/停止、异步读取、按钮事件、状态显示 |
| Forms/MonitorForm.Layout.cs | 主界面布局、控件初始化、事件订阅 |
| Forms/MonitorForm.Chart.cs | ScottPlot显示 |
| Forms/SettingsForm.cs | 设置窗口、保存和取消按钮 |
| Models/AppSettings.cs | 配置数据 |
| Models/TemperatureSample.cs | 一条采样记录 |
| Models/CommunicationMode.cs | RTU/TCP枚举 |
| Services/ModbusService.cs | 打开连接、读温度、写目标值并读回 |
| Services/SettingsStore.cs | 参数检查、JSON加载保存 |
| Services/FileRecorder.cs | CSV和TXT简单追加 |
| Infrastructure/AppPaths.cs | 本地数据目录 |

MonitorForm的三个文件通过partial组成同一个类，共用字段，只创建一个窗体。
文件拆分不改变线程规则：界面事件在UI线程，ModbusService经Task.Run在后台执行，await后更新UI。
服务中不访问任何窗体控件。

## 保留和简化的内容

保留：RTU/TCP读写、超时、isBusy防重入、异常处理、跨线程限制、写入读回验证、最近100条曲线、保存失败提示、配置范围检查、取消不改配置。

简化：去掉Channel后台保存队列、Interlocked、专门的保存状态定时器、关闭排空队列、配置临时文件替换和自动备份。
配置属性使用get/set，设置窗口创建独立新对象；监控或通信时不能编辑，避免修改当前通信设置。
CSV/TXT使用普通同步追加。每次只是少量本地文本，适合当前低频学习Demo；磁盘慢时可能短暂影响界面，不适合直接用于高频生产采集。
JSON使用WriteAllText覆盖。写入失败或进程中断时文件可能不完整，当前配置不会因此应用；下次加载失败会提示并使用默认值。此版不保证文件原子更新。
关闭窗口时停止后续采集，晚到的通信结果不再更新UI；不撤销已发出的写入请求，也不额外等待设备通信。

## 数据和设置

继续使用 `%LOCALAPPDATA%\ModbusMonitor`。会加载之前保存的settings.json，原CSV/TXT保留。
旧版本产生的settings.json.bak不会自动删除，也不会自动加载；新版本不再创建备份。
RTU/TCP下拉选择本身不保存，启动时默认RTU。按实际模拟器选择即可。
新旧项目请先后运行，避免同时占用串口或写同一个日志文件。

## 建议阅读顺序

Program.cs → MonitorForm.Layout.cs → MonitorForm.cs里的Timer_Tick → ModbusService.ReadTemperature → SaveSample → MonitorForm.Chart.cs → FileRecorder.SaveSample。

修改练习：先尝试改一个按钮文字，再修改曲线颜色，最后解释一个温度从设备到CSV的完整路径。

## 验证说明

已检查源文件引用、入口唯一性、服务不依赖UI、删除机制无残留及压缩包完整性。
整理环境没有.NET SDK且不能运行Windows窗体，未进行编译和界面实测。
请先在Visual Studio确认启动及基本读取正常，若报错提供完整错误信息。
