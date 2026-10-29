# PLCClient

个人开发的一款 PLC 客户端以及服务端测试工具。旨在将工作中常用的 PLC 全部实现模拟通讯。缓慢开发中……

| 项目 | 说明 |
| --- | --- |
| 工程文件 | `PlcTest.csproj`（老式非 SDK 工程，程序集清单手工维护） |
| 程序集 / 根命名空间 | `PlcTest` |
| 运行环境 | .NET Framework 4.7.2 · WinForms（`OutputType=WinExe`） |
| 第三方依赖 | 无，只用 BCL |
| 解决方案 | `PlcTest.slnx` |

## 软件架构

![项目架构总览](./Document/md/项目架构总览.png)

整体分三层：**模型层（`Models`）→ 通讯层（`Client` / `Server`）→ 界面层（`View`）**，依赖单向，`Models` 为叶节点。

通讯层把「怎么发数据」和「数据怎么编解码」拆成两条互不依赖的抽象，任一侧换实现都不影响另一侧：

| 层次 | 接口 | 现有实现 | 职责 |
| --- | --- | --- | --- |
| 客户端 · 通讯方式 | `IClientCommunication` | `TcpClientCommunication`、`SerialPortClientCommunication` | 建连、收发原始字节（收发串行化；按协议给出的帧长读满一帧） |
| 客户端 · 通讯协议 | `IPlcClientProtocol` | `ClientProtocolManagement\` 下各厂商协议 | 组装请求报文、解析响应报文、声明区域访问能力与应答帧长 |
| 客户端 · 设备 | `IPlcClient` | `CommunicationPlcClient`（单一实现，构造注入通讯、协议与位/字规则） | 连接、心跳与 8 个读写 |
| 服务端 · 通讯方式 | `IServerCommunication` | `TcpServerCommunication`、`SerialServerCommunication` | 监听端口、收发字节，每个连接用 `IClientSession` 标识 |
| 服务端 · 通讯协议 | `IServerProtocol` / `IModbusServerProtocol` | 三菱 6 个（1E/3E/4E × Binary/ASCII）+ 汇川 2 个（Modbus TCP/RTU 从站） | **帧定界**、解析请求、构建响应（含结束码 / 异常码与写数据解码） |
| 服务端 · 设备 | `IPlcServer` | `MelsecPlcServer`（+`MelsecMc3EPlcServer` 薄子类）、`InovancePlcServer` | 回应客户端的读写请求 |
| 服务端 · 寄存器表 | `IRegisterTable` | `MelsecPlcServer`、`InovancePlcServer` | 模拟寄存器的增删查改与清零 |

贯穿全项目的几套惯用写法：

- **接口抽象**：上表每一行两端都靠接口隔离，厂商代码只碰接口不碰具体类。
- **建造者**：`PlcClientBuilder` / `PlcServerBuilder` 流式拼装「通讯方式 + 协议 + 实例」，一行链式调用出实例。
- **组合收敛**：`CommunicationPlcClient` 用同一个类承担连接、断开、心跳重连与 8 个读写的「拼包 → 发送 → 解析」骨架；通讯对象与协议实现由构造函数注入，不再为每个厂商派生空子类，异常统一收敛成失败 `Result`。
- **位/字只有一处真相**：能不能按位/按字访问由协议声明（`IPlcClientProtocol.GetAreaAccess`），客户端按方法语义（`ReadBit`/`ReadWord`）归一化地址标志，不再维护「D 为字、其余为位」这类客户端侧规则。
- **界面跟着协议走**：`IPlcClient.GetSupportedAreas()` 返回当前协议真正支持的软元件区域，`PlcClientForm` 用它填充三个下拉框（读区 / 写位 / 写字），**PLC 上不存在的软元件不会出现在选项里**；位/字分支也改成 `GetAreaAccess` 判断，不再硬编码"是不是 D"。断开连接时会关闭绑定了旧协议的窗体，重连后列表不会残留。
- **三菱软元件表集中**：`Melsec3E4EDeviceCatalog` 是 **QnA 兼容 3E / 4E 帧共用**的软元件表（两者只有帧头不同，软元件代码与编号规则一致），是三菱软元件代码、位/字能力、**编号进制**与默认编号范围的唯一来源（依据《基本型 QCPU 软元件一览》）。3E / 4E × ASCII / Binary 四个协议全部改为查表，不再各写一份 28 个 `case` 的 switch；ASCII 地址按「X/Y/B/W/SB/SW/DX/DY/ZR 十六进制，其余十进制」格式化。
- **1E 单独一张表**：A 兼容 1E 帧（FX 系列）与 3E 规则不同——单字符代码补**空格**(0x20) 而非 `*`、编号统一十六进制、只支持 12 种软元件（D / R / X / Y / M / S / TN / TS / TC / CN / CS / CC），故单列 `Melsec1EDeviceCatalog`（依据 `Document/Melsec/1E寄存器地址.png` 与 Q/L 系列 MC 协议手册 442–444 页）。该表带机型概念（`Melsec1ECpuModel`，默认 **FX3G/FX3GC**）。报文顺序已按手册 436 页示例核实：**起始编号 → 软元件名 → 点数**，与实现一致。
- **汇川 Modbus 一张表**：`InovanceDeviceCatalog` 统管起始地址、地址空间（线圈 01/05 与保持寄存器 03/06/16）、**地址分段**与各机型编号范围（`InovanceCpuModel`，默认 **H3U**）。Easy 与 H3U 的 S/M/X/Y/D/R 基地址完全一致，差异只在范围与机型独有区：**Easy 有 B 区，H3U 有 SM/SD/T/C**。线圈区只能按位、寄存器区只能按字——与三菱"位软元件也能按字读"完全不同。
- **32 位计数器按段处理**：汇川 `CN` 分两段——`C0–C199` 在 0xF400（16 位，每点 1 个寄存器）、`C200–C255` 在 0xF700（32 位，**每点 2 个寄存器**）。地址按段换算，寄存器数量 = 点数 × 每点寄存器数，因此 32 位段永远不会落到 0x06（32 位寄存器不支持单写）；跨段访问（如 `C195` 起 10 点）直接拒绝，不发出必然错位的报文。读响应按实际字节数解析，避免 32 位值被砍掉一半。
- **Modbus 数量上限前置校验**：`InovanceModbusLimits` 记录单帧上限（读线圈 2000 / 读寄存器 125 / 写线圈 1968 / 写寄存器 123，见 H3U 手册 9.4.2），协议在拼包前按**换算后的寄存器数**校验，超限直接报「一次最多读 125 个寄存器，当前请求 200 个」，不用等 PLC 回一个看不懂的异常码。注意写比读少 2，且 32 位段的点数要先乘 2 再判。
- **编号上限归协议、越界判断归客户端**：`IPlcClientProtocol.GetMaxIndex` 只声明"这个区域最大编号多少"（-1 表示不限制，范围由从站或 PLC 组态决定），起始点与连续访问末点的越界计算统一在 `CommunicationPlcClient.CheckAddress` 里做，越界返回 `ErrorKind.Validation`，不发给 PLC。协议不必各写一遍同样的校验逻辑。
- **帧定界归协议**：`IPlcClientProtocol.GetResponseFrameLength` 告诉通讯层「这一帧多长」，通讯层按长度读满；协议判不出长度（如 MC 1E 无长度字段、4E ASCII 头长存在歧义）时返回 -1，才回退到「空闲 60 ms 判据」。
- **错误可分类**：`Result<T>` 带 `ErrorKind`（Validation / Transport / Protocol / DeviceEndCode）与 `EndCode`，调用方不必解析 `ErrorMsg` 字符串。
- **模板方法**：`TemplateClientProtocol` / `TemplatePlcClient` 是留白的骨架，新增厂商时复制这两个文件往里填就行。
- **依赖注入**：客户端与服务端的构造函数都接收通讯接口与协议接口，传 `null` 时内部 `new` 默认实现，方便替换和测试。
- **心跳保活**：`Connect()` 同步建连并返回 `Result`（失败原因直接可见），成功后才起后台心跳线程，1 s 一轮，断线自动重连（`CommunicationPlcClient.HeartBeatLoop`）。

### 核心功能

- 实现 PLC 客户端通讯
- 实现 PLC 服务端模拟

## 统一地址模型

各厂商共用 `Models\PlcAddress.cs`，屏蔽「三菱的 D100」「西门子的 DB1.DBW0」这类差异：

```csharp
// 字地址：D100，长度 1
PlcAddress.Word(MemoryArea.D, 100);

// 位地址：M10
PlcAddress.Bit(MemoryArea.M, 10);

// 西门子 DB 块，10 个字
PlcAddress.Word(MemoryArea.D, 0, dbNumber: 1, length: 10);
```

- `PlcAddress` 只携带 `Area` / `Address` / `Length` / `IsBit` / `DbNumber` 五个字段，位与字的区别由 `IsBit` 表达。
- `MemoryArea` 覆盖三菱软元件：`SM` `SD` `X` `Y` `M` `L` `F` `V` `B` `D` `W` `TS` `TC` `TN` `SS` `SC` `SN` `CS` `CC` `CN` `SB` `SW` `S` `DX` `DY` `Z` `R` `ZR`。
- 读写统一返回 `Result<T>`（`IsSuccess` / `Data` / `ErrorMsg`），失败不抛异常。

## 协议支持进度

客户端全部通过 `PlcClientBuilder` 选择协议：

| 厂商 | 协议 | 建造者方法 | 协议实现 | 客户端封装 | 服务端模拟 |
| --- | --- | --- | --- | --- | --- |
| 三菱 | MC 1E ASCII | `UseMelsecMc1EAscii()` | ✅ | ✅ | ✅ |
| 三菱 | MC 1E Binary | `UseMelsecMc1EBinary()` | ✅ | ✅ | ✅ |
| 三菱 | MC 3E ASCII | `UseMelsecMc3EAscii()` | ✅ | ✅ | ✅ |
| 三菱 | MC 3E Binary | `UseMelsecMc3EBinary()` | ✅ | ✅ | ✅ |
| 三菱 | MC 4E ASCII | `UseMelsecMc4EAscii()` | ✅ | ✅ | ✅ |
| 三菱 | MC 4E Binary | `UseMelsecMc4EBinary()` | ✅ | ✅ | ✅ |
| 西门子 | S7-1200（S7comm） | `UseSiemensS71200()` | ✅ | ✅ | — |
| 欧姆龙 | FINS TCP | `UseOmronFinsTcp()` | ❌ 未实现 | ❌ 读写未实现 | — |
| 汇川 | Modbus TCP | `UseInovanceModbusTcp()` | ✅ | ✅ | ✅ `UseInovanceModbusTcp()` |
| 汇川 | Modbus RTU | `UseInovanceModbusRtu()` | ✅ | ✅ | ✅ `UseInovanceModbusRtu()` |

> 表中「协议实现 ✅」指报文编解码已写完。
>
> 未实现的入口不会在运行时才炸：`UseOmronFinsTcp()`、`BuildOmronPlcClient()` 直接抛 `NotSupportedException`；三菱 4E ASCII 与西门子 S7 已接入客户端封装，可正常构建使用。

服务端已覆盖 **8 种协议**：三菱 MC 六种（**1E / 3E / 4E × ASCII / Binary**）+ 汇川 Modbus 两种（**TCP / RTU 从站**）。

- 三菱六者共用 `MelsecPlcServer`（`0x0401` 批量读、`0x1401` 批量写），帧格式差异全部由注入的 `IServerProtocol` 承担；`MelsecMc3EPlcServer` 只是绑定默认协议的薄子类。
- **服务端的软元件范围跟着 catalog 走**：设备码 → 区域的解码统一走 `MelsecServerDeviceMapper`，直接查客户端同一份 `Melsec3E4EDeviceCatalog`（28 种）/ `Melsec1EDeviceCatalog`（12 种），不再只认 `D`/`M`/`X`/`Y`。**客户端认得的软元件，服务端就模拟**，不会出现"客户端能发、服务端装不认识"的错位。
- 位 / 字按请求里的单位标志路由（3E/4E 看子命令 bit0，1E 看功能码最低位），每条区域各存一张位表与一张字表；**同一区域既能按位又能按字时（如 M），两张表互不联动**——这是模拟器为了简单做的取舍，不是真实 PLC 的行为。
- 汇川由 `InovancePlcServer` 承担，按 Modbus 功能码 **01 / 03 / 05 / 06 / 0F / 10** 处理，内部是两张按 Modbus 地址索引的表（线圈位、保持寄存器）；地址映射复用客户端的 `InovanceDeviceCatalog`，因此 `C200–C255` 的 32 位段在服务端同样成立。异常按规范返回 `功能码 | 0x80` + 异常码（01 非法功能 / 02 非法地址 / 03 非法数据值）。
- **帧定界已归协议**：`IServerProtocol.GetRequestFrameLength` / `IModbusServerProtocol.GetRequestFrameLength` 决定「这一帧读多长」，通讯层不再写死 3E Binary 的 9 字节头；返回 -1（如 MC 1E 的写请求）时回退到 60 ms 空闲判据。

## 快速开始

环境：Visual Studio 2019 及以上，或装有 .NET Framework 4.7.2 Developer Pack 的 MSBuild。

```powershell
# 用 Visual Studio 打开
start PlcTest.slnx

# 或者命令行重建
MSBuild PlcTest.csproj /t:Rebuild /p:Configuration=Debug
```

作为客户端库使用的最小例子：

```csharp
using PlcTest.Models;                          // PlcClientBuilder / PlcAddress / MemoryArea
using PlcTest.Client.ClientInterface;          // IPlcClient

var client = new PlcClientBuilder()
    .UseTcp("192.168.1.10", 5000)
    .UseMelsecMc3EBinary()
    .BuildMelsecMcPlcClient();

client.ShowCommMessageEvent += msg => Console.WriteLine(msg);   // 交互报文日志

Result<bool> connectResult = client.Connect();                  // 同步建连，失败原因直接返回
if (!connectResult.IsSuccess)
{
    Console.WriteLine(connectResult.ErrorMsg);
    return;
}

Result<short[]> read = client.ReadWordArray(PlcAddress.Word(MemoryArea.D, 100, null, 10));
if (read.IsSuccess)
{
    foreach (short v in read.Data)
    {
        Console.WriteLine(v);
    }
}
else
{
    Console.WriteLine("读取失败：" + read.ErrorMsg);
}

client.WriteWord(PlcAddress.Word(MemoryArea.D, 100), 1234);
client.WriteBit(PlcAddress.Bit(MemoryArea.M, 10), true);

client.Disconnect();
```

串口通讯把 `UseTcp(...)` 换成 `UseSerial("COM3", 9600, Parity.None, 8, StopBits.One)` 即可，协议那一行不动。

服务端用对称的 `PlcServerBuilder` 起步：

```csharp
using PlcTest.Server;
using PlcTest.Server.ServerInterface;

IPlcServer server = new PlcServerBuilder()
    .UseTcp("127.0.0.1", 5000)
    .UseMelsecMc3E()
    .Build();
server.OpenServer();   // 之后客户端即可按 MC 3E Binary 连入并读写模拟寄存器

// 汇川 Modbus 从站（TCP / RTU 二选一，端口按 Modbus 惯例 502）
IPlcServer modbusServer = new PlcServerBuilder()
    .UseTcp("127.0.0.1", 502)
    .UseInovanceModbusTcp()
    .Build();
modbusServer.OpenServer();
```

## 界面

| 窗体 | 功能 |
| --- | --- |
| `MainForm` | 客户端 / 服务端连接参数配置、连接状态指示灯、打开寄存器视图；连接客户端与开启服务端时都会弹出「通讯方式 + 协议」选择框 |
| `ConnectSettingsForm` | 连接设置对话框：选 TCP / 串口与协议；串口模式下 IP 框填串口名（如 COM3）、端口框填波特率。客户端模式列出全部客户端协议，服务端模式列出支持的 8 种（三菱 6 种 + 汇川 Modbus TCP/RTU） |
| `PlcClientForm` | 选区域 + 起始地址 + 长度后周期读取（1 s），表格展示：D 区展开 16 个位并附高字节 / 低字节 / short / ASCII，其他区按点位显示；双击位格反写回 PLC；单点写位 / 写字；区间清零与全区清零；通讯报文日志框 |
| `PlcServerForm` | 初始化服务端模拟寄存器的区域与范围、单点改写模拟值、清零 |

界面文案目前是英文（`Please Check ...`），控件命名沿用 `btn_` / `tb_` / `lab_` / `cmb_` / `DGV` 前缀，Designer 生成的代码不动。

## 目录结构

```
PLCClient/
├─ Models/                         统一模型
│  ├─ PlcAddress.cs                统一 PLC 地址模型
│  ├─ Result.cs                    统一返回结果
│  ├─ RequestInfo.cs               服务端解析出的请求结构（含强类型 DeviceArea）
│  └─ Enums.cs                     MemoryArea / PlcType / CommunicationType
├─ Client/
│  ├─ ClientInterface/             IClientCommunication / IPlcClient / IPlcClientProtocol
│  ├─ ClientCommunication/         TCP、串口通讯实现（内部串行化 + 读全帧）
│  ├─ ClientProtocolManagement/    各厂商协议实现
│  │  ├─ Melsec/                   1E/3E/4E × ASCII/Binary
│  │  ├─ Siemens/                  S7-1200
│  │  ├─ Omron/                    FINS TCP
│  │  ├─ Inovance/                 Modbus RTU / TCP
│  │  └─ Template/                 新协议模板
│  ├─ PlcClient/                   CommunicationPlcClient（组合式客户端）+ Omron/Template 骨架
│  └─ PlcClientBuilder.cs          客户端建造者
├─ Server/
│  ├─ ServerInterface/             IPlcServer / IRegisterTable / IServerCommunication / IServerProtocol / IClientSession
│  ├─ ServerCommunication/         TCP 监听、串口
│  ├─ ServerProtocolManagement/Melsec/   1E/3E/4E × ASCII/Binary 协议解析与响应构建
│  ├─ PlcServer/Melsec/            MelsecPlcServer 通用模拟服务 + MelsecMc3EPlcServer 薄子类
│  └─ PlcServerBuilder.cs          服务端建造者
├─ View/
│  ├─ Forms/                       MainForm / PlcClientForm / PlcServerForm / ConnectSettingsForm（命名空间 PlcTest.View.Forms）
│  └─ CustomControl/               MGroupBox 自绘分组框
├─ Tool/                           ConverterTool（位 / 字节 / ASCII 转换）、OtherTool（校验）、StructureHelp
├─ Document/                       协议报文与架构资料
└─ Program.cs                      程序入口
```

## 文档资料

- `Document/md/项目架构总览.png`：架构图
- `Document/Melsec/`：3E / 4E / 1E 报文、控制命令截图；`寄存器地址.png` 为《基本型 QCPU 软元件一览》（3E / 4E 共用 28 种软元件，含进制列），`1E寄存器地址.png` 为 A 兼容 1E / FX 系列的 10 种软元件与编号范围（另两个线圈代码 `TC` / `CC` 取自 MC 协议手册 442 页，图中未列）
- `Document/Siemens/`：两次握手、读写报文、区域类型、功能编码、ROSCTR、PUD 类型截图
- `Document/Inovance/Modbus.doc`：Modbus 协议资料
- `Document/Inovance/Easy寄存器地址.png`、`H3U寄存器地址.png`：两机型的 Modbus 地址映射（线圈区 / 寄存器区），均为官方手册页面的截图，原页见下表

### 官方手册索引

软元件代码、位/字能力、编号范围与 Modbus 编址，**以下面这些手册为准**，截图只作速查。
`InovanceDeviceCatalog`、`Melsec3E4EDeviceCatalog`、`Melsec1EDeviceCatalog` 的每一项都应能在这里找到出处。

| 手册 | 文件 | 关键页 |
| --- | --- | --- |
| H3U / H3S 指令及编程手册（753 页） | `19010394-SC_A20（…H3U H3S系列…）.pdf` | **30** 3.1 软元件一览表（全部范围）<br>**33** 3.5 定时器（6 类共 512 点）<br>**571–573** 9.4.2 功能码与帧格式（含数量上限 2000 / 125 / 123）<br>**574** 9.4.3 变量编址（线圈编址 + 寄存器编址）<br>**576** 9.4.3 Modbus 通信地址（首地址 + 变量序号）<br>**680** 9.7 以太网 Modbus 通信地址 |
| H5U & Easy 编程手册（1036 页） | `19011157-SC_A21_H5U&Easy系列…pdf` | **110–111** 6.2 软元件（位 / 字）<br>**419** 9.x 串口 Modbus 从站地址<br>**435** 10.3.3 以太网 Modbus 从站地址<br>**995–997** 附录 功能码详解 |
| MELSEC-Q/L 通讯协议参考手册（518 页） | `三菱QL系列MC协议.pdf`（CJK CID 编码，需用 `pdf-cjk-cid-text` skill 提取） | **125–127** 3E/4E 软元件范围（基本型 / 高性能型+过程+冗余+QnA / 通用型+LCPU 三套）<br>**436–437** A 兼容 1E 帧报文示例（编号 → 软元件名 → 点数）<br>**442–444** 1E 软元件代码表（含 TC 54H,43H、CC 43H,43H） |

两条从手册里查证、容易踩错的结论：

- **H3U 的 `T256–T511` 只有触点，没有当前值**：线圈编址 `T0~T511 @0xF000`（512 点），寄存器编址只到 `T0~T255 @0xF000`（256 点）。手册 574 页与 680 页两处口径一致，是官方设计而非遗漏。故 `TS` 上限 511、`TN` 上限 255。
- **Easy 编程支持 `W0~W32767`，但 Modbus 不映射 W**（435 页原文：不支持 W 元件、不支持 Pointer 变量）。故 `InovanceDeviceCatalog` 中 Easy 不含 W 区。

三菱侧同样两条：

- **3E / 4E 的 `R` 是 10 进制、`ZR` 才是 16 进制**（125 页「表示」列），注释里曾把 `R` 写成 16 进制。
- **1E 帧的 `TC` / `CC` 是独立软元件代码**（442 页：`TC` = 54H,43H 定时器线圈、`CC` = 43H,43H 计数器线圈），与 `TS`/`CS`（触点）、`TN`/`CN`（当前值）并列，六个都要建。

## 冒烟验证

`Tests/PlcTest.SmokeTest` 是一个不依赖测试框架的控制台工程（不属于 `PlcTest.slnx`，随便删）：

```powershell
cd Tests/PlcTest.SmokeTest
dotnet build
bin/Debug/net472/PlcTest.SmokeTest.exe
```

覆盖客户端侧的协议帧长推断（3E/4E Binary、3E ASCII、1E Binary/ASCII、Modbus TCP/RTU、西门子 TPKT）、
**服务端端到端往返**（8 种协议：客户端组包 → 服务端解析 → 服务端响应 → 客户端解析，并校验各协议帧长判定 = 实际请求长度）、
位/字门禁与地址归一化、各协议暴露的软元件区域列表、三菱 3E / 4E / 1E 软元件表逐项核对、汇川 Modbus 软元件表与机型差异、编号范围校验、Builder 厂商校验、`Result.ErrorKind` 分类。全部通过时退出码为 0。

## 待办

- [ ] 欧姆龙 FINS TCP：补齐协议编解码与客户端 8 个读写
- [ ] 西门子 S7：DB 区读写与 M0.3 这类位偏移（`PlcAddress` 目前没有位序号字段）
- [ ] 服务端补齐其他厂商（西门子、欧姆龙、汇川）
- [ ] 服务端帧定界：`TcpServerCommunication.ReadFrame` / `SerialServerCommunication.ReadFrame` 仍写死 3E Binary 的 9 字节头 + `[7..8]` 长度，4E / ASCII / 1E 会读错（客户端侧已改为协议驱动帧长）
- [ ] 汇川 32 位计数器的界面展示：协议层已返回每点两个 16 位寄存器，但表格仍按「一行一个寄存器」渲染，`C200` 以上会显示成两行（低 16 位 / 高 16 位），尚未合并成 32 位值；清零按钮也需按 2 倍长度生成数据
- [ ] 汇川 T256–T511 只支持触点：官方手册（9.4.3 串行 Modbus、9.7 以太网 Modbus 两处口径一致）的寄存器编址只到 `T0~T255`，`T256~T511`（1ms 定时器）只有线圈地址、没有当前值地址。当前 `TN` 上限设 255、`TS` 上限设 511 与手册一致，属于官方限制而非实现缺陷
- [ ] 汇川 M 区分段：H3U 的 M 是 `M0–M7679` + `M8000–M8511`，中间 7680–7999 是空洞；`GetMaxIndex` 只能表达单一下限，目前按 8511 放行，空洞内地址不拦
- [ ] 界面写入时位/字分支已按协议判断，但读区下拉仍只渲染一种表格形态（位元件显示 16 位列、字元件显示位+值）；三菱 M 这类"既能位又能字"的区域目前固定按位展示，需要时再加位/字切换
- [ ] 3E / 4E 的软元件范围按**基本型 QCPU**建模（`D` 到 11135、`X` 到 0x7FF）。官方手册还给出「高性能型 / 过程 / 冗余 / QnA CPU」（第 126 页，`D` 到 12287、`X` 到 0x1FFF）和「通用型 QCPU / LCPU」（第 127 页）两套更大的范围；若实际连这些机型，超出的合法地址会被误拦（不会读错，只是保守拒绝）
- [ ] `btn_Trigger` 触发按钮还没有事件实现
- [ ] 界面提示语英文化 → 改中文
- [ ] 用真实 PLC / 客户端做一次真机联调（本轮客户端改造只做了编译 + 冒烟验证）
