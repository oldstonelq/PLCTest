using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Models
{
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
    }
}
