using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Generic;

namespace PlcTest.Server.PlcServer
{
    /// <summary>
    /// 模拟服务端的公共骨架：通讯启停、客户端计数、通讯日志、收帧异常兜底。
    /// <para>
    /// 子类只负责「拿到一帧怎么应答」（<see cref="HandleFrame"/>）和「寄存器怎么存」
    /// （<see cref="IPlcServer"/> / <see cref="IRegisterTable"/> 的成员）。
    /// 三家服务端原本各抄一份启停与日志，连文案都一字不差——改一处很容易漏掉另两处，
    /// 上次「ClearAll 只清三菱和欧姆龙」就是这类分叉的后果。
    /// </para>
    /// </summary>
    public abstract class PlcServerBase : IPlcServer, IRegisterTable
    {
        private readonly IServerCommunication _comm;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">服务端通讯方式</param>
        protected PlcServerBase(IServerCommunication comm)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
        }

        /// <summary>
        /// 通讯方式（收发字节、连接数）
        /// </summary>
        protected IServerCommunication Communication
        {
            get { return _comm; }
        }

        /// <summary>
        /// 服务是否开启
        /// </summary>
        public bool ServerIsOpen
        {
            get { return _comm != null && _comm.IsRunning; }
        }

        /// <summary>
        /// 连接客户端数量
        /// </summary>
        public int ClientCount
        {
            get { return _comm != null ? _comm.ClientCount : 0; }
        }

        /// <summary>
        /// 通讯日志事件（与客户端 <c>IPlcClient.ShowCommMessageEvent</c> 对称）。
        /// 事件在协议线程上触发，订阅方自己负责切回 UI 线程。
        /// </summary>
        public event Action<string> ShowCommMessageEvent;

        /// <summary>
        /// 结构化通讯帧事件（与客户端 <c>IPlcClient.FrameEvent</c> 对称）。
        /// 同时触发旧的字符串事件，两者内容等价。
        /// </summary>
        public event Action<CommFrame> FrameEvent;

        /// <summary>
        /// 启动服务：挂接收委托、开通讯、记一条日志。重复调用是空操作。
        /// </summary>
        public void OpenServer()
        {
            if (_comm.IsRunning)
            {
                return;
            }

            _comm.OnDataReceived += OnDataReceived;
            _comm.Start();
            LogComm("服务端已启动，等待客户端连接");
        }

        /// <summary>
        /// 关闭服务：摘接收委托、停通讯、记一条日志。
        /// </summary>
        public void CloseServer()
        {
            if (!_comm.IsRunning)
            {
                return;
            }

            _comm.OnDataReceived -= OnDataReceived;
            _comm.Stop();
            LogComm("服务端已停止");
        }

        /// <summary>
        /// 收帧入口：只做异常兜底，具体应答交给子类。
        /// 解析异常必须记日志，否则现场只会看到「客户端超时」，查不到原因。
        /// </summary>
        private void OnDataReceived(byte[] data, IClientSession client)
        {
            try
            {
                HandleFrame(data, client);
            }
            catch (Exception ex)
            {
                LogComm("请求处理异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 处理一帧请求：解析 → 查表 → 组包 → 回发。抛出的异常由基类统一记日志。
        /// </summary>
        /// <param name="data">收到的原始报文</param>
        /// <param name="client">来源连接</param>
        protected abstract void HandleFrame(byte[] data, IClientSession client);

        /// <summary>
        /// 本服务端支持的软元件区域，供界面「软元件视图」列行（与客户端同名方法对称）。
        /// </summary>
        public abstract IReadOnlyList<MemoryArea> GetSupportedAreas();

        #region 寄存器存取（存储模型由子类决定，这里只声明契约）
        // 三家服务端的存储形态不同：三菱按区域分字典、欧姆龙用 key = 区域<<8 | 索引、
        // 汇川用定长数组。差异只到这一层，界面和协议层看到的都是 IPlcServer / IRegisterTable。

        /// <summary>读单个位；未建立的地址按 false 返回。</summary>
        public abstract Result<bool> ReadBit(PlcAddress address);

        /// <summary>读位数组；长度取自 <see cref="PlcAddress.Length"/>。</summary>
        public abstract Result<bool[]> ReadBitArray(PlcAddress address);

        /// <summary>写单个位。</summary>
        public abstract Result<bool> WriteBit(PlcAddress address, bool value);

        /// <summary>写位数组。</summary>
        public abstract Result<bool> WriteBitArray(PlcAddress address, bool[] values);

        /// <summary>读单个字；未建立的地址按 0 返回。</summary>
        public abstract Result<short> ReadWord(PlcAddress address);

        /// <summary>读字数组。</summary>
        public abstract Result<short[]> ReadWordArray(PlcAddress address);

        /// <summary>写单个字。</summary>
        public abstract Result<bool> WriteWord(PlcAddress address, short value);

        /// <summary>写字数组。</summary>
        public abstract Result<bool> WriteWordArray(PlcAddress address, short[] values);

        /// <summary>指定区域已建立的寄存器数量。</summary>
        public abstract int GetRegisterCount(MemoryArea area);

        /// <summary>指定地址是否已建立。</summary>
        public abstract bool ContainsRegister(MemoryArea area, int address);

        /// <summary>建立地址（值初始化为 0/false），已存在时返回 false。</summary>
        public abstract bool TryAddRegister(MemoryArea area, int address);

        /// <summary>移除区域内的全部地址（值随之消失）。</summary>
        public abstract void ClearRegisters(MemoryArea area);

        /// <summary>把区间内的值清零，地址保留。</summary>
        public abstract void ResetRegisterValues(MemoryArea area, int start, int length);

        /// <summary>把整个区域的值清零，地址保留。</summary>
        public abstract void ResetAllRegisterValues(MemoryArea area);

        /// <summary>
        /// 按完整地址建立寄存器。默认忽略块号、转调 <see cref="TryAddRegister(MemoryArea,int)"/>：
        /// 地址里的 DbNumber 只有 S7 的 DB 区（以及欧姆龙的 EM bank）才有意义，
        /// 其余协议的实现不必为此多写一份。
        /// </summary>
        /// <param name="address">完整地址</param>
        public virtual bool TryAddRegister(PlcAddress address)
        {
            return address != null && TryAddRegister(address.Area, address.Address);
        }

        /// <summary>按完整地址清零区间。默认忽略块号，转调三元组版本。</summary>
        /// <param name="address">完整地址</param>
        /// <param name="length">长度（字/点位个数）</param>
        public virtual void ResetRegisterValues(PlcAddress address, int length)
        {
            if (address != null)
            {
                ResetRegisterValues(address.Area, address.Address, length);
            }
        }
        #endregion

        /// <summary>
        /// 触发通讯日志。订阅方抛异常不能影响报文处理，这里吞掉。
        /// 旧字符串事件与新帧事件各抛一次：老界面照旧，新报文视图能拿到方向与字节。
        /// </summary>
        /// <param name="message">日志内容</param>
        protected void LogComm(string message)
        {
            RaiseSafe(message, CommFrame.Note(message));
        }

        /// <summary>
        /// 记一条收发帧。旧事件收到的文本与重构前逐字一致（"发送报文: 01-FF"），
        /// 新事件额外带上方向与原始字节。
        /// </summary>
        /// <param name="isSend">true 表示本端发出</param>
        /// <param name="frame">完整报文</param>
        /// <param name="caption">说明，默认「发送报文」/「接收报文」；丢弃、握手等场景可写具体原因</param>
        protected void LogFrame(bool isSend, byte[] frame, string caption = null)
        {
            CommFrame item = isSend ? CommFrame.Send(frame, caption) : CommFrame.Receive(frame, caption);
            RaiseSafe(item.ToLogText(), item);
        }

        /// <summary>
        /// 两个事件一起抛，任一订阅方抛异常都不影响另一路与报文处理。
        /// </summary>
        private void RaiseSafe(string message, CommFrame frame)
        {
            Action<string> messageHandler = ShowCommMessageEvent;
            if (messageHandler != null)
            {
                try
                {
                    messageHandler(message);
                }
                catch
                {
                    // 日志订阅方的异常不能影响报文处理
                }
            }

            Action<CommFrame> frameHandler = FrameEvent;
            if (frameHandler != null)
            {
                try
                {
                    frameHandler(frame);
                }
                catch
                {
                    // 同上
                }
            }
        }
    }
}
