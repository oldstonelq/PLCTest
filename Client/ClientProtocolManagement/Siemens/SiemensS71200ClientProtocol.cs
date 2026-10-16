using PlcTest.Models;
using PlcTest.Client.PlcInterface;
using System;

namespace PlcTest.Client.ClientProtocolManagement.Siemens
{
    /// <summary>
    /// 西门子 S7-1200 通讯协议（S7comm over TPKT+COTP）
    /// </summary>
    public class SiemensS71200ClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        /// 协议单元，每次累加
        /// </summary>
        private byte _pduRef = 0;

        /// <summary>
        /// 获取 S7 存储区编码
        /// X:0x81(Input)  Y:0x82(Output)  M:0x83(Marker)  D:0x84(DB)
        /// </summary>
        /// <summary>
        /// 获取西门子 S7 存储区代码。
        /// </summary>
        /// <param name="area">存储区域。</param>
        /// <returns>存储区代码。</returns>
        public int GetMemoryCode(MemoryArea area)
        {
            switch (area)
            {
                case MemoryArea.X: return 0x81;
                case MemoryArea.Y: return 0x82;
                case MemoryArea.M: return 0x83;
                //case MemoryArea.DB: return 0x84;
                default:
                    throw new ArgumentOutOfRangeException(nameof(area), "无效的存储区类型");
            }
        }

        /// <summary>
        /// 将 bool 数组转为 S7 位写入数据（每 bool 占 1 字节，0x00/0x01）
        /// </summary>
        /// <summary>
        /// 将位数组转换为 S7 位写入数据。
        /// </summary>
        /// <param name="bools">位数据。</param>
        /// <returns>写入数据区。</returns>
        public byte[] GetWriteBitData(bool[] bools)
        {
            byte[] mData = new byte[bools.Length];
            for (int i = 0; i < bools.Length; i++)
            {
                mData[i] = bools[i] ? (byte)0x01 : (byte)0x00;
            }
            return mData;
        }

        /// <summary>
        /// 将 short 数组转为 S7 字写入数据（大端序，每字 2 字节）
        /// </summary>
        /// <summary>
        /// 将字数组转换为 S7 字写入数据。
        /// </summary>
        /// <param name="shorts">字数据。</param>
        /// <returns>写入数据区。</returns>
        public byte[] GetWriteWordData(short[] shorts)
        {
            byte[] mData = new byte[shorts.Length * 2];
            for (int i = 0; i < shorts.Length; i++)
            {
                byte[] bytes = BitConverter.GetBytes(shorts[i]);
                mData[i * 2] = bytes[1];
                mData[i * 2 + 1] = bytes[0];
            }
            return mData;
        }

        /// <summary>
        /// 构建 S7 读取命令参数区（功能码 0x04，14 字节）
        /// </summary>
        /// <summary>
        /// 构建 S7 读取命令的数据区。
        /// </summary>
        /// <param name="address">统一地址模型，包含 DB 编号、地址和长度。</param>
        /// <returns>读取参数区。</returns>
        public byte[] BuildReadData(PlcAddress address)
        {
            int memory = GetMemoryCode(address.Area);
            int db = address.DbNumber ?? 0;
            int startAddress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] param = new byte[14];
            //功能码
            param[0] = 0x04;
            //通信项数
            param[1] = 0x01;
            //变量指定
            param[2] = 0x12;
            //后续地址描述长度
            param[3] = 0x0A;
            //SyntaxID
            param[4] = 0x10;
            //传输类型: 0x01=位, 0x02=字
            param[5] = isBit ? (byte)0x01 : (byte)0x02;
            //操作数据长度 (大端)
            param[6] = (byte)(length >> 8);
            param[7] = (byte)(length & 0xFF);
            //DB号 (非DB时为0)
            if (memory != 0x84)
            {
                param[8] = 0x00;
                param[9] = 0x00;
            }
            else
            {
                param[8] = (byte)((db >> 8) & 0xFF);
                param[9] = (byte)(db & 0xFF);
            }
            //存储区类型
            param[10] = (byte)memory;
            //起始地址
            int addrVal = startAddress * 8;
            param[11] = (byte)((addrVal >> 16) & 0xFF);
            param[12] = (byte)((addrVal >> 8) & 0xFF);
            param[13] = (byte)(addrVal & 0xFF);
            return param;
        }

        /// <summary>
        /// 构建 S7 写入命令参数区 + 数据区（功能码 0x05，18 字节参数 + 写入数据）
        /// </summary>
        /// <summary>
        /// 构建 S7 写入命令的数据区。
        /// </summary>
        /// <param name="address">统一地址模型，包含 DB 编号、地址和长度。</param>
        /// <param name="data">写入数据。</param>
        /// <returns>写入参数区与数据区。</returns>
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            int memory = GetMemoryCode(address.Area);
            int db = address.DbNumber ?? 0;
            int startAddress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] param = new byte[18 + data.Length];
            //功能码
            param[0] = 0x05;
            //通信项数
            param[1] = 0x01;
            //变量指定
            param[2] = 0x12;
            //后续地址描述长度
            param[3] = 0x0A;
            //SyntaxID
            param[4] = 0x10;
            //传输类型: 0x01=位, 0x02=字
            param[5] = isBit ? (byte)0x01 : (byte)0x02;
            //操作数据长度 (大端)
            param[6] = (byte)(length >> 8);
            param[7] = (byte)(length & 0xFF);
            //DB号 (非DB时为0)
            if (memory != 0x84)
            {
                param[8] = 0x00;
                param[9] = 0x00;
            }
            else
            {
                param[8] = (byte)((db >> 8) & 0xFF);
                param[9] = (byte)(db & 0xFF);
            }
            //存储区类型
            param[10] = (byte)memory;
            //起始地址
            int addrVal = startAddress * 8;
            param[11] = (byte)((addrVal >> 16) & 0xFF);
            param[12] = (byte)((addrVal >> 8) & 0xFF);
            param[13] = (byte)(addrVal & 0xFF);
            //预留
            param[14] = 0x00;
            //写入数据类型: 字节 0X04 位0X03
            param[15] = isBit ? (byte)0x03 : (byte)0x04;

            //写入数据长度

            param[16] = (byte)((addrVal >> 8) & 0xFF);
            param[17] = (byte)(addrVal & 0xFF);

            Array.Copy(data, 0, param, 18, data.Length);
            return param;
        }

        /// <summary>
        /// 封装 TPKT(4) + COTP(3) + S7通讯头(10) + 负载，构建完整报文
        /// 前 14 字节为参数区，剩余为数据区
        /// </summary>
        /// <summary>
        /// 封装 TPKT + COTP + S7 头，构建完整报文。
        /// </summary>
        /// <param name="data">S7 参数区与数据区。</param>
        /// <returns>完整通讯报文。</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            int paramLen = data[0] == 0x04 ? 14 : (data[0] == 0x05 ? 18 : 14);
            int dataLen = data.Length - paramLen;
            if (dataLen < 0) dataLen = 0;

            int totalLen = 7 + 10 + data.Length;
            byte[] command = new byte[totalLen];

            //TPKT（版本号1个字节+预留1个字节+报文总长度2个字节）
            command[0] = 0x03;
            command[1] = 0x00;
            command[2] = (byte)(totalLen >> 8);
            command[3] = (byte)(totalLen & 0xFF);
            //COTP(长度1个字节+PDU类型1个字节+目标引用1个字节)
            command[4] = 0x02;
            command[5] = 0xF0;
            command[6] = 0x80;
            //Header（协议ID 1个字节+ ACK_data 1个字节 +预留 2个字节 +协议数据单元 2个字节
            //+参数位长度 2个字节 数据长度 2个字节
            //）
            command[7] = 0x32;
            command[8] = 0x01;
            command[9] = 0x00;
            command[10] = 0x00;
            unchecked { _pduRef++; }
            command[11] = (byte)(_pduRef >> 8);
            command[12] = (byte)(_pduRef & 0xFF);
            command[13] = (byte)(paramLen >> 8);
            command[14] = (byte)(paramLen & 0xFF);
            command[15] = (byte)(dataLen >> 8);
            command[16] = (byte)(dataLen & 0xFF);

            data.CopyTo(command, 17);
            return command;
        }

        /// <summary>
        /// 解析 S7 写入响应报文
        /// </summary>
        /// <summary>
        /// 解析写入响应，检查返回码是否成功。
        /// </summary>
        /// <param name="data">完整响应报文。</param>
        /// <returns>解析结果。</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            Result<bool> result = new Result<bool>();

            if (data == null || data.Length < 22)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }

            byte funcCode = data[17];
            byte itemCount = data[18];
            byte returnCode = data[19];

            if (funcCode != 0x05)
            {
                result.ErrorMsg = "功能码不一致";
                return result;
            }
            if (returnCode != 0xFF)
            {
                result.ErrorMsg = GetS7ErrorMsg(returnCode);
                return result;
            }

            result.IsSuccess = true;
            result.Data = true;
            return result;
        }

        /// <summary>
        /// 解析 S7 读位响应报文
        /// </summary>
        /// <summary>
        /// 解析位读取响应。
        /// </summary>
        /// <param name="data">完整响应报文。</param>
        /// <param name="size">期望点数。</param>
        /// <returns>解析结果。</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            Result<bool[]> result = new Result<bool[]>();

            if (data == null || data.Length < 22)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }

            byte funcCode = data[17];
            byte itemCount = data[18];
            byte returnCode = data[19];

            if (returnCode != 0xFF)
            {
                result.ErrorMsg = GetS7ErrorMsg(returnCode);
                return result;
            }

            int dataOffset = 23;
            if (data.Length - dataOffset < size)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }

            bool[] realData = new bool[size];
            for (int i = 0; i < size; i++)
            {
                realData[i] = data[dataOffset + i] != 0x00;
            }

            result.IsSuccess = true;
            result.Data = realData;
            return result;
        }

        /// <summary>
        /// 解析 S7 读字响应报文
        /// </summary>
        /// <summary>
        /// 解析字读取响应。
        /// </summary>
        /// <param name="data">完整响应报文。</param>
        /// <param name="size">期望字数。</param>
        /// <returns>解析结果。</returns>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            Result<short[]> result = new Result<short[]>();

            if (data == null || data.Length < 22)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }

            byte funcCode = data[17];
            byte itemCount = data[18];
            byte returnCode = data[19];

            if (returnCode != 0xFF)
            {
                result.ErrorMsg = GetS7ErrorMsg(returnCode);
                return result;
            }

            int expectedLen = size * 2;
            int dataOffset = 23;
            if (data.Length - dataOffset < expectedLen)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }

            short[] realData = new short[size];
            for (int i = 0; i < size; i++)
            {
                realData[i] = (short)((data[dataOffset + i * 2] << 8) | data[dataOffset + i * 2 + 1]);
            }

            result.IsSuccess = true;
            result.Data = realData;
            return result;
        }

        /// <summary>
        /// S7 错误码对应的中文描述
        /// </summary>
        /// <summary>
        /// 将 S7 返回码转换为中文错误信息。
        /// </summary>
        /// <param name="errorCode">返回码。</param>
        /// <returns>错误描述。</returns>
        private static string GetS7ErrorMsg(byte errorCode)
        {
            switch (errorCode)
            {
                case 0x05: return "地址越界";
                case 0x07: return "写入数据长度错误";
                case 0x0A: return "对象不存在";
                case 0xFF: return "";
                default: return $"S7错误码: 0x{errorCode:X2}";
            }
        }
    }
}
