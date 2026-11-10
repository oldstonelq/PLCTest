using System;

namespace PlcTest.Models
{
    /// <summary>
    /// 一条通讯日志的方向。
    /// </summary>
    public enum CommFrameDirection
    {
        /// <summary>本端发出</summary>
        Send = 0,
        /// <summary>本端收到</summary>
        Receive = 1,
        /// <summary>纯文本提示（启动、丢弃、异常原因等），没有报文</summary>
        Note = 2,
    }

    /// <summary>
    /// 一条结构化的通讯日志：时间 + 方向 + 原始报文 + 说明。
    /// <para>
    /// 为什么不再用字符串：旧事件 <c>ShowCommMessageEvent</c> 只给一句拼好的文本
    /// （"发送报文: 01-FF-…"），界面要按方向着色、要一列 HEX 一列 ASCII、要复制原始帧，
    /// 就只能去解析这行文本——等于把日志格式当成协议来逆向。这里把方向和字节直接带出来。
    /// </para>
    /// <para>
    /// 兼容性：<see cref="ToLogText"/> 生成的文本与重构前**逐字相同**，
    /// 所以旧的字符串事件继续原样触发，老调用方和既有测试不受影响。
    /// </para>
    /// </summary>
    public sealed class CommFrame
    {
        /// <summary>发送方向的默认说明文本</summary>
        public const string SendCaption = "发送报文";
        /// <summary>接收方向的默认说明文本</summary>
        public const string ReceiveCaption = "接收报文";

        /// <summary>方向</summary>
        public CommFrameDirection Direction { get; private set; }

        /// <summary>产生这条日志的时间（本地时间）</summary>
        public DateTime Time { get; private set; }

        /// <summary>原始报文；纯文本提示时为空数组，永不为 null</summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// 说明文本。收发帧在未特殊指定时为「发送报文」/「接收报文」，
        /// 也可以写成「节点地址请求」「报文非法，已丢弃」这类更具体的描述。
        /// </summary>
        public string Caption { get; private set; }

        /// <summary>是否带报文（纯文本提示为 false）</summary>
        public bool HasData
        {
            get { return Data.Length > 0; }
        }

        /// <summary>是否是一条真正的收发帧（不是提示文本）</summary>
        public bool IsTraffic
        {
            get { return Direction != CommFrameDirection.Note; }
        }

        private CommFrame(CommFrameDirection direction, byte[] data, string caption)
        {
            Direction = direction;
            Time = DateTime.Now;
            Data = data ?? new byte[0];
            Caption = string.IsNullOrEmpty(caption)
                ? (direction == CommFrameDirection.Send ? SendCaption
                    : direction == CommFrameDirection.Receive ? ReceiveCaption
                    : string.Empty)
                : caption;
        }

        /// <summary>本端发出的报文</summary>
        /// <param name="data">完整请求报文</param>
        /// <param name="caption">说明，默认「发送报文」</param>
        public static CommFrame Send(byte[] data, string caption = null)
        {
            return new CommFrame(CommFrameDirection.Send, data, caption);
        }

        /// <summary>本端收到的报文</summary>
        /// <param name="data">完整响应报文</param>
        /// <param name="caption">说明，默认「接收报文」</param>
        public static CommFrame Receive(byte[] data, string caption = null)
        {
            return new CommFrame(CommFrameDirection.Receive, data, caption);
        }

        /// <summary>纯文本提示</summary>
        /// <param name="message">提示内容</param>
        public static CommFrame Note(string message)
        {
            return new CommFrame(CommFrameDirection.Note, null, message);
        }

        /// <summary>
        /// 旧格式的报文文本：带连字符的十六进制，如 <c>01-FF-0A</c>。
        /// 与 <see cref="BitConverter.ToString(byte[])"/> 的输出一致。
        /// </summary>
        public string HexWithDash
        {
            get { return Data.Length == 0 ? string.Empty : BitConverter.ToString(Data); }
        }

        /// <summary>
        /// 界面上用的报文文本：空格分隔，如 <c>01 FF 0A</c>。字节多时比连字符好读。
        /// </summary>
        public string HexSpaced
        {
            get { return Data.Length == 0 ? string.Empty : BitConverter.ToString(Data).Replace('-', ' '); }
        }

        /// <summary>
        /// 拼成旧事件使用的那一行文本，保证与重构前逐字一致：
        /// 收发帧为「说明: XX-XX」，提示为原文。
        /// </summary>
        public string ToLogText()
        {
            if (!HasData)
            {
                return Caption;
            }
            return Caption + ": " + HexWithDash;
        }

        /// <summary>调试用</summary>
        public override string ToString()
        {
            return Time.ToString("HH:mm:ss.fff") + " " + Direction + " " + ToLogText();
        }
    }
}
