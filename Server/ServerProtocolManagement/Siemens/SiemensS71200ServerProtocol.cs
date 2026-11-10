using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Generic;

namespace PlcTest.Server.ServerProtocolManagement.Siemens
{
    /// <summary>
    /// 西门子 S7 服务端协议实现：TPKT + COTP + S7 头 + 参数区 + 数据项。
    /// <para>
    /// 设计上**无状态**：每一帧自己就能判断类型并应答，不需要记住「这个连接握手到哪一步」。
    /// 因此也不用给 <see cref="IClientSession"/> 挂 Tag，更不用在服务端维护会话字典
    /// （通讯层没有「客户端断开」事件，字典建了也回收不掉）。
    /// </para>
    /// <para>
    /// 常量与偏移全部对齐客户端的 <c>SiemensS71200ClientProtocol</c>：
    /// 请求头 10 字节（参数区从 17 开始），Ack-Data 头 12 字节（参数区从 19 开始）——
    /// 这个「响应头比请求头多 2 字节」的地方曾经害得真机读写全解析失败，服务端必须同样严格。
    /// </para>
    /// <para>
    /// **多变量**：参数区是 <c>功能码 | 项数 | N × 12 字节 ANY 指针</c>，单变量只是 N=1 的特例，
    /// 不需要两套解析。响应里每项一个数据项（读）或 1 字节返回码（写），
    /// 某一项失败只影响该项——真实 PLC 不会因为第二个地址越界就把第一个地址的数据也丢掉。
    /// </para>
    /// </summary>
    public class SiemensS71200ServerProtocol : IS7ServerProtocol
    {
        /// <summary>TPKT 头：版本 / 预留 / 总长（2 字节）</summary>
        private const int TpktHeaderLength = 4;
        /// <summary>COTP 数据头：长度 / PDU 类型 / 引用（2 字节）</summary>
        private const int CotpDataHeaderLength = 3;
        /// <summary>TPKT + COTP 的长度，S7 头从这里开始</summary>
        private const int S7HeaderOffset = TpktHeaderLength + CotpDataHeaderLength;
        /// <summary>作业（Job）头 10 字节</summary>
        private const int S7JobHeaderLength = 10;
        /// <summary>Ack-Data 头 12 字节：比 Job 多 [错误类][错误码]</summary>
        private const int S7AckDataHeaderLength = 12;
        /// <summary>Job 的参数区起点</summary>
        private const int S7JobParamOffset = S7HeaderOffset + S7JobHeaderLength;
        /// <summary>Ack-Data 的参数区起点</summary>
        private const int S7AckDataParamOffset = S7HeaderOffset + S7AckDataHeaderLength;
        /// <summary>参数区头：功能码 1 字节 + 项数 1 字节</summary>
        private const int JobParamHeaderLength = 2;
        /// <summary>一个变量项（ANY 指针）12 字节</summary>
        private const int ItemLength = 12;
        /// <summary>写请求数据区里每项的头：预留 1 + 类型 1 + 位长度 2</summary>
        private const int WriteItemHeaderLength = 4;
        /// <summary>服务端愿意协商的最大 PDU</summary>
        private const int MaxPduLength = 960;

        /// <summary>传输类型：位（元素数的单位是位）</summary>
        private const byte TransportBit = 0x01;
        /// <summary>传输类型：字节（元素数的单位是字节，字读要填字数 × 2）</summary>
        private const byte TransportByte = 0x02;
        /// <summary>数据项返回码：成功</summary>
        private const byte DataItemOk = 0xFF;
        /// <summary>数据项返回码：地址越界</summary>
        private const byte DataItemAddressOutOfRange = 0x05;
        /// <summary>数据项传输尺寸：位</summary>
        private const byte DataItemBit = 0x03;
        /// <summary>数据项传输尺寸：字节/字/双字（长度字段按位算）</summary>
        private const byte DataItemByteWord = 0x04;

        /// <summary>
        /// 本服务端模拟的区域。DB 区按块号惰性分配存储，块号取自请求参数区，
        /// 所以 DB1.DBW0 与 DB2.DBW0 是两块独立内存。
        /// </summary>
        public IReadOnlyList<MemoryArea> GetSupportedAreas()
        {
            return new[] { MemoryArea.X, MemoryArea.Y, MemoryArea.M, MemoryArea.DB };
        }

        /// <summary>
        /// TPKT 的长度字段就在前 4 字节，比 MC 的「按位置猜」简单得多。
        /// </summary>
        public int GetRequestFrameLength(byte[] buffer, int available)
        {
            if (buffer == null || available < TpktHeaderLength)
            {
                return 0;
            }

            int length = (buffer[2] << 8) | buffer[3];
            return length >= S7HeaderOffset ? length : 0;
        }

        /// <summary>
        /// 分类顺序：先看 COTP PDU 类型（CR 是 0xE0，DT 是 0xF0），再看 S7 功能码。
        /// 不能只看功能码——COTP 连接请求里根本没有 S7 头。
        /// </summary>
        public S7RequestKind ClassifyRequest(byte[] data)
        {
            if (data == null || data.Length < 7 || data[0] != 0x03)
            {
                return S7RequestKind.Unknown;
            }

            if (data[5] == 0xE0)
            {
                return S7RequestKind.CotpConnect;
            }

            // COTP DT（0xF0）之后才是 S7 头，功能码在参数区第一个字节
            if (data.Length <= S7JobParamOffset || data[S7HeaderOffset] != 0x32)
            {
                return S7RequestKind.Unknown;
            }

            switch (data[S7JobParamOffset])
            {
                case 0xF0:
                    return S7RequestKind.SetupCommunication;
                case 0x04:
                    return S7RequestKind.ReadVar;
                case 0x05:
                    return S7RequestKind.WriteVar;
                default:
                    return S7RequestKind.Unknown;
            }
        }

        /// <summary>
        /// COTP 连接确认：目标引用回显请求的源引用（客户端会据此判断「这是不是给这条连接的应答」），
        /// 参数区（C1/C2/C0）原样带回，让客户端看到的 TSAP 与它发的一致。
        /// </summary>
        public byte[] BuildCotpConnectResponse(byte[] request)
        {
            if (request == null || request.Length < 11)
            {
                return new byte[0];
            }

            int paramLength = request.Length - 11;
            // CC 没有 class 字节：长度 + PDU 类型 + 目标引用 + 源引用 + 参数
            int cotpLength = 5 + paramLength;
            int total = TpktHeaderLength + 6 + paramLength;

            byte[] frame = new byte[total];
            frame[0] = 0x03;
            frame[1] = 0x00;
            frame[2] = (byte)(total >> 8);
            frame[3] = (byte)(total & 0xFF);
            frame[4] = (byte)cotpLength;
            frame[5] = 0xD0;
            // 目标引用 = 请求的源引用（CR 里 [8..9]）
            frame[6] = request[8];
            frame[7] = request[9];
            // 服务端自己的源引用
            frame[8] = 0x00;
            frame[9] = 0x01;
            Array.Copy(request, 11, frame, 10, paramLength);
            return frame;
        }

        /// <summary>
        /// Setup Communication 应答：固定 27 字节（参数 8、数据 0），PDU 长度回显请求值。
        /// 客户端的 <c>ParseSetupCommunicationResponse</c> 会核对「参数长度 8 + 数据长度 0 + 总长 27」
        /// 三者必须自洽，所以这里不能多也不能少。
        /// </summary>
        public byte[] BuildSetupCommunicationResponse(byte[] request)
        {
            if (request == null || request.Length < S7JobParamOffset + 8)
            {
                return new byte[0];
            }

            int requestedPdu = (request[23] << 8) | request[24];
            if (requestedPdu <= 0 || requestedPdu > MaxPduLength)
            {
                requestedPdu = MaxPduLength;
            }

            byte[] frame = new byte[S7AckDataParamOffset + 8];
            frame[0] = 0x03;
            frame[1] = 0x00;
            frame[2] = 0x00;
            frame[3] = (byte)frame.Length;
            frame[4] = 0x02;
            frame[5] = 0xF0;
            frame[6] = 0x80;
            frame[7] = 0x32;
            frame[8] = 0x03;                       // ROSCTR = Ack-Data
            frame[9] = 0x00;
            frame[10] = 0x00;
            frame[11] = request[11];               // PDU 引用回显
            frame[12] = request[12];
            frame[13] = 0x00;                      // 参数长度 8
            frame[14] = 0x08;
            frame[15] = 0x00;                      // 数据长度 0
            frame[16] = 0x00;
            frame[17] = 0x00;                      // 错误类
            frame[18] = 0x00;                      // 错误码
            frame[19] = 0xF0;                      // 功能码
            frame[20] = 0x00;
            frame[21] = 0x00;
            frame[22] = 0x03;                      // 最大并行任务
            frame[23] = 0x00;
            frame[24] = 0x03;
            frame[25] = (byte)(requestedPdu >> 8);
            frame[26] = (byte)(requestedPdu & 0xFF);
            return frame;
        }

        /// <summary>
        /// 解析读变量请求（功能码 0x04）。参数区布局：
        /// <c>04 | 项数 | (12 0A 10 | TS | 元素数(2) | DB 号(2) | 区码 | 位地址(3)) × N</c>。
        /// </summary>
        public bool TryParseReadRequest(byte[] data, out S7RequestInfo request, out string error)
        {
            return TryParseJob(data, 0x04, false, out request, out error);
        }

        /// <summary>
        /// 解析写变量请求（功能码 0x05）。参数区与读相同，参数区之后是数据区：
        /// 每项 <c>00 | 写入类型 | 位长度(2) | 数据</c>，数据字节数 = 位长度 / 8（向上取整）。
        /// </summary>
        public bool TryParseWriteRequest(byte[] data, out S7RequestInfo request, out string error)
        {
            return TryParseJob(data, 0x05, true, out request, out error);
        }

        /// <summary>
        /// 构建读响应：参数区只有 <c>04 项数</c>，每项一个数据项按顺序排在数据区。
        /// </summary>
        public byte[] BuildReadResponse(byte[] request, IReadOnlyList<S7ItemResult> items)
        {
            int count = items == null ? 0 : items.Count;
            byte[] parameter = { 0x04, (byte)count };

            var data = new List<byte>(count * 8);
            for (int i = 0; i < count; i++)
            {
                S7ItemResult item = items[i];
                if (item == null)
                {
                    data.AddRange(BuildDataItemError(DataItemAddressOutOfRange));
                }
                else if (item.ErrorCode != 0)
                {
                    data.AddRange(BuildDataItemError(item.ErrorCode));
                }
                else if (item.IsBit)
                {
                    data.AddRange(BuildBitDataItem(item.Bits ?? new bool[0]));
                }
                else
                {
                    data.AddRange(BuildWordDataItem(item.Words ?? new short[0]));
                }
            }

            return BuildAckDataFrame(request, parameter, data.ToArray());
        }

        /// <summary>
        /// 构建写响应：参数区 <c>05 项数</c>，数据区每项 1 字节返回码。
        /// S7 的写响应没有「数据项头」，成功的项就是单个 0xFF。
        /// </summary>
        public byte[] BuildWriteResponse(byte[] request, IReadOnlyList<ushort> errorCodes)
        {
            int count = errorCodes == null ? 0 : errorCodes.Count;
            byte[] parameter = { 0x05, (byte)count };
            byte[] data = new byte[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = errorCodes[i] == 0
                    ? DataItemOk
                    : (byte)(errorCodes[i] & 0xFF);
            }

            return BuildAckDataFrame(request, parameter, data);
        }

        /// <summary>
        /// 构建作业级错误响应：参数区 <c>功能码 00</c>、数据区为空，错误写在 S7 头 [17][18]。
        /// 客户端会在定位数据项之前先查这两个字节，所以能直接报出「PLC 拒绝请求：错误类 …」。
        /// </summary>
        public byte[] BuildJobErrorResponse(byte[] request, byte functionCode, byte errorClass, byte errorCode)
        {
            byte[] parameter = { functionCode, 0x00 };
            byte[] frame = BuildAckDataFrame(request, parameter, new byte[0]);
            frame[17] = errorClass;
            frame[18] = errorCode;
            return frame;
        }

        #region 解析细节

        /// <summary>
        /// 读写请求共用一套解析：先校验作业头与项数，再逐项解析 ANY 指针；
        /// 写请求还要按项头的位长度切出数据。
        /// </summary>
        private static bool TryParseJob(
            byte[] data, byte functionCode, bool hasData, out S7RequestInfo request, out string error)
        {
            request = null;
            error = null;

            if (data == null || data.Length < S7JobParamOffset + JobParamHeaderLength)
            {
                error = "请求参数区不完整";
                return false;
            }
            if (data[S7HeaderOffset] != 0x32 || data[S7HeaderOffset + 1] != 0x01)
            {
                error = "不是 S7 作业请求（协议 ID / ROSCTR 不符）";
                return false;
            }
            if (data[S7JobParamOffset] != functionCode)
            {
                error = $"功能码 0x{data[S7JobParamOffset]:X2} 与预期 0x{functionCode:X2} 不符";
                return false;
            }

            int itemCount = data[S7JobParamOffset + 1];
            if (itemCount <= 0)
            {
                error = "项数为 0";
                return false;
            }

            int paramLength = (data[13] << 8) | data[14];
            int dataLength = (data[15] << 8) | data[16];
            int requiredParam = JobParamHeaderLength + itemCount * ItemLength;
            if (paramLength < requiredParam)
            {
                error = $"参数区长度 {paramLength} 不足以容纳 {itemCount} 项（至少需要 {requiredParam}）";
                return false;
            }

            int dataStart = S7JobParamOffset + paramLength;
            if (hasData)
            {
                if (dataLength < WriteItemHeaderLength)
                {
                    error = $"写请求数据区不足（声明 {dataLength} 字节）";
                    return false;
                }
                if (data.Length < dataStart + dataLength)
                {
                    error = $"写请求数据区被截断（声明 {dataLength} 字节，报文仅 {data.Length - dataStart} 字节）";
                    return false;
                }
            }

            var items = new List<S7ItemRequest>(itemCount);
            int cursor = dataStart;
            int dataEnd = dataStart + dataLength;

            for (int i = 0; i < itemCount; i++)
            {
                S7ItemRequest item;
                if (!TryParseItem(data, S7JobParamOffset + JobParamHeaderLength + i * ItemLength, out item, out error))
                {
                    return false;
                }

                if (hasData && !TryReadWritePayload(data, ref cursor, dataEnd, i, item, out error))
                {
                    return false;
                }

                items.Add(item);
            }

            request = new S7RequestInfo { Items = items };
            return true;
        }

        /// <summary>解析一个 12 字节的 ANY 指针。</summary>
        private static bool TryParseItem(byte[] data, int at, out S7ItemRequest item, out string error)
        {
            item = null;
            error = null;

            if (data[at] != 0x12 || data[at + 1] != 0x0A)
            {
                error = $"不支持的变量指定格式（期望 12 0A，实际 {data[at]:X2} {data[at + 1]:X2}）";
                return false;
            }

            byte transportSize = data[at + 3];
            if (transportSize != TransportBit && transportSize != TransportByte)
            {
                error = $"不支持的传输类型 0x{transportSize:X2}（只支持 0x01 位 / 0x02 字节）";
                return false;
            }

            MemoryArea area;
            if (!TryMapArea(data[at + 8], out area))
            {
                error = $"不支持的区码 0x{data[at + 8]:X2}";
                return false;
            }

            int elementCount = (data[at + 4] << 8) | data[at + 5];
            int dbNumber = (data[at + 6] << 8) | data[at + 7];
            int bitOffset = (data[at + 9] << 16) | (data[at + 10] << 8) | data[at + 11];
            bool isBit = transportSize == TransportBit;

            // TS=0x04 时元素数的单位是字节，字节数必须是偶数才能整除成字。
            // 奇数字节（DBB 这类字节访问）目前不支持，明确拒绝好过截断成半个字。
            if (!isBit && (elementCount & 1) != 0)
            {
                error = $"字访问的字节数必须为偶数（收到 {elementCount} 字节），字节读写尚未支持";
                return false;
            }

            int length = isBit ? elementCount : elementCount / 2;
            if (length <= 0)
            {
                error = $"请求长度非法（元素数 {elementCount}）";
                return false;
            }

            item = new S7ItemRequest
            {
                Area = area,
                DbNumber = dbNumber,
                ByteOffset = bitOffset / 8,
                IsBit = isBit,
                Length = length,
            };
            return true;
        }

        /// <summary>
        /// 从数据区切出某一项的写入数据。数据字节数由项头的位长度决定，
        /// 所以多变量也能精确切分——不依赖「按剩余长度猜」。
        /// </summary>
        private static bool TryReadWritePayload(
            byte[] data, ref int cursor, int dataEnd, int index, S7ItemRequest item, out string error)
        {
            error = null;

            if (cursor + WriteItemHeaderLength > dataEnd)
            {
                error = $"第 {index + 1} 项数据头被截断";
                return false;
            }

            int bitLength = (data[cursor + 2] << 8) | data[cursor + 3];
            int payloadBytes = (bitLength + 7) / 8;
            if (payloadBytes <= 0)
            {
                error = $"第 {index + 1} 项位长度为 0";
                return false;
            }
            if (cursor + WriteItemHeaderLength + payloadBytes > dataEnd)
            {
                error = $"第 {index + 1} 项数据被截断（需要 {payloadBytes} 字节）";
                return false;
            }

            item.WriteData = new byte[payloadBytes];
            Array.Copy(data, cursor + WriteItemHeaderLength, item.WriteData, 0, payloadBytes);
            cursor += WriteItemHeaderLength + payloadBytes;
            return true;
        }

        /// <summary>区码 → 区域。</summary>
        private static bool TryMapArea(byte code, out MemoryArea area)
        {
            switch (code)
            {
                case 0x81:
                    area = MemoryArea.X;
                    return true;
                case 0x82:
                    area = MemoryArea.Y;
                    return true;
                case 0x83:
                    area = MemoryArea.M;
                    return true;
                case 0x84:
                    area = MemoryArea.DB;
                    return true;
                default:
                    area = MemoryArea.M;
                    return false;
            }
        }

        #endregion

        #region 帧构建

        /// <summary>
        /// 拼一帧 Ack-Data：TPKT + COTP(DT) + 12 字节 S7 头 + 参数区 + 数据区。
        /// 作业级的错误填在 S7 头 [17][18]；数据项级的错误走 <see cref="BuildDataItemError"/>。
        /// </summary>
        private static byte[] BuildAckDataFrame(byte[] request, byte[] parameter, byte[] data)
        {
            int total = S7AckDataParamOffset + parameter.Length + data.Length;
            byte[] frame = new byte[total];
            frame[0] = 0x03;
            frame[1] = 0x00;
            frame[2] = (byte)(total >> 8);
            frame[3] = (byte)(total & 0xFF);
            frame[4] = 0x02;
            frame[5] = 0xF0;
            frame[6] = 0x80;
            frame[7] = 0x32;
            frame[8] = 0x03;
            frame[9] = 0x00;
            frame[10] = 0x00;
            // PDU 引用必须回显，客户端靠它判定「这是不是本次请求的应答」
            frame[11] = request == null ? (byte)0x00 : request[11];
            frame[12] = request == null ? (byte)0x00 : request[12];
            frame[13] = (byte)(parameter.Length >> 8);
            frame[14] = (byte)(parameter.Length & 0xFF);
            frame[15] = (byte)(data.Length >> 8);
            frame[16] = (byte)(data.Length & 0xFF);
            frame[17] = 0x00;
            frame[18] = 0x00;
            Array.Copy(parameter, 0, frame, S7AckDataParamOffset, parameter.Length);
            Array.Copy(data, 0, frame, S7AckDataParamOffset + parameter.Length, data.Length);
            return frame;
        }

        /// <summary>数据项级的失败：只有 [返回码][错误码] 两字节，没有数据。</summary>
        private static byte[] BuildDataItemError(ushort errorCode)
        {
            byte code = errorCode == 0 ? DataItemAddressOutOfRange : (byte)(errorCode & 0xFF);
            return new[] { DataItemAddressOutOfRange, code };
        }

        /// <summary>位数据项：FF | 03 | 位长度(2) | 按位打包的数据。</summary>
        private static byte[] BuildBitDataItem(bool[] bits)
        {
            int length = bits.Length;
            int byteCount = (length + 7) / 8;
            byte[] item = new byte[4 + byteCount];
            item[0] = DataItemOk;
            item[1] = DataItemBit;
            item[2] = (byte)(length >> 8);
            item[3] = (byte)(length & 0xFF);

            for (int i = 0; i < length; i++)
            {
                if (bits[i])
                {
                    item[4 + (i >> 3)] |= (byte)(1 << (i & 7));
                }
            }
            return item;
        }

        /// <summary>字数据项：FF | 04 | 位长度(2) | 大端字数据。TS=0x04 时长度字段按位算。</summary>
        private static byte[] BuildWordDataItem(short[] words)
        {
            int byteCount = words.Length * 2;
            int bitLength = byteCount * 8;
            byte[] item = new byte[4 + byteCount];
            item[0] = DataItemOk;
            item[1] = DataItemByteWord;
            item[2] = (byte)(bitLength >> 8);
            item[3] = (byte)(bitLength & 0xFF);

            for (int i = 0; i < words.Length; i++)
            {
                item[4 + i * 2] = (byte)((words[i] >> 8) & 0xFF);
                item[5 + i * 2] = (byte)(words[i] & 0xFF);
            }
            return item;
        }

        #endregion
    }
}
