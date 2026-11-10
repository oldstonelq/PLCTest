using PlcTest.Client.ClientInterface;
using System;
using System.Threading;

namespace PlcTest.Client.ClientCommunication
{
    /// <summary>
    /// 一帧响应的组装策略：先按协议给出的帧长读满，协议判不出长度时才回退到空闲判据。
    /// <para>
    /// TCP 与串口的差异（阻塞读、可读字节数、异常文案）由 <see cref="IFrameTransport"/> 承担，
    /// 这里只保留「怎么凑齐一帧」这一份实现——两端各写一遍时，改一处漏一处就会变成
    /// 「TCP 正常、串口卡住」这类只在现场才暴露的问题。
    /// </para>
    /// </summary>
    internal static class ResponseFrameAssembler
    {
        /// <summary>
        /// 接收缓冲区最大长度（PLC 报文 4 KB 足够）
        /// </summary>
        internal const int MaxBufferLength = 4096;

        /// <summary>
        /// 拼接后续分片时的轮询间隔 ms
        /// </summary>
        private const int IdleWaitMs = 20;

        /// <summary>
        /// 连续多少次没有新数据即认为一帧已收全（20 ms × 3 = 60 ms 兜底）
        /// </summary>
        private const int MaxIdleRounds = 3;

        /// <summary>
        /// 读取一帧完整响应。
        /// </summary>
        /// <param name="request">已下发的请求报文，供帧长解析器判断响应形态</param>
        /// <param name="resolver">响应帧长度解析器；为 null 或返回非法值时回退空闲判据</param>
        /// <param name="transport">具体传输层（TCP / 串口）</param>
        /// <returns>已按帧长截断的响应报文</returns>
        internal static byte[] ReadFrame(byte[] request, ResponseFrameLengthResolver resolver, IFrameTransport transport)
        {
            byte[] buffer = new byte[MaxBufferLength];

            // 首段阻塞读取：一条一问一答的响应至少要有 1 字节，读不到说明设备没回
            int total = transport.Read(buffer, 0, buffer.Length, true);
            if (total <= 0)
            {
                throw new Exception(transport.EmptyResponseMessage);
            }

            int frameLength = ResolveFrameLength(resolver, request, buffer, total);

            if (frameLength > 0)
            {
                // 按协议长度读满，不再靠「静默 60 ms」猜
                while (total < frameLength)
                {
                    int read = transport.Read(buffer, total, frameLength - total, false);
                    if (read <= 0)
                    {
                        throw new Exception(transport.EmptyResponseMessage);
                    }

                    total += read;
                }

                // 一问一答模式下不应多收；多出来的一律截掉，避免污染解析
                if (total > frameLength)
                {
                    total = frameLength;
                }
            }
            else
            {
                // 回退：连续 MaxIdleRounds 次没有新数据即认为一帧收全
                int idleRounds = 0;
                while (total < buffer.Length && idleRounds < MaxIdleRounds)
                {
                    if (transport.HasPendingData)
                    {
                        int read = transport.Read(buffer, total, buffer.Length - total, false);
                        if (read <= 0)
                        {
                            break;
                        }

                        total += read;
                        idleRounds = 0;
                        continue;
                    }

                    Thread.Sleep(IdleWaitMs);
                    idleRounds++;
                }
            }

            byte[] result = new byte[total];
            Array.Copy(buffer, result, total);
            return result;
        }

        /// <summary>
        /// 解析帧长：解析器缺失、抛异常，或给出的长度超过缓冲区，都视为「判不出」（返回 -1）。
        /// </summary>
        private static int ResolveFrameLength(ResponseFrameLengthResolver resolver, byte[] request, byte[] buffer, int available)
        {
            if (resolver == null)
            {
                return -1;
            }

            int frameLength;
            try
            {
                frameLength = resolver(request, buffer, available);
            }
            catch
            {
                // 头部不足时解析器可能越界抛异常，交回空闲判据兜底即可
                return -1;
            }

            // 协议给出的长度超出缓冲上限，视为判定不可信
            return frameLength > buffer.Length ? -1 : frameLength;
        }
    }

    /// <summary>
    /// 帧组装所需的传输层能力：阻塞读一段、是否还有待读字节、读不到数据时的提示文案。
    /// <para>
    /// 实现方负责把底层异常（IOException / TimeoutException / InvalidOperationException）
    /// 翻译成面向用户的中文提示，组装器不关心底层是 TCP 还是串口。
    /// </para>
    /// </summary>
    internal interface IFrameTransport
    {
        /// <summary>
        /// 阻塞读取一段数据。
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">写入起点</param>
        /// <param name="count">最多读取字节数</param>
        /// <param name="firstRead">是否为本帧的首段读取；用来区分「设备没响应」与「帧未收全」两种提示</param>
        /// <returns>实际读取字节数；返回 0 表示对端已断开</returns>
        int Read(byte[] buffer, int offset, int count, bool firstRead);

        /// <summary>
        /// 当前是否已有可立即读取的数据（非阻塞探询，用于空闲判据）
        /// </summary>
        bool HasPendingData { get; }

        /// <summary>
        /// 读不到数据时的提示文案
        /// </summary>
        string EmptyResponseMessage { get; }
    }
}
