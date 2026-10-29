using PlcTest.Client.ClientProtocolManagement.Omron;
using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlcTest.Client.PlcClient.Omron
{
    /// <summary>
    /// 欧姆龙PLC客户端类，使用欧姆龙协议进行通讯
    /// </summary>
    public class OmronPlcClient : IPlcClient
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
        /// <param name="protocol">协议实现（可选），若为 null 则使用默认的 OmronFinsTcpClientProtocol</param>
        public OmronPlcClient(IClientCommunication comm, IPlcClientProtocol protocol = null)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
            // 使用构造注入替代硬编码，便于单元测试与替换协议实现
            _communicationProtocol = protocol ?? new OmronFinsTcpClientProtocol();
        }
        #region 公有方法
        /// <summary>
        /// 连接方法
        /// </summary>
        public Result<bool> Connect()
        {
            // 1. 先判断：心跳任务正在运行，直接返回，禁止重复启动
            if (_connectTask != null && !_connectTask.IsCompleted)
            {
                return Result<bool>.Success(true);
            }
            // 2. 同步建连，失败原因返回给调用方
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
            // 3. 清理上一次残留的令牌源，启动心跳循环
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
            _cts = new CancellationTokenSource();
            _connectTask = Task.Run(() => HeartBeatLoop(_cts.Token), _cts.Token);
            return Result<bool>.Success(true);
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
            throw new NotImplementedException();
        }
        /// <summary>
        /// 读取位数组数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<bool[]> ReadBitArray(PlcAddress address)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 写入单个位数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="value">写入值</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteBit(PlcAddress address, bool value)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 写入位数组数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="values">位数据数组</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteBitArray(PlcAddress address, bool[] values)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 读取单个字数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<short> ReadWord(PlcAddress address)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 读取字数组数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <returns>读取结果。</returns>
        public Result<short[]> ReadWordArray(PlcAddress address)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 写入单个字数据。
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="value">写入值</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteWord(PlcAddress address, short value)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 写入字数组数据
        /// </summary>
        /// <param name="address">统一地址模型</param>
        /// <param name="values">字数据数组</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteWordArray(PlcAddress address, short[] values)
        {
            throw new NotImplementedException();
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
