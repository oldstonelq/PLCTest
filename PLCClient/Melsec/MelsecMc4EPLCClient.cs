using PLCTest.ClientProtocolManagement.Melsec;
using PLCTest.Models;
using PLCTest.PLCInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static PLCTest.Models.Enums;

namespace PLCTest.PLCClient.Melsec
{
    public class MelsecMc4EPLCClient : IPLCClient
    {
        /// <summary>
        /// 通讯方式接口
        /// </summary>
        protected IClientCommunication _comm;
        /// <summary>
        /// 通讯协议接口
        /// </summary>
        protected IPLCClientProtocol communicationProtocol;
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
        public MelsecMc4EPLCClient(IClientCommunication comm)
        {
            _comm = comm;
            communicationProtocol = new MelsecMc4EBinaryClientProtocol();
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
                        _comm.DisConnectServer();
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
        /// 读取位数据
        /// </summary>
        /// <param name="area">区域</param>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public Result<bool> ReadBit(MemoryArea area, int address)
        {
            Result<bool> result = new Result<bool>();
            //1.校验软元件类型
            if (area == MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //完整的报文  数据头+实际数据
            //2.拼接数据报文
            //3.构建完整报文
            //4.发送报告并接收返回数据
            byte[] BuildReadData = communicationProtocol.BuildReadData((byte)area, address, 1, true);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildReadData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //5.数据校验与解析
            var res = communicationProtocol.ParseReadBitResponse(mReByte, 1);
            result.IsSuccess = res.IsSuccess;
            result.ErrorMsg = res.ErrorMsg;
            result.Data = res.Data != null && res.Data.Length > 0 ? res.Data[0] : false;

            return result;
        }
        /// <summary>
        /// 读取位数组数据
        /// </summary>
        /// <param name="area"></param>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public Result<bool[]> ReadBitArray(MemoryArea area, int address, int size)
        {
            Result<bool[]> result = new Result<bool[]>();
            //1.校验软元件类型
            if (area == MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //完整的报文  数据头+实际数据
            //2.拼接数据报文
            //3.构建完整报文
            //4.发送报告并接收返回数据
            byte[] BuildReadData = communicationProtocol.BuildReadData((byte)area, address, size, true);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildReadData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //5.数据校验与解析
            result = communicationProtocol.ParseReadBitResponse(mReByte, size);

            return result;
        }
        /// <summary>
        /// 写入位数据
        /// </summary>
        /// <param name="area"></param>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public Result<bool> WriteBit(MemoryArea area, int address, bool value)
        {
            Result<bool> result = new Result<bool>();
            //1.校验软元件类型
            if (area == MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.计算数据
            byte[] mData = new byte[1];
            // 第 2*i 位 → 高4位 0x10
            if (value)
            {
                mData[0] += 0x10;
            }
            //完整的报文  数据头+实际数据
            //3.拼接数据报文
            //4.构建完整报文
            //5.发送报告并接收返回数据
            byte[] BuildWriteData = communicationProtocol.BuildWriteData((byte)area, address, 1, true, mData);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildWriteData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //6.数据校验与解析
            result = communicationProtocol.ParseWriteResponse(mReByte);

            return result;
        }
        /// <summary>
        /// 写入位数组数据
        /// </summary>
        /// <param name="area"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public Result<bool> WriteBitArray(MemoryArea area, int address, bool[] values)
        {
            Result<bool> result = new Result<bool>();
            //1.检查写入数据是否为空， 
            if (values == null || values.Length == 0)
            {
                result.ErrorMsg = "写入数据为空";
                return result;
            }
            //2.校验软元件类型
            if (area == MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //3.计算打包后字节长度：每2个bit封装1个byte，向上取整
            int mLength = (values.Length + 1) / 2;
            byte[] mData = new byte[mLength];

            for (int i = 0; i < mLength; i++)
            {
                // 第 2*i 位 → 高4位 0x10
                if (values[i * 2])
                {
                    mData[i] += 0x10;
                }
                // 判断是否存在下一组bit
                int secondBitIndex = i * 2 + 1;
                if (secondBitIndex < values.Length)
                {
                    // 第 2*i+1 位 → 低4位 0x01
                    if (values[secondBitIndex])
                    {
                        mData[i] += 0x01;
                    }
                }
            }
            //完整的报文  数据头+实际数据
            //4.拼接数据报文
            //5.构建完整报文
            //6.发送报告并接收返回数据
            byte[] BuildWriteData = communicationProtocol.BuildWriteData((byte)area, address, values.Length, true, mData);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildWriteData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //7.数据校验与解析
            result = communicationProtocol.ParseWriteResponse(mReByte);
            return result;
        }
        /// <summary>
        /// 读取字数据
        /// </summary>
        /// <param name="area"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Result<short> ReadWord(MemoryArea area, int address)
        {
            Result<short> result = new Result<short>();
            //1.校验软元件类型
            if (area != MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //完整的报文  数据头+实际数据
            //2.拼接数据报文
            //3.构建完整报文
            //4.发送报告并接收返回数据
            byte[] BuildReadData = communicationProtocol.BuildReadData((byte)area, address, 1, false);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildReadData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //5.数据校验与解析
            var res = communicationProtocol.ParseReadWordResponse(mReByte, 1);
            result.IsSuccess = res.IsSuccess;
            result.ErrorMsg = res.ErrorMsg;
            result.Data = res.Data != null && res.Data.Length > 0 ? res.Data[0] : (short)0;
            return result;
        }
        /// <summary>
        /// 读取字数组数据
        /// </summary>
        /// <param name="area"></param>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Result<short[]> ReadWordArray(MemoryArea area, int address, int size)
        {
            Result<short[]> result = new Result<short[]>();
            //1.校验软元件类型
            if (area != MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //完整的报文  数据头+实际数据
            //2.拼接数据报文
            //3.构建完整报文
            //4.发送报告并接收返回数据
            byte[] BuildReadData = communicationProtocol.BuildReadData((byte)area, address, size, false);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildReadData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //5.数据校验与解析
            result = communicationProtocol.ParseReadWordResponse(mReByte, size);
            return result;
        }
        /// <summary>
        /// 写入字数据
        /// </summary>
        /// <param name="area"></param>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Result<bool> WriteWord(MemoryArea area, int address, short value)
        {
            Result<bool> result = new Result<bool>();
            //1.校验软元件类型
            if (area != MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //2.计算打包后字节长度：每个short占2个字节
            byte[] mData = new byte[2];
            for (int i = 0; i < 1; i++)
            {
                byte[] mbyte = BitConverter.GetBytes(value);
                mbyte.CopyTo(mData, i * 2);
            }
            //完整的报文  数据头+实际数据
            //3.拼接数据报文
            //4.构建完整报文
            //5.发送报告并接收返回数据
            byte[] BuildWriteData = communicationProtocol.BuildWriteData((byte)area, address, 1, false, mData);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildWriteData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //6.数据校验与解析
            result = communicationProtocol.ParseWriteResponse(mReByte);
            return result;
        }
        /// <summary>
        /// 写入字数组数据
        /// </summary>
        /// <param name="area"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Result<bool> WriteWordArray(MemoryArea area, int address, short[] values)
        {
            Result<bool> result = new Result<bool>();
            //1.检查写入数据是否为空， 
            if (values == null || values.Length == 0)
            {
                result.ErrorMsg = "写入数据为空";
                return result;
            }
            //2.校验软元件类型
            if (area != MemoryArea.D)
            {
                result.ErrorMsg = "软元件类型错误";
                return result;
            }
            //3.计算打包后字节长度：每个short占2个字节
            byte[] mData = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                byte[] mbyte = BitConverter.GetBytes(values[i]);
                mbyte.CopyTo(mData, i * 2);
            }
            //完整的报文  数据头+实际数据
            //4.拼接数据报文
            //3.构建完整报文
            //4.发送报告并接收返回数据
            byte[] BuildWriteData = communicationProtocol.BuildWriteData((byte)area, address, values.Length, false, mData);
            byte[] BuildCompletePack = communicationProtocol.BuildCompletePack(BuildWriteData);
            byte[] mReByte = _comm.SendAndRecevieData(BuildCompletePack);
            //5.数据校验与解析
            result = communicationProtocol.ParseWriteResponse(mReByte);
            return result;
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 私有心跳循环方法，用于保持与PLC的连接状态
        /// </summary>
        /// <param name="token"></param>
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
