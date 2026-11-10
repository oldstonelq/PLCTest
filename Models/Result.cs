using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Models
{
    /// <summary>
    /// 失败类型：调用方据此决定"重连 / 提示用户 / 交给上层"，不必再解析 ErrorMsg 字符串。
    /// </summary>
    public enum ErrorKind
    {
        /// <summary>未失败，或失败原因未归类</summary>
        None = 0,
        /// <summary>入参校验失败（地址为空、区域不支持位/字访问、长度非法等），报文没有发出</summary>
        Validation = 1,
        /// <summary>通讯层失败（未连接、超时、对端断开）</summary>
        Transport = 2,
        /// <summary>协议层失败（拼包/解析异常、报文格式不符）</summary>
        Protocol = 3,
        /// <summary>设备回应了非零结束码（PLC 侧拒绝执行）</summary>
        DeviceEndCode = 4,
    }

    /// <summary>
    /// 返回结果类
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class Result<T>
    {
        /// <summary>
        /// 成功标志 True:成功 False:失败
        /// </summary>
        public bool IsSuccess { get; set; }
        /// <summary>
        /// 错误信息 
        /// </summary>
        public string ErrorMsg { get; set; }
        /// <summary>
        /// 失败类型
        /// </summary>
        public ErrorKind Kind { get; set; }
        /// <summary>
        /// 设备结束码（仅 <see cref="ErrorKind.DeviceEndCode"/> 时有值）
        /// </summary>
        public ushort? EndCode { get; set; }
        /// <summary>
        /// 返回数据
        /// </summary>
        public T Data { get; set; }
        /// <summary>
        /// 返回默认成功结果
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Result<T> Success(T data)
            => new Result<T> { IsSuccess = true, Data = data };
        /// <summary>
        /// 返回默认失败结果
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static Result<T> Fail(string msg)
            => new Result<T> { IsSuccess = false, ErrorMsg = msg };
        /// <summary>
        /// 返回带失败类型的失败结果
        /// </summary>
        /// <param name="msg">错误信息</param>
        /// <param name="kind">失败类型</param>
        /// <returns></returns>
        public static Result<T> Fail(string msg, ErrorKind kind)
            => new Result<T> { IsSuccess = false, ErrorMsg = msg, Kind = kind };
        /// <summary>
        /// 返回"设备结束码非零"的失败结果
        /// </summary>
        /// <param name="msg">错误信息</param>
        /// <param name="endCode">设备结束码</param>
        /// <returns></returns>
        public static Result<T> DeviceFail(string msg, ushort endCode)
            => new Result<T> { IsSuccess = false, ErrorMsg = msg, Kind = ErrorKind.DeviceEndCode, EndCode = endCode };
    }
}
