using PlcTest.Client.ClientInterface;
using PlcTest.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlcTest.Client.PlcClient
{
    /// <summary>
    /// PLC 客户端通用实现：持有通讯对象与协议对象，负责连接/断开、心跳重连、
    /// 8 个读写的“拼包 → 发送 → 解析”骨架，以及把通讯/协议异常收敛为失败 Result。
    /// 厂商差异通过组合注入：通讯方式、协议实现，以及可选的“位/字地址判定规则”。
    /// </summary>
    public class CommunicationPlcClient : IPlcClient
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
        /// 位/字地址判定规则：默认沿用 PlcAddress.IsBit；
        /// 按软元件区域区分位/字的协议（如三菱 MC）由构造时注入规则。
        /// </summary>
        private readonly Func<PlcAddress, bool> _isBitArea;

        /// <summary>
        /// 取消令牌
        /// </summary>
        private CancellationTokenSource _cts;
        /// <summary>
        /// 心跳线程
        /// </summary>
        private Task _connectTask;
        /// <summary>
        /// 显示交互信息
        /// </summary>
        public event Action<string> ShowCommMessageEvent;

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected => _comm == null ? false : _comm.IsConnected;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">通讯方式</param>
        /// <param name="protocol">协议实现</param>
        /// <param name="isBitArea">位/字地址判定规则，为 null 时使用 PlcAddress.IsBit</param>
        public CommunicationPlcClient(IClientCommunication comm, IPlcClientProtocol protocol, Func<PlcAddress, bool> isBitArea = null)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
            _communicationProtocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _isBitArea = isBitArea ?? (address => address.IsBit);
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
        /// 断开连接方法：只停止心跳并关闭底层连接，保留通讯对象，之后仍可再次 Connect()。
        /// </summary>
        public void Disconnect()
        {
            try
            {
                //1.发送心跳线程取消请求
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                try
                {
                    // 2.等待连接任务退出（短超时以防阻塞）
                    _connectTask?.Wait(2000);
                }
                catch (AggregateException) { }
                catch (Exception) { }

                // 3.关闭 通讯方式的底层 连接
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
                //4.释放令牌资源。_comm 必须保留，否则再次 Connect() 时心跳拿不到通讯对象
                _cts?.Dispose();
                _cts = null;
                _connectTask = null;
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
            try
            {
                //1.校验软元件类型
                if (!_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //3.数据校验与解析
                var res = _communicationProtocol.ParseReadBitResponse(receiveBytes, 1);
                result.IsSuccess = res.IsSuccess;
                result.ErrorMsg = res.ErrorMsg;
                result.Data = res.Data != null && res.Data.Length > 0 ? res.Data[0] : false;

                return result;
            }
            catch (Exception ex)
            {
                // 协议层拼包/解析抛出的异常统一收敛，不穿透到 UI 线程
                return ProtocolError<bool>(ex.Message);
            }
        }
        /// <summary>
        /// 读取位数组数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<bool[]> ReadBitArray(PlcAddress address)
        {
            Result<bool[]> result = new Result<bool[]>();
            try
            {
                //1.校验软元件类型
                if (!_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //3.数据校验与解析
                result = _communicationProtocol.ParseReadBitResponse(receiveBytes, address.Length);

                return result;
            }
            catch (Exception ex)
            {
                return ProtocolError<bool[]>(ex.Message);
            }
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
            try
            {
                //1.校验软元件类型
                if (!_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //2.计算数据
                byte[] mData = _communicationProtocol.GetWriteBitData(new bool[1] { value });
                //3.拼接数据报文并构建完整报文
                byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //4.数据校验与解析
                result = _communicationProtocol.ParseWriteResponse(receiveBytes);

                return result;
            }
            catch (Exception ex)
            {
                return ProtocolError<bool>(ex.Message);
            }
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
            try
            {
                //1.检查写入数据是否为空
                if (values == null || values.Length == 0)
                {
                    result.ErrorMsg = "写入数据为空";
                    return result;
                }
                //2.校验软元件类型
                if (!_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //3.计算打包后字节
                byte[] mData = _communicationProtocol.GetWriteBitData(values);
                //4.拼接数据报文并构建完整报文
                byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //5.数据校验与解析
                result = _communicationProtocol.ParseWriteResponse(receiveBytes);
                return result;
            }
            catch (Exception ex)
            {
                return ProtocolError<bool>(ex.Message);
            }
        }
        /// <summary>
        /// 读取单个字数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<short> ReadWord(PlcAddress address)
        {
            Result<short> result = new Result<short>();
            try
            {
                //1.校验软元件类型
                if (_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //3.数据校验与解析
                var res = _communicationProtocol.ParseReadWordResponse(receiveBytes, 1);

                result.IsSuccess = res.IsSuccess;
                result.ErrorMsg = res.ErrorMsg;
                result.Data = res.Data != null && res.Data.Length > 0 ? res.Data[0] : (short)0;
                return result;
            }
            catch (Exception ex)
            {
                return ProtocolError<short>(ex.Message);
            }
        }
        /// <summary>
        /// 读取字数组数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<short[]> ReadWordArray(PlcAddress address)
        {
            Result<short[]> result = new Result<short[]>();
            try
            {
                //1.校验软元件类型
                if (_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //3.数据校验与解析
                result = _communicationProtocol.ParseReadWordResponse(receiveBytes, address.Length);
                return result;
            }
            catch (Exception ex)
            {
                return ProtocolError<short[]>(ex.Message);
            }
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
            try
            {
                //1.校验软元件类型
                if (_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //2.计算打包后字节
                byte[] mData = _communicationProtocol.GetWriteWordData(new short[1] { value });
                //3.拼接数据报文并构建完整报文
                byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //4.数据校验与解析
                result = _communicationProtocol.ParseWriteResponse(receiveBytes);
                return result;
            }
            catch (Exception ex)
            {
                return ProtocolError<bool>(ex.Message);
            }
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
            try
            {
                //1.检查写入数据是否为空
                if (values == null || values.Length == 0)
                {
                    result.ErrorMsg = "写入数据为空";
                    return result;
                }
                //2.校验软元件类型
                if (_isBitArea(address))
                {
                    result.ErrorMsg = "软元件类型错误";
                    return result;
                }
                //3.计算打包后字节
                byte[] mData = _communicationProtocol.GetWriteWordData(values);
                //4.拼接数据报文并构建完整报文
                byte[] BuildWriteData = _communicationProtocol.BuildWriteData(address, mData);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildWriteData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    return result;
                }
                //5.数据校验与解析
                result = _communicationProtocol.ParseWriteResponse(receiveBytes);
                return result;
            }
            catch (Exception ex)
            {
                return ProtocolError<bool>(ex.Message);
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 构造一个“协议异常”失败结果，统一协议层拼包/解析抛出的异常信息
        /// </summary>
        private static Result<T> ProtocolError<T>(string message)
        {
            return new Result<T> { ErrorMsg = "协议异常: " + message };
        }

        /// <summary>
        /// 发送报文并接收响应，把通讯层异常统一收敛成失败结果，不让异常穿到 UI 线程
        /// </summary>
        /// <param name="pack">完整请求报文</param>
        /// <param name="response">响应报文</param>
        /// <param name="error">失败原因</param>
        /// <returns>True:通讯成功 False:通讯失败</returns>
        protected bool TrySendAndReceive(byte[] pack, out byte[] response, out string error)
        {
            response = null;
            error = null;
            try
            {
                if (_comm == null)
                {
                    error = "通讯对象已释放";
                    return false;
                }

                ShowCommMessageEvent?.Invoke($"发送报文: {BitConverter.ToString(pack)}");
                response = _comm.SendAndReceiveData(pack);
                ShowCommMessageEvent?.Invoke($"接收报文: {BitConverter.ToString(response)}");
                return true;
            }
            catch (Exception ex)
            {
                error = "通讯异常: " + ex.Message;
                return false;
            }
        }

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
                            //未连接，尝试重新连接：成功即跳出，否则下一次循环会把刚建立好的连接踹断
                            for (int i = 0; i < 3 && !IsConnected && !token.IsCancellationRequested; i++)
                            {
                                try
                                {
                                    _comm.ConnectServer();
                                    if (IsConnected)
                                        break;
                                }
                                catch
                                {
                                    // 单次重连失败，继续下一次
                                }
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
