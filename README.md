# PLCClient

个人开发的一款 PLC 客户端以及服务端测试工具。旨在将工作中常用的 PLC 全部实现模拟通讯。缓慢开发中……

| 项目 | 说明 |
| --- | --- |
| 工程文件 | `PlcTest.csproj`（老式非 SDK 工程，程序集清单手工维护） |
| 程序集 / 根命名空间 | `PlcTest` |
| 运行环境 | .NET Framework 4.7.2 · WinForms（`OutputType=WinExe`） |
| 第三方依赖 | 无，只用 BCL |
| 解决方案 | `PlcTest.slnx` |

## 目录

- [软件架构](#软件架构) — 分层、通讯抽象、贯穿全项目的写法
- [协议支持](#协议支持) — 进度表与各厂商实现要点
- [统一地址模型](#统一地址模型)
- [协议选项](#协议选项) — 机型、字节序、机架槽号等私有参数
- [界面](#界面)
- [快速开始](#快速开始)
- [目录结构](#目录结构)
- [文档资料](#文档资料) — 含官方手册索引与查证结论
- [冒烟验证](#冒烟验证)
- [待办](#待办) — 待做 / 暂缓 / 已完成归档

## 软件架构

![项目架构总览](./Document/md/项目架构总览.png)

### 分层与依赖

整体分四层：**`Models`（模型）→ `Protocols` / `Tool`（协议知识与工具）→ `Client` / `Server`（通讯层）→ `View`（界面）**，依赖严格单向：

- `Models` 只 `using System.*`，是叶节点；
- `Protocols`、`Tool` 只向下依赖 `Models`；
- `Client` 与 `Server` **互不依赖**（服务端模拟软元件时查的是 `Protocols` 里的软元件表，不反向引用客户端）；
- 只有 `View` 同时依赖 `Client` / `Server`。

### 通讯与协议抽象

通讯层把「怎么发数据」和「数据怎么编解码」拆成两条互不依赖的抽象，任一侧换实现都不影响另一侧：

| 层次 | 接口 | 现有实现 | 职责 |
| --- | --- | --- | --- |
| 客户端 · 通讯方式 | `IClientCommunication` | `TcpClientCommunication`、`SerialPortClientCommunication` | 建连、收发原始字节（收发串行化；按协议给出的帧长读满一帧） |
| 客户端 · 通讯协议 | `IPlcClientProtocol` | `ClientProtocolManagement\` 下各厂商协议 | 组装请求报文、解析响应报文、声明区域访问能力与应答帧长 |
| 客户端 · 可选能力 | `IDataFormatConfigurable` / `IOmronFinsClientProtocol` / `IS7SessionProtocol` / `IProtocolOptionHost` | 只有需要的协议实现 | 32 位排列、FINS 节点握手、S7 两段握手、接收界面传来的私有选项 |
| 客户端 · S7 扩展 | `IS7MultiVarProtocol` / `IFrameSegmentProtocol` | `SiemensS71200ClientProtocol` | 一条请求读多个不连续地址、单帧装不下时拆帧 |
| 客户端 · 设备 | `IPlcClient` | `CommunicationPlcClient`（单一实现，构造注入通讯与协议），`OmronPlcClient` / `S7PlcClient` 只重写 `Connect()` 插入握手 | 连接、心跳与 8 个读写 |
| 服务端 · 通讯方式 | `IServerCommunication` | `TcpServerCommunication`、`SerialServerCommunication` | 监听端口、收发字节，每个连接用 `IClientSession` 标识 |
| 服务端 · 通讯协议 | `IServerProtocol` / `IModbusServerProtocol` / `IOmronFinsServerProtocol` / `IS7ServerProtocol` | 三菱 6 个（1E/3E/4E × Binary/ASCII）+ 汇川 2 个（Modbus TCP/RTU 从站）+ 欧姆龙 FINS + 西门子 S7 | **帧定界**、解析请求、构建响应（含结束码 / 异常码 / S7 数据项错误）、声明支持的软元件区域 |
| 服务端 · 设备 | `IPlcServer` + `PlcServerBase` | `MelsecPlcServer`、`InovancePlcServer`、`OmronFinsServer`、`S7PlcServer` | 回应读写请求。**启停、客户端计数、通讯日志、收帧异常兜底都在 `PlcServerBase`**，子类只实现 `HandleFrame` 与寄存器存储 |
| 共享 · 软元件表 | — | `Protocols/` 下 4 份 catalog（三菱 3E4E / 三菱 1E / 汇川 / 欧姆龙）+ `DeviceAreaInfoProvider` | 软元件代码、编号范围、位/字能力、名称与 Modbus/FINS 编址的**唯一真相**，Client 与 Server 都向下查 |
| 服务端 · 寄存器表 | `IRegisterTable` | 上述四个服务端 | 模拟寄存器的增删查改与清零 |

**服务端协议为什么分成四族接口而不是一个基接口**：`IServerProtocol`（MC 的结束码）/ `IModbusServerProtocol`（功能码 + 异常码）/ `IOmronFinsServerProtocol`（FINS 结束代码）/ `IS7ServerProtocol`（S7 头的错误类/错误码 + 按项的数据项）响应结构本质不同，硬塞进一个基接口只会到处判空。所以 `PlcServerBuilder` 的协议字段是 `object`，`Build()` 用 `is` 分派——这是刻意的，与客户端「一个 `IPlcClientProtocol` 通吃」的情况不同。

### 贯穿全项目的写法

- **接口抽象**：上表每一行两端都靠接口隔离，厂商代码只碰接口不碰具体类。
- **两个 Builder 结构完全同构，协议分派只有一个入口**：`UseProtocol(ProtocolType)` + 无参 `Build()`。两端都是「两个字段（通讯方式 + 已选协议）+ 一行赋值的 `UseXxx` + `Build` 里按**接口**分派」：

  | | 字段 | `Build()` 的分派 |
  | --- | --- | --- |
  | `PlcClientBuilder` | `IClientCommunication` + `IPlcClientProtocol` | `IOmronFinsClientProtocol` → `OmronPlcClient`；`IS7SessionProtocol` → `S7PlcClient`；其余 → `CommunicationPlcClient` |
  | `PlcServerBuilder` | `IServerCommunication` + `object` | `IS7ServerProtocol` → `S7PlcServer`；`IOmronFinsServerProtocol` → `OmronFinsServer`；`IModbusServerProtocol` → `InovancePlcServer`（带机型）；`IServerProtocol` → `MelsecPlcServer` |

  判断依据是**接口/能力**，不是厂商枚举：新增一个需要特殊客户端的厂商，只要它的协议实现对应接口即可。窗体只传一个枚举，新增协议改 3 处（枚举 + Builder 的 case + 软元件表）。
- **组合收敛**：`CommunicationPlcClient` 用同一个类承担连接、断开、心跳重连与 8 个读写的「拼包 → 发送 → 解析」骨架；通讯对象与协议实现由构造函数注入。欧姆龙与西门子只重写 `Connect()` 插入各自握手，不复制 8 个方法。
- **帧定界归协议**：`IPlcClientProtocol.GetResponseFrameLength` / `IServerProtocol.GetRequestFrameLength`（及 Modbus / FINS / S7 的对应成员）决定「这一帧多长」。判不出时返回 -1，才回退到「空闲 60 ms 判据」。这套「凑齐一帧」的逻辑只有一份：`Client/ClientCommunication/ResponseFrameAssembler.cs`，TCP 与串口各自只实现 `IFrameTransport` 三个成员。
- **位/字只有一处真相**：能不能按位/按字访问由协议声明（`IPlcClientProtocol.GetAreaAccess`），客户端按方法语义（`ReadBit`/`ReadWord`）归一化地址标志，不再维护「D 为字、其余为位」这类客户端侧规则。
- **编号上限归协议、越界判断归客户端**：`GetMaxIndex` 只声明「这个区域最大编号多少」（-1 表示不限制），起始点与连续访问末点的越界计算统一在 `CommunicationPlcClient.CheckAddress` 里做，越界返回 `ErrorKind.Validation`，不发给 PLC。
- **软元件表集中**：四种厂商的软元件代码、位/字能力、编号进制与默认范围都收在 `Protocols/` 的 catalog 里，客户端与服务端查同一份。客户端认得的软元件，服务端就模拟，不会出现「客户端能发、服务端装不认识」。
- **界面跟着协议走**：`GetSupportedAreas()` 返回当前协议真正支持的区域，两个工作台的软元件列表、三个下拉框、位/字视图形态都由它驱动，**PLC 上不存在的软元件不会出现在选项里**。
- **协议私有选项统一通道**：见 [协议选项](#协议选项)，不再用 `as 具体协议类` 强制转换来设参数。
- **错误可分类**：`Result<T>` 带 `ErrorKind`（Validation / Transport / Protocol / DeviceEndCode）与 `EndCode`，调用方不必解析 `ErrorMsg` 字符串。
- **依赖注入 + 模板方法**：构造函数都接收通讯接口与协议接口；`TemplateClientProtocol` / `TemplatePlcClient` 是留白的骨架，新增厂商时复制这两个文件往里填。
- **心跳保活**：`Connect()` 同步建连并返回 `Result`（失败原因直接可见），成功后才起后台心跳线程，1 s 一轮，断线自动重连（`CommunicationPlcClient.HeartBeatLoop`）。

## 协议支持

客户端全部通过 `PlcClientBuilder` 选择协议：

| 厂商 | 协议 | 建造者方法 | 协议实现 | 客户端封装 | 服务端模拟 |
| --- | --- | --- | --- | --- | --- |
| 三菱 | MC 1E ASCII | `UseMelsecMc1EAscii()` | ✅ | ✅ | ✅ |
| 三菱 | MC 1E Binary | `UseMelsecMc1EBinary()` | ✅ | ✅ | ✅ |
| 三菱 | MC 3E ASCII | `UseMelsecMc3EAscii()` | ✅ | ✅ | ✅ |
| 三菱 | MC 3E Binary | `UseMelsecMc3EBinary()` | ✅ | ✅ | ✅ |
| 三菱 | MC 4E ASCII | `UseMelsecMc4EAscii()` | ✅ | ✅ | ✅ |
| 三菱 | MC 4E Binary | `UseMelsecMc4EBinary()` | ✅ | ✅ | ✅ |
| 西门子 | S7-1200（S7comm） | `UseSiemensS71200()` | ✅ 待真机验证 | ✅ | ✅ I/Q/M/DB |
| 欧姆龙 | FINS TCP | `UseOmronFinsTcp()` | ✅ | ✅ | ✅ |
| 汇川 | Modbus TCP | `UseInovanceModbusTcp()` | ✅ | ✅ | ✅ |
| 汇川 | Modbus RTU | `UseInovanceModbusRtu()` | ✅ | ✅ | ✅ |

**客户端与服务端都可选 10 种**。串口能力全部关闭后，原本只跑串口的 1E 两种与 Modbus RTU 改成「帧直接跑在 TCP 上」（帧格式与串口一致，不是 Modbus TCP 的 MBAP），因此仍然可选。重新打开串口只需在 `ProtocolType` 里给对应标签补回 `clientSerial` / `serverSerial`，下拉框会自动恢复，TCP 与串口可并存。

> 「协议实现 ✅」指报文编解码已写完并有逐字节断言，不等于真机验证过。**西门子**目前只到「按公开报文样本核对」，连 S7-1200 前记得在 TIA 里打开「允许 PUT/GET 通信访问」，DB 块关掉「优化的块访问」。

### 三菱 MC（6 种）

- **3E / 4E 共用一张表**：`Melsec3E4EDeviceCatalog`（28 种，QnA 兼容 3E / 4E 帧共用，两者只有帧头不同）是三菱软元件代码、位/字能力、**编号进制**与默认范围的唯一来源（依据《基本型 QCPU 软元件一览》）。四个协议全部查表，不再各写一份 28 个 `case`。ASCII 地址按「X/Y/B/W/SB/SW/DX/DY/ZR 十六进制，其余十进制」格式化。
- **1E 单独一张表**：A 兼容 1E 帧（FX 系列）与 3E 规则不同——单字符代码补**空格**（0x20）而非 `*`、编号统一十六进制、只支持 12 种软元件（D / R / X / Y / M / S / TN / TS / TC / CN / CS / CC），故单列 `Melsec1EDeviceCatalog`（带机型概念 `Melsec1ECpuModel`，默认 FX3G/FX3GC）。报文顺序已按手册 436 页示例核实：**起始编号 → 软元件名 → 点数**。
- **1E 响应头只有 2 字节**：副头部(1，`0x80 | 指令码`，字读即 `0x81`) + 结束代码(1)。结束代码为 `0x5B` 时后面另跟 2 字节错误信息（`80 5B 12 00`）、且没有数据区。与 3E/4E 的「9 字节头 + 2 字节结束码」完全不同，按 3 字节头算会多等 1 字节，表现为「读取 PLC 响应超时，帧未收全」。
- **`R` 是十进制、`ZR` 才是十六进制**（手册 125 页「表示」列），注释里曾把 `R` 写成十六进制。
- **1E 的 `TC` / `CC` 是独立软元件代码**（手册 442 页：`TC` = 54H,43H 定时器线圈、`CC` = 43H,43H 计数器线圈），与 `TS`/`CS`（触点）、`TN`/`CN`（当前值）并列，六个都要建。
- **服务端不区分子类**：六种协议共用一个 `MelsecPlcServer`（`0x0401` 批量读、`0x1401` 批量写），帧格式差异全部由注入的 `IServerProtocol` 承担；设备码 → 区域走 `MelsecServerDeviceMapper`，查的是客户端同一份 catalog。

### 汇川 Modbus（2 种）

- **一张表统管**：`InovanceDeviceCatalog` 管起始地址、地址空间（线圈 01/05 与保持寄存器 03/06/16）、**地址分段**与各机型范围（`InovanceCpuModel`，默认 **H3U**）。Easy 与 H3U 的 S/M/X/Y/D/R 基地址完全一致，差异只在范围与机型独有区：**Easy 有 B 区，H3U 有 SM/SD/T/C**。线圈区只能按位、寄存器区只能按字——与三菱「位软元件也能按字读」完全不同。
- **32 位计数器按段处理**：`CN` 分两段——`C0–C199` 在 0xF400（16 位，每点 1 个寄存器）、`C200–C255` 在 0xF700（32 位，**每点 2 个寄存器**）。地址按段换算，寄存器数量 = 点数 × 每点寄存器数，因此 32 位段永远不会落到 0x06（32 位寄存器不支持单写）；跨段访问（如 `C195` 起 10 点）直接拒绝，不发出必然错位的报文。
- **数量上限前置校验**：`InovanceModbusLimits` 记录单帧上限（读线圈 2000 / 读寄存器 125 / 写线圈 1968 / 写寄存器 123，手册 9.4.2），协议在拼包前按**换算后的寄存器数**校验，超限直接报「一次最多读 125 个寄存器，当前请求 200 个」，不用等 PLC 回一个看不懂的异常码。注意写比读少 2，且 32 位段的点数要先乘 2 再判。
- **服务端**：`InovancePlcServer` 按功能码 **01 / 03 / 05 / 06 / 0F / 10** 处理，内部是两张按 Modbus 地址索引的表（线圈位、保持寄存器）；地址映射复用客户端同一份 catalog，因此 32 位段在服务端同样成立。异常按规范返回 `功能码 | 0x80` + 异常码（01 非法功能 / 02 非法地址 / 03 非法数据值）。

### 欧姆龙 FINS（1 种）

- **两层套娃**：`OmronFinsTcpClientProtocol` 负责外层 FINS/TCP 外壳（16 字节 `FINS` + 长度 + 命令 2 + 错误码）与内层 FINS 帧（10 字节头 + 指令代码 + 结束代码）。
- **建连后必须先做节点地址握手**（20 字节请求 → 24 字节响应，取 `[19]`/`[23]` 作为 SA1/DA1）。`OmronPlcClient` 因此不重复实现 8 个读写，而是继承 `CommunicationPlcClient`、只重写 `Connect()` 插入握手——否则 PLC 会静默丢弃后续命令，表现为「连上了但读不到数据」。
- **区码表集中**：`OmronFinsDeviceCatalog`，位码与字码分开（如 DM 位 `02` / 字 `82`）。服务端 `OmronFinsServer` 用 `Resolve(区码, 字地址)` 反向查表——客户端是「区域 → 区码」，服务端正好反过来，两个特例（TIM/CNT 共用区码靠 `0x8000` 区分、EM 区码随 bank 变化）都在同一张表里闭合。
- **服务端失败按 FINS 结束代码返回**：`0x1101` 区域代码不正确、`0x1103` 地址溢出、`0x1104` 长度不符；响应的 FINS 帧头会把源/目标地址互换、`ICF` 置 `0xC0`。

### 西门子 S7（1 种）

- **两段握手单独成接口**：`IS7SessionProtocol`（COTP 连接 `0xE0` + Setup Communication `0xF0`，顺便带回协商的 PDU 长度）。与 `IOmronFinsClientProtocol` 同一套路子——`Build()` 只看协议实现了哪个接口来挑客户端，不按厂商枚举配对。
- **响应头比请求头多 2 字节**：`ROSCTR=0x03`（Ack-Data）的 S7 头是 12 字节，末尾两个字节是 [错误类][错误码]，参数区因此从偏移 **19** 开始、数据项从 **21** 开始。按请求的 10 字节头（偏移 17）去读，会把错误类当成功能码、把功能码当成数据项返回码——**真机上读写会全部解析失败**。
- **握手结果必须逐层校验**：COTP 要检查 PDU 类型 `0xD0`（收到 `0x80` 是 DR，多为机架/槽号错或连接占满）、目标引用回显；Setup 要检查响应头错误类/错误码（`0x81` 应用关系、`0x83` 资源不足）。不校验就会「握手假成功、第一次读才报错」。
- **位数据按位打包**：读响应的 `TS=0x03(BIT)` 数据项长度填**位数**，数据按位压缩（50 位只占 7 字节，字节内低→高）；写的位数据同理只发 `ceil(点数/8)` 字节。旧实现按「1 点 1 字节」收发，而测试替身也按同样错法拼包，属于**自洽假通过**；2026-11-10 已按标准改掉，真机上仍需再确认。
- **长度字段按「变量类型 × 数量」解释**：Data Item 的 `0x03(BIT)` / `0x04(BYTE/WORD/DWORD)` 按**位**算（4 字节写 = `0x0020`）；Request Item 的 Count 单位跟着 Variable Type 走——`0x02(BYTE)` 时是字节数，字读 N 点要填 `N×2`。
- **地址是位偏移**：帧里填 `字节地址 × 8`，所以 `PlcAddress.Address` 是**字节偏移**（不是字编号）。`DB1.DBW0` 已支持（块号走 `PlcAddress.DbNumber`）；位偏移（`M0.3` / `DB1.DBX0.3`）暂不支持，见待办。
- **DB 区按块号惰性分配**：`Dictionary<int, byte[]>`，只有访问过的块才占内存；`DB1.DBW0` 与 `DB2.DBW0` 是两块独立内存（照某些实现让四个区共用一块会让多 DB 串数据）。
- **多变量读写**：参数区是 `2 + 12 × N`（功能码 + 项数 + N 个 ANY 指针），**每项独立成败**——越界项只回 `0x05`，同帧其他项照常带数据；整条作业不成立（项数为 0、区码不认识、报文截断）才走 S7 头错误。客户端侧是可选接口 `IS7MultiVarProtocol`，不进公共 `IPlcClientProtocol`（三菱/汇川/欧姆龙的批量是「同区连续」，用不上这个签名）。
- **单帧装不下时自动拆帧**：可选接口 `IFrameSegmentProtocol`，`CommunicationPlcClient` 的四个数组方法按协议给的分段计划逐帧收发再拼接。按协商 PDU（默认 960）算：读 467 字 / 7480 位，写 462 字 / 7400 位。**地址推进规则归协议**——字按 `×2` 字节推进，位段必须落在字节边界。

### 服务端的公共约定

- **「清空地址」与「清零值」是两件事**：`ClearRegisters` 移除地址本身（`GetRegisterCount` 变 0，界面表格随之清空）；`ResetRegisterValues` / `ResetAllRegisterValues` 只把值改回 0，**地址集合不变**。界面上的「清零区间 / 全部清零」走后者，「移除区域」走前者。三菱与欧姆龙原先把 `ResetAllRegisterValues` 实现成 `ClearRegisters`，点「ClearAll」后整个表格会消失；汇川的 `CN` 有两段地址，按基地址清会漏掉 32 位段（`C200–C255` @ `0xF700`），现在按 catalog 的地址段逐段清。
- **位与字是否联动，三家不同**：三菱/汇川/欧姆龙每条区域各存一张位表与一张字表，**同一区域既能按位又能按字时两张表互不联动**（如三菱 M）——这是模拟器为了简单做的取舍，不是真实 PLC 的行为。西门子必须联动：`MB100` / `MW100` / `M100.0` 是同一片字节内存的不同视图，所以 `S7PlcServer` 用字节数组存储，位读写走掩码。
- **越界一律明确报错**：汇川回异常码 02、欧姆龙回 `0x1103`、S7 回数据项 `0x05`。不做「越界读返回全 0 且报成功」——那样客户端拿到半真半假的数据，还以为 PLC 里就是 0。
- **帧定界**：`IServerProtocol.GetRequestFrameLength` / `IModbusServerProtocol.GetRequestFrameLength` / `IS7ServerProtocol.GetRequestFrameLength`（TPKT 长度字段）决定「这一帧读多长」，通讯层不再写死 3E Binary 的 9 字节头；返回 -1（如 MC 1E 的写请求）时回退到 60 ms 空闲判据。

## 统一地址模型

各厂商共用 `Models\PlcAddress.cs`，屏蔽「三菱的 D100」「西门子的 DB1.DBW0」这类差异：

```csharp
// 字地址：D100，长度 1
PlcAddress.Word(MemoryArea.D, 100);

// 位地址：M10
PlcAddress.Bit(MemoryArea.M, 10);

// 西门子 DB 块，10 个字（DB1.DBW0 起）
PlcAddress.Word(MemoryArea.DB, 0, dbNumber: 1, length: 10);
```

- `PlcAddress` 只携带 `Area` / `Address` / `Length` / `IsBit` / `DbNumber` 五个字段，位与字的区别由 `IsBit` 表达。
- **`Address` 的单位随协议而定**：三菱/汇川/欧姆龙是软元件编号，西门子是**字节偏移**（`MW100` 与 `M100.0` 落在同一字节）。分段、显示、越界判断都要按协议口径处理。
- `MemoryArea` 共 **33 项**：三菱的 `SM` `SD` `X` `Y` `M` `L` `F` `V` `B` `D` `W` `TS` `TC` `TN` `SS` `SC` `SN` `CS` `CC` `CN` `SB` `SW` `S` `DX` `DY` `Z` `R` `ZR`，欧姆龙专用的 `CIO` `HR` `AR` `EM`（EM 的存储体走 `DbNumber`），以及西门子专用的 `DB`（块号同样走 `DbNumber`）。
- 读写统一返回 `Result<T>`（`IsSuccess` / `Data` / `ErrorMsg`），失败不抛异常。

## 协议选项

机型、字节序、机架/槽号这类「厂商私有参数」走同一条通道，不再靠 `as 具体协议类` 强制转换：

```
ProtocolOption（描述：键 / 显示名 / 下拉或整数 / 默认值 / 范围 / 提示）
        ↓ ProtocolOptionCatalog.ForClient/ForServer(protocol) 给出该协议有哪些项
ConnectSettingsForm 按描述动态生成控件
        ↓
ProtocolOptions（键值袋）→ Builder.UseOptions(options) → IProtocolOptionHost.ApplyOptions()
```

| 协议 | 可配置项 |
| --- | --- |
| 汇川（TCP/RTU） | `CpuModel`（H3U / Easy）、`DataFormat`（ABCD/BADC/CDAB/DCBA，默认 CDAB） |
| 汇川服务端 | `CpuModel` |
| 欧姆龙 | `DataFormat`、`TargetNetwork`（DNa）、`TargetUnit`（Da2） |
| 西门子 | `Rack`（0–7）、`Slot`（0–31）、`PduLength`（240–960，默认 960） |
| 三菱 3E / 4E | `NetworkNumber`、`StationNumber`（写进帧头；1E 帧没有这两个字段，故不提供） |

- **用接口而不是反射设属性**：反射要求键名与字段名一致，改名时编译期发现不了、失效也静默。每个协议只认自己那几个键，多余键一律忽略。
- **选项在 `Build()` 里应用**，所以 `UseOptions` 与 `UseProtocol` 的先后顺序无关。
- **值越界会夹到协议范围**（集中在新 `Tool/ProtocolOptionValues.cs`），不会把非法的 TSAP 或网络号写进帧。
- 工作台的状态条会显示当前生效的选项（如「机型=Easy · 字节序=CDAB」），连接对话框关掉之后仍能确认。

## 界面

| 窗体 | 功能 |
| --- | --- |
| `MainForm` | 客户端 / 服务端连接参数、连接状态指示灯、打开工作台；连接客户端与开启服务端时都会弹出「通讯方式 + 协议 + 协议选项」对话框 |
| `ConnectSettingsForm` | 选通信方式与协议。**当前只提供 TCP**（串口标志全为 false）。客户端与服务端都列 10 种，列表由 `ProtocolInfo` 能力位自动生成；协议有私有参数时下方「协议选项」组按 `ProtocolOptionCatalog` 动态生成，没有就整块隐藏 |
| `PlcWorkspaceForm` | 客户端调试工作台：顶部状态条（协议 · 连接状态 · 软元件数 · 生效选项）、左侧软元件列表、右侧「监视 / 单次读写」两个页签、底部报文视图。地址栏以**字符串为主输入**（`M100`、`DB1.DBW0`），区域下拉与偏移框双向同步 |
| `PlcServerWorkspaceForm` | 服务端调试工作台：多一条地址栏，「模拟寄存器」页带初始化区间 / 清零区间 / 全部清零 / 移除区域 / 随机填充，「单次读写」页用于自测 |

界面细节：

- **监视列表**（`MonitorGrid`）：一行一个地址，列 = 地址 / 方式 / 长度 / 值 / 状态，**位与字可以混排**；后台周期读取，非法地址在状态列标红。
- **报文视图**（`FrameLogView`）：TX / RX 分色、HEX 显示、暂停 / 清空 / 复制 / 自动滚动，超过行数上限从头部裁剪。
- **四个窗体都是固定尺寸**（`FixedSingle` / `Fixed3D` / `FixedDialog`），禁用最大化与最小化。
- 五个窗体/控件（两个工作台、`AddressInputPanel`、`MonitorGrid`、`FrameLogView`）都可用 VS 设计器打开；`ConnectSettingsForm` 因选项行随协议变化，仍是代码布局。
- 旧的两个 `PlcClientForm` / `PlcServerForm`（21 列固定表 + DataView/DeviceView/LogView 三页签）已删除，由上述工作台取代。
- 界面文案目前是英文（`Please Check ...`），见待办。

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
using PlcTest.Models;                          // PlcAddress / MemoryArea / ProtocolOptions
using PlcTest.Client;                          // PlcClientBuilder
using PlcTest.Client.ClientInterface;          // IPlcClient

var client = new PlcClientBuilder()
    .UseTcp("192.168.1.10", 5000)
    .UseMelsecMc3EBinary()
    .Build();

client.ShowCommMessageEvent += msg => Console.WriteLine(msg);   // 交互报文日志
client.FrameEvent += frame => Console.WriteLine(frame.ToLogText());  // 结构化帧（含方向与原始字节）

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

需要设厂商参数时在 `Build()` 前加一行 `UseOptions`：

```csharp
using PlcTest.Models;      // ProtocolOptions / DataFormat
using PlcTest.Protocols;   // InovanceCpuModel
using PlcTest.Client;      // PlcClientBuilder

var client = new PlcClientBuilder()
    .UseTcp("192.168.1.10", 502)
    .UseInovanceModbusTcp()
    .UseOptions(new ProtocolOptions()
        .Set("CpuModel", InovanceCpuModel.Easy)
        .Set("DataFormat", DataFormat.BADC))
    .Build();
```

串口通讯把 `UseTcp(...)` 换成 `UseSerial("COM3", 9600, Parity.None, 8, StopBits.One)` 即可，协议那一行不动。

> 注意：**串口链路尚未实测**，因此 `ProtocolType` 里所有标签的 `clientSerial` / `serverSerial` 目前都是 `false`，界面不会给出串口入口。1E 与 Modbus RTU 暂以「同样的帧 + TCP 传输」提供（1E 读命令按 12 字节定长，写命令回退 60 ms 空闲判据），不是 Modbus TCP 的 MBAP。API 层（`UseSerial` + 串口协议）不受影响，改标志位即可恢复。

服务端用对称的 `PlcServerBuilder` 起步：

```csharp
using PlcTest.Server;
using PlcTest.Server.ServerInterface;

IPlcServer server = new PlcServerBuilder()
    .UseTcp("127.0.0.1", 5000)
    .UseMelsecMc3EBinary()
    .Build();
server.OpenServer();   // 之后客户端即可按 MC 3E Binary 连入并读写模拟寄存器

// 汇川 Modbus 从站（TCP / RTU 二选一，端口按 Modbus 惯例 502）
IPlcServer modbusServer = new PlcServerBuilder()
    .UseTcp("127.0.0.1", 502)
    .UseInovanceModbusTcp()
    .Build();
modbusServer.OpenServer();
```

## 目录结构

```
PLCClient/
├─ Protocols/                      软元件表 + 协议元数据（只依赖 Models）
│  ├─ Melsec3E4EDeviceCatalog.cs   3E/4E 共用：28 种软元件、进制、编号范围
│  ├─ Melsec1EDeviceCatalog.cs     1E（FX 系列）：12 种，含 FX3G/FX3U 上限
│  ├─ InovanceDeviceCatalog.cs     汇川：起始地址、地址分段、H3U/Easy 范围
│  ├─ OmronFinsDeviceCatalog.cs    欧姆龙：位码/字码、EM bank、TIM/CNT 偏移
│  ├─ ProtocolOptionCatalog.cs     协议私有选项的登记表
│  └─ DeviceAreaInfoProvider.cs    协议 + 区域 → 描述/位字能力（界面软元件列表用）
├─ Models/                         统一模型（叶节点，不引用任何上层）
│  ├─ PlcAddress.cs                统一 PLC 地址模型
│  ├─ Result.cs                    统一返回结果（含 ErrorKind / EndCode）
│  ├─ RequestInfo.cs               服务端解析出的 MC 请求结构
│  ├─ DeviceAreaInfo.cs            软元件视图的一行：区域 / 描述 / 位 / 字 / 备注
│  ├─ CommFrame.cs                 结构化帧（方向 / 时间 / 原始字节 / 说明）
│  ├─ ProtocolOption.cs            选项描述 + 选项值集（ProtocolOptions）
│  ├─ DataFormat.cs                32 位数据的 ABCD / BADC / CDAB / DCBA
│  └─ Enums.cs                     MemoryArea / PlcType / CommunicationType / ProtocolType(+ProtocolInfo)
├─ Client/
│  ├─ ClientInterface/             IClientCommunication / IPlcClient / IPlcClientProtocol /
│  │                               IDataFormatConfigurable / IOmronFinsClientProtocol / IS7SessionProtocol /
│  │                               IS7MultiVarProtocol / IFrameSegmentProtocol / IProtocolOptionHost
│  ├─ ClientCommunication/         TCP、串口通讯实现（内部串行化）+ ResponseFrameAssembler（共用的帧组装）
│  ├─ ClientProtocolManagement/    各厂商协议实现
│  │  ├─ Melsec/                   1E/3E/4E × ASCII/Binary
│  │  ├─ Siemens/                  S7-1200（COTP + Setup + 读写 + 多变量 + 分段）
│  │  ├─ Omron/                    FINS TCP（含节点地址握手）
│  │  ├─ Inovance/                 Modbus RTU / TCP（含 32 位段与数量上限）
│  │  └─ Template/                 新协议模板
│  ├─ PlcClient/                   CommunicationPlcClient（组合式客户端）
│  │                               + Omron/S7 两个只重写 Connect 的子类 + Template 骨架
│  └─ PlcClientBuilder.cs          客户端建造者（通讯方式 + 协议 + 选项）
├─ Server/
│  ├─ ServerInterface/             IPlcServer / IRegisterTable / IServerCommunication / IClientSession /
│  │                               IServerProtocol / IModbusServerProtocol / IOmronFinsServerProtocol /
│  │                               IS7ServerProtocol / S7RequestInfo
│  ├─ ServerCommunication/         TCP 监听、串口（按协议帧长收帧）
│  ├─ ServerProtocolManagement/    Melsec / Inovance / Omron / Siemens 四族协议
│  │                               + AsciiFrameHelper + MelsecServerDeviceMapper
│  ├─ PlcServer/                   PlcServerBase（公共骨架）+ 四个模拟服务
│  │                               Melsec / Inovance / Omron / Siemens
│  └─ PlcServerBuilder.cs          服务端建造者（结构同客户端 Builder，Build 按接口分派）
├─ View/                           界面层，按「是不是可视控件」分三个目录
│  ├─ Forms/                       MainForm / ConnectSettingsForm / PlcWorkspaceForm / PlcServerWorkspaceForm
│  ├─ Controls/                    MGroupBox（自绘分组框）/ FrameLogView（报文视图）/
│  │                               AddressInputPanel（地址栏）/ MonitorGrid（监视列表）
│  └─ Support/                     ProtocolCapability（协议能力快照）/ AddressTextParser（地址解析）/
│                                  MonitorReadResult（一次读取的结果）
├─ Tool/                           ConverterTool（位/字节/ASCII）/ DataFormatConverter（32 位排列）/
│                                  OtherTool（校验）/ ProtocolOptionValues（选项值夹范围）/ StructureHelp
├─ Document/                       协议报文、厂商手册与架构资料
└─ Program.cs                      程序入口
```

## 文档资料

- `Document/md/项目架构总览.png`：架构图
- `Document/Melsec/`：3E / 4E / 1E 报文、控制命令截图；`寄存器地址.png` 为《基本型 QCPU 软元件一览》，`1E寄存器地址.png` 为 A 兼容 1E / FX 系列的软元件与编号范围
- `Document/Siemens/`：`西门子S7协议介绍.docx`（TPKT/COTP/S7 头、Request Item 与 Data Item 字段、错误码、块上传下载），另有两次握手、读写报文、区域类型、功能编码、ROSCTR、PDU 类型截图
- 西门子的报文基准还参考了博客园《西门子s7通信协议》（2024-09-06）：COTP 连接、Setup Communication、读写 M2000 的完整十六进制序列，已抄进冒烟测试逐字节比对
- `Document/Inovance/Modbus.doc`：Modbus 协议资料
- `Document/Inovance/Easy寄存器地址.png`、`H3U寄存器地址.png`：两机型的 Modbus 地址映射，均为官方手册页面截图，原页见下表

### 官方手册索引

软元件代码、位/字能力、编号范围与 Modbus 编址，**以下面这些手册为准**，截图只作速查。四份 catalog 的每一项都应能在这里找到出处。

| 手册 | 文件 | 关键页 |
| --- | --- | --- |
| H3U / H3S 指令及编程手册（753 页） | `19010394-SC_A20（…H3U H3S系列…）.pdf` | **30** 3.1 软元件一览表（全部范围）<br>**33** 3.5 定时器（6 类共 512 点）<br>**571–573** 9.4.2 功能码与帧格式（含数量上限 2000 / 125 / 123）<br>**574** 9.4.3 变量编址（线圈编址 + 寄存器编址）<br>**576** 9.4.3 Modbus 通信地址（首地址 + 变量序号）<br>**680** 9.7 以太网 Modbus 通信地址 |
| H5U & Easy 编程手册（1036 页） | `19011157-SC_A21_H5U&Easy系列…pdf` | **110–111** 6.2 软元件（位 / 字）<br>**419** 9.x 串口 Modbus 从站地址<br>**435** 10.3.3 以太网 Modbus 从站地址<br>**995–997** 附录 功能码详解 |
| FINS 通信指令手册（127 页） | `Document/Omron/FINS通讯手册_中文版.pdf`（OMRON W342 中文翻译版）；同目录另有 `FINS报文.pdf` | **80–84** 5-2-2 区码表与地址示例（含 TIM/CNT 共码、EM 按 bank）<br>**26–27** 0101 读命令示例<br>**29–30** 指令/响应帧结构 |
| MELSEC-Q/L 通讯协议参考手册（518 页） | `三菱QL系列MC协议.pdf`（CJK CID 编码，需用 `pdf-cjk-cid-text` skill 提取） | **125–127** 3E/4E 软元件范围（基本型 / 高性能型+过程+冗余+QnA / 通用型+LCPU 三套）<br>**430** 6.1.2 响应报文格式（二进制 `80 00` / 异常 `80 5B 12 00`，证明结束码为 1 字节）<br>**436–437** A 兼容 1E 帧报文示例（编号 → 软元件名 → 点数）<br>**442–444** 1E 软元件代码表（含 TC 54H,43H、CC 43H,43H） |

从手册里查证、容易踩错的结论：

- **H3U 的 `T256–T511` 只有触点，没有当前值**：线圈编址 `T0~T511 @0xF000`（512 点），寄存器编址只到 `T0~T255 @0xF000`（256 点）。手册 574 页与 680 页两处口径一致，是官方设计而非遗漏。故 `TS` 上限 511、`TN` 上限 255。
- **Easy 编程支持 `W0~W32767`，但 Modbus 不映射 W**（435 页原文：不支持 W 元件、不支持 Pointer 变量）。故 `InovanceDeviceCatalog` 中 Easy 不含 W 区。
- **3E / 4E 的 `R` 是十进制、`ZR` 才是十六进制**（125 页「表示」列）。
- **1E 的 `TC` / `CC` 是独立软元件代码**（442 页），六个定时器/计数器代码都要建。

## 冒烟验证

`Tests/PlcTest.SmokeTest` 是一个不依赖测试框架的控制台工程（**不在 `PlcTest/` 仓库内**，随便删）。当前 **600 项断言全部通过、0 失败（2026-11-10 基线）**：

```powershell
cd Tests/PlcTest.SmokeTest
dotnet build -c Release
dotnet run -c Release --no-build
```

覆盖范围：

- **客户端协议帧**：3E/4E Binary、3E ASCII、1E Binary/ASCII、Modbus TCP/RTU、西门子 TPKT 的帧长推断与逐字节构帧（含 S7 两段握手、DB 区、多变量、位写打包）
- **服务端端到端往返（10 种协议）**：客户端组包 → 服务端解析 → 服务端响应 → 客户端解析，并校验帧长判定 = 实际请求长度；S7 额外覆盖多变量跨区/跨 DB、越界项独立报错、PDU 分段（3 帧读、2 帧写）
- **结构与能力**：位/字门禁与地址归一化、各协议暴露的软元件区域、四份软元件表逐项核对、机型差异、编号范围校验、`Result.ErrorKind` 分类
- **界面代理测试**：每个可设计控件/窗体用无参构造实例化并遍历控件树（等价于设计器打开时做的事），窗体边框断言「不可调整」
- **协议选项**：登记表、越界夹取、选项真正落到帧（汇川 Easy 剩 7 个软元件、S7 槽号进 COTP、PDU 长度进 Setup）

## 待办

### 待做

- [ ] 西门子 S7**上真机验证**（M / X / Y / DB 区读写、两次握手、错误码分支）。已验证的只有「按公开报文样本逐字节核对」与自建服务端回环；另见下方暂缓项中的外部基准验证
- [ ] **三菱 / 欧姆龙服务端缺地址上限校验**：两家的 `TryAddRegister` 都只判 `address < 0`，而 `D` 的上限 11135、欧姆龙各区范围都写在 catalog 里——客户端 `GetMaxIndex` 用了，服务端完全没用。后果：界面能注册 `D999999`，客户端也读得到，读回 0 且报成功，会掩盖**客户端越界访问**的 bug。汇川已经做对了（`TryResolve` + 批量长度校验），照它补即可。**成本不在判断本身**：`MelsecPlcServer` 是 6 个 MC 协议共用一个类，而 `IServerProtocol` 没暴露 `GetMaxIndex`
- [ ] **S7 多变量的数据项偶数字节对齐**：标准要求多变量各数据项从偶数偏移开始（奇数长度的项后面补 `0x00`）。当前实现客户端与服务端都是「紧挨着拼、紧挨着读」，两边自洽但与规范不一致——**含奇数长度项（如 3 位、1 字节）的多变量请求在真机上会错位**。参考文档 §2.2 与 `Document/Siemens` 里的 Data Item 说明
- [ ] 汇川 32 位计数器的界面展示：协议层已返回每点两个 16 位寄存器，但监视表仍按「一行一个寄存器」渲染，`C200` 以上会显示成两行（低 16 位 / 高 16 位），尚未合并成 32 位值；清零按钮也需按 2 倍长度生成数据。**解码能力已就绪**（`DataFormatConverter` + 协议上的 `DataFormat`），差的只是表格这一层
- [ ] 汇川 M 区分段：H3U 的 M 是 `M0–M7679` + `M8000–M8511`，中间 7680–7999 是空洞；`GetMaxIndex` 只能表达单一下限，目前按 8511 放行，空洞内地址不拦
- [ ] 3E / 4E 的软元件范围按**基本型 QCPU**建模（`D` 到 11135、`X` 到 0x7FF）。官方手册还给出「高性能型 / 过程 / 冗余 / QnA CPU」（126 页，`D` 到 12287、`X` 到 0x1FFF）和「通用型 QCPU / LCPU」（127 页）两套更大的范围；若实际连这些机型，超出的合法地址会被误拦（不会读错，只是保守拒绝）
- [ ] 欧姆龙 EM bank 的界面预建：`TryAddRegister` 只能建 `bank = 0xFF` 的地址，界面无法为 bank≠0 预建寄存器。可以照 S7 的做法（`PlcAddress` 带 `DbNumber` 的重载）补，优先级低
- [ ] 位视图目前把 0/1 放进「值」列，若要更紧凑（单独位格）需要按视图动态显隐列
- [ ] `btn_Trigger` 触发按钮还没有事件实现
- [ ] 界面提示语改中文（当前是 `Please Check ...` 这类英文）
- [ ] 用真实 PLC / 客户端做一次真机联调（本轮改造只做了编译 + 冒烟验证）

> 汇川 T256–T511 只支持触点不是缺陷：手册 9.4.3 与 9.7 两处口径一致，寄存器编址只到 `T0~T255`。`TN` 上限 255、`TS` 上限 511 与手册一致。

### 暂缓：西门子 S7 三项（2026-11-10 明确不做）

> 三条都是「知道该怎么做、也知道代价」，本轮决定不做。写在这里是为了下次不重复评估，也避免有人以为已经支持了。

- [ ] **① DB 区字节 / 双字读写（`DB1.DBB0` / `DB1.DBD0`）**
  - 现状：只支持整字 `DB1.DBW0`。`AddressTextParser` 对 `DBB` / `DBD` 是**明确拒绝并说明原因**，不是解析成字（否则会读错 4 倍的数据）。
  - 卡点：对外接口只有 `bool` / `short` 两套，1 字节和 4 字节都没有出口。要加就得先定「可选能力接口（只有 S7 实现）」还是「扩公共接口（10 个协议全动）」——这一步不定，写出来的 API 会绑死。
  - 若要做：TS=0x02 时元素数字节数，服务端 `TryParseItem` 里「字访问字节数必须为偶数」的拒绝要放开；`PlcAddress` 还得能表达「1 字节 / 4 字节」，否则 `Length` 会被当成字数。
- [ ] **② 位偏移（`M0.3` / `DB1.DBX0.3`）**
  - 现状：`AddressTextParser` 明确拒绝。原因不是没实现，而是 `PlcAddress` **没有位序号字段**——硬解析成 `M0` 会读到同一字节的 bit0，看着成功、读的不是同一个点，比报错危险得多。
  - 卡点：`PlcAddress` 加 `BitOffset`（0–7）会波及 40+ 个引用文件的构造调用；`BuildReadData` 要改成 `addrVal = Address * 8 + BitOffset`；分段时位段起点不再需要「落在字节边界」这条妥协；`AddressTextParser.Format`、`AddressInputPanel` 的回填、`MonitorGrid` 的地址显示都要跟着改。
  - 工作量：协议层约半天，主要成本在 `PlcAddress` 这个 fan-in 最高的类型上。
- [ ] **③ 外部基准验证（HSL 虚拟服务端 / 真实 S7-1200）**
  - 现状：S7 的全部结论都来自「我们的客户端 ↔ 我们的服务端」回环 + 公开报文样本。回环只能证明**两端一致**，不能证明符合规范。
  - 为什么必须做：已经被这个模式坑过三次——响应头 12 vs 10 字节、位读按位打包、位写按位打包，每次都是「两边一起错、测试全绿」。多变量与分段是第四次同类风险。
  - 具体做法：HSL 虚拟服务端或真机各跑一遍「读位 / 读字 / 写位 / 写字 / 多变量读 / 跨 DB 读写 / 超过 467 字的长读」，逐字节比对请求与响应。

### 暂缓：结构扩展性（2026-11-05 评审结论）

> 这几条不是 bug，是「现在不改也能跑，但会卡住下一次扩展」的设计债。按性价比 A→D 排序，E 建议等真有第四家厂商再做。

- [ ] **A. 传输能力从 4 个 bool 改成 flags**：`ProtocolInfoAttribute` 的 `clientTcp` / `clientSerial` / `serverTcp` / `serverSerial` 是硬编码的二元矩阵，加 UDP / TLS 要改构造签名（10 个枚举标签全部要动）；更麻烦的是 `ProtocolTypeExtensions.GetProtocols` 里 `bool tcp = communicationType == CommunicationType.Tcp`——第三种传输会被当成串口，**静默返回错误集合**。改成 `[Flags] enum TransportSupport` + `HasFlag` 即可。改动面：`Models/Enums.cs`（属性 + 10 个标签 + `GetProtocols`）、`View/Forms/ConnectSettingsForm.cs`（通讯方式下拉目前写死 TCP / 串口两项）。
- [ ] **B. 拆客户端与服务端的重复协议知识**：`IPlcClientProtocol.BuildReadData` 与 `IServerProtocol.ParseRequest` 各自实现同一套地址编址（三菱的软元件代码 + 24 位偏移、S7 的区码 + 字节偏移），新增厂商要在两边各写一遍。可以抽一个「地址编解码」能力接口，让客户端组帧与服务端解帧共用同一份映射。收益要到第四家厂商才明显。
- [ ] **C. 抽公共表格控件**：`MonitorGrid` 已经收敛了「建行、刷新、双击写值」，但两个工作台里仍有若干重复的启停与状态刷新逻辑。下一步是把「读取计划（周期 / 分段 / 失败重试）」也收进控件，否则「汇川 32 位合并显示」这类需求还要在两层各改一遍。
- [ ] **D. 拆 `MemoryArea`（成本高，建议最后做）**：33 个值混了三菱与欧姆龙（`CIO/HR/AR/EM` 欧姆龙专用，`D` 在三菱是数据寄存器、在欧姆龙是 DM，`DB` 是西门子专用），**40 个文件**引用，是 fan-in 最高的类型。拆成各厂商枚举 + 映射能消除厂商知识泄漏，但影响面大、收益要等第四家厂商出现才兑现。

顺带记录、暂不单独立项：`PlcType` 与 `ProtocolType` 是双枚举（`PlcType` 的引用全在 `PlcClientBuilder` 内，只用于厂商/协议错配校验），加厂商要同时改两个；`PlcAddress` 是可变类且无 `Equals`，不能当字典键。

### 已完成（2026-11 归档）

- [x] **西门子 S7 服务端（从站）**：新增 `IS7ServerProtocol` / `S7RequestInfo` / `SiemensS71200ServerProtocol` / `S7PlcServer`，I/Q/M/DB 四区、位字共享字节内存、多变量、DB 按块惰性分配。`serverTcp` 已打开，界面两端都可选
- [x] **协议选项统一通道**：`ProtocolOption` / `ProtocolOptionCatalog` / `IProtocolOptionHost` / `Builder.UseOptions()`，解决「机型只能强制转换、界面无法设置」
- [x] **S7 多变量读写与 PDU 分段**：见「协议支持 · 西门子」
- [x] **`IRegisterTable` 增加带 `PlcAddress` 的重载**：`TryAddRegister(PlcAddress)` / `ResetRegisterValues(PlcAddress, int)`，`PlcServerBase` 给 virtual 默认实现，只有 `S7PlcServer` 覆盖
- [x] **界面重构**：新增两个调试工作台替换旧的 `PlcClientForm` / `PlcServerForm`，五个窗体/控件转设计器可打开，`View/` 按 Forms / Controls / Support 重组
