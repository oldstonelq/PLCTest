using PlcTest.Client.ClientProtocolManagement.Inovance;
using PlcTest.Models;
using PlcTest.Client.PlcInterface;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlcTest.Client.PlcClient.Inovance
{
    /// <summary>
    /// 汇川PLC客户端类，使用汇川协议进行通讯
    /// </summary>
    public class InovancePlcClient:IPlcClient
    {
        /// <summary>
        /// 通讯方式接口
        /// </summary>
        protected IClientCommunication _comm;
        /// <summary>
        /// 通讯协议接口
        /// </summary>
        protected IPlcClientProtocol _communicationProtocol;
        /// <summary>
        /// 连接状态:
        /// </summary>
        public bool IsConnected => _comm == null ? false : _comm.IsConnected;
        /// <summary>
        /// 取消令牌
        /// </summary>
        private CancellationTokenSource _cts;
        /// <summary>
        /// 心跳线程
        /// </summary
        private Task _connectTask;
        /// <summary>
        /// 关闭标志 True表示已关闭，False表示未关闭
        /// </summary>
        private bool _disposed = false;
        /// <summary>
        /// 显示交互信息
        /// </summary>
        public event Action<string> ShowCommMessageEvent;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">通讯方式</param>
        /// <param name="protocol">协议实现（可选），若为 null 则使用默认的 InovanceModbusTcpClientProtocol</param>
        public InovancePlcClient(IClientCommunication comm, IPlcClientProtocol protocol = null)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
            // 使用构造注入替代硬编码，便于单元测试与替换协议实现
            _communicationProtocol = protocol ?? new InovanceModbusTcpClientProtocol();
        }

        #region 公有方法
        /// <summary>
        /// 连接方法
        /// </summary>
        public void Connect()
        {
            // 1. 先判断：心跳任务正在运行，直接返回，禁止重复启动
            if (_connectTask != null && !_connectTask.IsCompleted)
            {
                return;
            }
            // 2. 清理上一次残留的令牌源，释放资源
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
            // 3. 新建令牌，启动心跳循环
            _cts = new CancellationTokenSource();
            //4. 启动心跳循环任务
            _connectTask = Task.Run(() => HeartBeatLoop(_cts.Token), _cts.Token);
        }
        /// <summary>
        /// 断开连接方法
        /// </summary>
        public void Disconnect()
        {
            //1.如果已经关闭，则直接返回
            if (_disposed) return;
            _disposed = true;

            try
            {
                //2.发送心跳线程取消请求
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                try
                {
                    // 3.等待连接任务退出（短超时以防阻塞）
                    _connectTask?.Wait(2000);
                }
                catch (AggregateException) { }
                catch (Exception) { }

                // 4.尝试关闭 通讯方式的底层 连接
                try
                {
                    if (_comm != null)
                    {
                        _comm.DisconnectServer();
                    }
                }
                catch { }
            }
            finally
            {
                //4.释放所有管理资源
                _cts?.Dispose();
                _cts = null;
                _connectTask = null;
                _comm = null;
            }
        }
        /// <summary>
        /// 读取单个位数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<bool> ReadBit(PlcAddress address)
        {
            Result<bool> result = new Result<bool>();
            //1.校验软元件类型
            if (!address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.拼接数据报文并构建完整报文
            byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //3.数据校验与解析
            var res = _communicationProtocol.ParseReadBitResponse(receiveBytes, 1);
            result.IsSuccess = res.IsSuccess;
            result.ErrorMsg = res.ErrorMsg;
            result.Data = res.Data != null && res.Data.Length > 0 ? res.Data[0] : false;

            return result;
        }
        /// <summary>
        /// 读取位数组数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<bool[]> ReadBitArray(PlcAddress address)
        {
            Result<bool[]> result = new Result<bool[]>();
            //1.校验软元件类型
            if (!address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.拼接数据报文并构建完整报文
            byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //3.数据校验与解析
            result = _communicationProtocol.ParseReadBitResponse(receiveBytes, address.Length);

            return result;
        }
        /// <summary>
        /// 写入单个位数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="value">写入值</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteBit(PlcAddress address, bool value)
        {
            Result<bool> result = new Result<bool>();
            //1.校验软元件类型
            if (!address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.计算数据
            byte[] mData = _communicationProtocol.GetWriteBitData(new bool[1] { value });
            //3.拼接数据报文并构建完整报文
            byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //4.数据校验与解析
            result = _communicationProtocol.ParseWriteResponse(receiveBytes);

            return result;
        }
        /// <summary>
        /// 写入位数组数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="values">位数据数组</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteBitArray(PlcAddress address, bool[] values)
        {
            Result<bool> result = new Result<bool>();
            //1.检查写入数据是否为空
            if (values == null || values.Length == 0)
            {
                result.ErrorMsg = "写入数据为空";
                return result;
            }
            //2.校验软元件类型
            if (!address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //3.计算打包后字节
            byte[] mData = _communicationProtocol.GetWriteBitData(values);
            //4.拼接数据报文并构建完整报文
            byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //5.数据校验与解析
            result = _communicationProtocol.ParseWriteResponse(receiveBytes);
            return result;
        }
        /// <summary>
        /// 读取单个字数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<short> ReadWord(PlcAddress address)
        {
            Result<short> result = new Result<short>();
            //1.校验软元件类型
            if (address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.拼接数据报文并构建完整报文
            byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //3.数据校验与解析
            var res = _communicationProtocol.ParseReadWordResponse(receiveBytes, 1);
            result.IsSuccess = res.IsSuccess;
            result.ErrorMsg = res.ErrorMsg;
            result.Data = res.Data != null && res.Data.Length > 0 ? res.Data[0] : (short)0;
            return result;
        }
        /// <summary>
        /// 读取字数组数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<short[]> ReadWordArray(PlcAddress address)
        {
            Result<short[]> result = new Result<short[]>();
            //1.校验软元件类型
            if (address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.拼接数据报文并构建完整报文
            byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //3.数据校验与解析
            result = _communicationProtocol.ParseReadWordResponse(receiveBytes, address.Length);
            return result;
        }
        /// <summary>
        /// 写入单个字数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="value">写入值</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteWord(PlcAddress address, short value)
        {
            Result<bool> result = new Result<bool>();
            //1.校验软元件类型
            if (address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.计算打包后字节
            byte[] mData = _communicationProtocol.GetWriteWordData(new short[1] { value });
            //3.拼接数据报文并构建完整报文
            byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //4.数据校验与解析
            result = _communicationProtocol.ParseWriteResponse(receiveBytes);
            return result;
        }
        /// <summary>
        /// 写入字数组数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="values">字数据数组</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteWordArray(PlcAddress address, short[] values)
        {
            Result<bool> result = new Result<bool>();
            //1.检查写入数据是否为空
            if (values == null || values.Length == 0)
            {
                result.ErrorMsg = "写入数据为空";
                return result;
            }
            //2.校验软元件类型
            if (address.IsBit)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //3.计算打包后字节
            byte[] mData = _communicationProtocol.GetWriteWordData(values);
            //4.拼接数据报文并构建完整报文
            byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
            byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
            ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(BuildCompletePack)}");
            byte[] receiveBytes = _comm.SendAndReceiveData(BuildCompletePack);
            ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(receiveBytes)}");
            //5.数据校验与解析
            result = _communicationProtocol.ParseWriteResponse(receiveBytes);
            return result;
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 心跳循环，用于在连接断开时自动重连
        /// </summary>
        /// <param name="token">取消令牌</param>
        private async Task HeartBeatLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (IsConnected)
                        {
                            // 已连接，等待一段时间再检查，避免忙循环
                            Thread.Sleep(1000);
                            continue;
                        }
                        else
                        {
                            //未连接，尝试重新连接
                            for (int i = 0; i < 3 && !IsConnected && !token.IsCancellationRequested; i++)
                            {
                                _comm.ConnectServer();
                            }
                        }
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // 忽略连接内异常，继续重试，除非被取消
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 取消时安全退出
            }
        }
        #endregion
    }
}
