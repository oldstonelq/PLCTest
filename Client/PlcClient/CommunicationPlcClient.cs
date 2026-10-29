using PlcTest.Client.ClientInterface;
using PlcTest.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlcTest.Client.PlcClient
{
    /// <summary>
    /// PLC 客户端通用实现：持有通讯对象与协议对象，负责连接/断开、心跳重连、
    /// 8 个读写的“拼包 → 发送 → 解析”骨架，以及把通讯/协议异常收敛为失败 Result。
    /// 厂商差异通过组合注入：通讯方式 + 协议实现；位/字能否访问由协议声明的
    /// <see cref="IPlcClientProtocol.GetAreaAccess"/> 决定，客户端不再自带第二套规则。
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
        /// 当前协议支持的软元件区域（界面只展示这些，避免出现这台 PLC 上不存在的软元件）。
        /// </summary>
        /// <returns>受支持的区域，按枚举声明顺序</returns>
        public IReadOnlyList<MemoryArea> GetSupportedAreas()
        {
            var areas = new List<MemoryArea>();
            foreach (MemoryArea area in Enum.GetValues(typeof(MemoryArea)))
            {
                AreaAccess access;
                if (TryGetAreaAccess(area, out access))
                {
                    areas.Add(area);
                }
            }
            return areas;
        }

        /// <summary>
        /// 该区域在当前协议下允许的访问方式。
        /// </summary>
        /// <param name="area">软元件区域</param>
        /// <returns>允许的访问方式</returns>
        public AreaAccess GetAreaAccess(MemoryArea area)
        {
            if (_communicationProtocol == null)
            {
                throw new InvalidOperationException("协议未初始化，无法查询软元件区域");
            }
            return _communicationProtocol.GetAreaAccess(area);
        }

        /// <summary>
        /// 探测协议是否认识该区域——各协议支持的软元件范围不同，认不出的会抛异常。
        /// </summary>
        private bool TryGetAreaAccess(MemoryArea area, out AreaAccess access)
        {
            access = AreaAccess.None;
            if (_communicationProtocol == null)
            {
                return false;
            }
            try
            {
                access = _communicationProtocol.GetAreaAccess(area);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">通讯方式</param>
        /// <param name="protocol">协议实现</param>
        public CommunicationPlcClient(IClientCommunication comm, IPlcClientProtocol protocol)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
            _communicationProtocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        }

        #region 公有方法
        /// <summary>
        /// 连接方法：同步建立连接，成功后再启动后台心跳（断线自动重连）。
        /// </summary>
        /// <returns>连接结果；失败时 ErrorMsg 为具体原因（超时、IP 错误等）</returns>
        public Result<bool> Connect()
        {
            // 1. 心跳任务已在运行，说明已经连接或正在维持连接，直接返回
            if (_connectTask != null && !_connectTask.IsCompleted)
            {
                return Result<bool>.Success(true);
            }
            if (IsConnected)
            {
                StartHeartBeat();
                return Result<bool>.Success(true);
            }

            // 2. 同步建连：失败原因直接返回给调用方，不再靠后台线程静默重试
            try
            {
                _comm.ConnectServer();
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail("连接失败: " + ex.Message);
            }

            if (!IsConnected)
            {
                return Result<bool>.Fail("连接失败: 通讯对象未能建立连接");
            }

            // 3. 建连成功后再起心跳
            StartHeartBeat();
            return Result<bool>.Success(true);
        }
        /// <summary>
        /// 断开连接方法：停止心跳并关闭底层连接，保留通讯对象，之后仍可再次 Connect()。
        /// 不再阻塞等待心跳线程退出（最坏要等 1 s），而是让心跳线程自己看令牌收尾。
        /// </summary>
        public void Disconnect()
        {
            try
            {
                //1.取消心跳
                _cts?.Cancel();

                //2.关闭底层连接（会让心跳线程在下一轮检查时自行退出）
                try
                {
                    _comm?.DisconnectServer();
                }
                catch { }
            }
            finally
            {
                //3.释放令牌资源。_comm 必须保留，否则再次 Connect() 时心跳拿不到通讯对象
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
                //1.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Bit);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, true);
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    result.Kind = ErrorKind.Transport;
                    return result;
                }
                //3.数据校验与解析
                var res = _communicationProtocol.ParseReadBitResponse(receiveBytes, 1);
                result.IsSuccess = res.IsSuccess;
                result.ErrorMsg = res.ErrorMsg;
                result.Kind = res.Kind;
                result.EndCode = res.EndCode;
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
                //1.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Bit);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, true);
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    result.Kind = ErrorKind.Transport;
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
                //1.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Bit);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, true);
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
                    result.Kind = ErrorKind.Transport;
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
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                //2.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Bit);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, true);
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
                    result.Kind = ErrorKind.Transport;
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
                //1.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Word);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, false);
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    result.Kind = ErrorKind.Transport;
                    return result;
                }
                //3.数据校验与解析
                var res = _communicationProtocol.ParseReadWordResponse(receiveBytes, 1);

                result.IsSuccess = res.IsSuccess;
                result.ErrorMsg = res.ErrorMsg;
                result.Kind = res.Kind;
                result.EndCode = res.EndCode;
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
                //1.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Word);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, false);
                //2.拼接数据报文并构建完整报文
                byte[] BuildReadData = _communicationProtocol.BuildReadData(address);
                byte[] BuildCompletePack = _communicationProtocol.BuildCompletePack(BuildReadData);
                byte[] receiveBytes;
                string commError;
                if (!TrySendAndReceive(BuildCompletePack, out receiveBytes, out commError))
                {
                    result.ErrorMsg = commError;
                    result.Kind = ErrorKind.Transport;
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
                //1.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Word);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, false);
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
                    result.Kind = ErrorKind.Transport;
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
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                //2.校验地址与区域访问能力
                string addressError = CheckAddress(address, AreaAccess.Word);
                if (addressError != null)
                {
                    result.ErrorMsg = addressError;
                    result.Kind = ErrorKind.Validation;
                    return result;
                }
                address = Normalize(address, false);
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
                    result.Kind = ErrorKind.Transport;
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
        /// 校验地址是否可用于本次操作：地址非空、长度为正、区域支持所需的访问方式。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="needed">本次操作需要的访问方式</param>
        /// <returns>null 表示校验通过，否则为错误信息</returns>
        private string CheckAddress(PlcAddress address, AreaAccess needed)
        {
            if (address == null)
            {
                return "地址不能为空";
            }
            if (address.Length <= 0)
            {
                return "操作长度必须大于 0";
            }

            AreaAccess allowed;
            try
            {
                allowed = _communicationProtocol.GetAreaAccess(address.Area);
            }
            catch (Exception ex)
            {
                // 协议不认识该区域（各协议支持的软元件范围不同）
                return "不支持的软元件区域: " + ex.Message;
            }

            if ((allowed & needed) == 0)
            {
                string kind = needed == AreaAccess.Bit ? "位" : "字";
                return $"软元件区域 {address.Area} 不支持{kind}访问";
            }

            // 编号范围：协议只声明该区域的最大编号（-1 表示不限制），
            // 越界判断（起始点、连续访问的末点）统一在这里算，越界就直接拦下，不发给 PLC。
            // 这样每个协议只需提供一个数字，不必各写一遍同样的校验逻辑。
            int maxIndex = _communicationProtocol.GetMaxIndex(address.Area);
            if (maxIndex >= 0)
            {
                if (address.Address > maxIndex)
                {
                    return $"软元件区域 {address.Area} 起始编号 {address.Address} 超出允许范围 0~{maxIndex}";
                }

                int lastIndex = address.Address + Math.Max(address.Length, 1) - 1;
                if (lastIndex > maxIndex)
                {
                    return $"软元件区域 {address.Area} 连续访问到编号 {lastIndex}，超出允许范围 0~{maxIndex}";
                }
            }

            return null;
        }

        /// <summary>
        /// 按本次操作的语义归一化地址的位/字标志。
        /// 读位就按位拼包、读字就按字拼包，避免"调用方传的 IsBit"与"协议拼包用的 IsBit"打架。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="isBit">本次操作是否为位操作</param>
        /// <returns>位/字标志已校正的地址（原对象未修改）</returns>
        private static PlcAddress Normalize(PlcAddress address, bool isBit)
        {
            if (address.IsBit == isBit)
            {
                return address;
            }
            return new PlcAddress(address.Area, address.Address, isBit, address.DbNumber, address.Length);
        }

        /// <summary>
        /// 构造一个“协议异常”失败结果，统一协议层拼包/解析抛出的异常信息
        /// </summary>
        private static Result<T> ProtocolError<T>(string message)
        {
            return Result<T>.Fail("协议异常: " + message, ErrorKind.Protocol);
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
                // 由协议决定"这一帧多长"，通讯层据此读满，不再靠静默超时猜帧尾
                response = _comm.SendAndReceiveData(pack, _communicationProtocol.GetResponseFrameLength);
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
        /// 启动心跳任务（内部使用；调用方应通过 <see cref="Connect"/> 触发）
        /// </summary>
        private void StartHeartBeat()
        {
            if (_connectTask != null && !_connectTask.IsCompleted)
            {
                return;
            }
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            _connectTask = Task.Run(() => HeartBeatLoop(token), token);
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
                            await Task.Delay(1000, token).ConfigureAwait(false);
                            continue;
                        }

                        // 未连接，尝试重新连接：成功即跳出，否则下一次循环会把刚建立好的连接踹断
                        for (int i = 0; i < 3 && !IsConnected && !token.IsCancellationRequested; i++)
                        {
                            try
                            {
                                _comm.ConnectServer();

                                // 建连过程中被要求断开：把刚建立的连接关掉再退出，避免留下"已连上但没人管"的状态
                                if (token.IsCancellationRequested)
                                {
                                    try { _comm.DisconnectServer(); } catch { }
                                    return;
                                }

                                if (IsConnected)
                                    break;
                            }
                            catch
                            {
                                // 单次重连失败，继续下一次
                            }
                        }

                        await Task.Delay(1000, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
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
