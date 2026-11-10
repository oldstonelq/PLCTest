using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Tool
{
    /// <summary>
    /// StructureHelp 的摘要说明
    /// </summary>
    public class StructureHelp
    {
        /// <summary>
        /// byte数组转结构体
        /// </summary>
        /// <param name="bytes">byte数组</param>
        /// <param name="type">结构体类型</param>
        /// <returns>转换后的结构体</returns>
        public static T BytesToStuct<T>(byte[] bytes, int startIndex) where T : struct
        {
            //得到结构体的大小
            int size = Marshal.SizeOf<T>();
            //byte数组长度小于结构体的大小
            if (size > bytes.Length)
            {
                //返回空
                return default(T);
            }
            //分配结构体大小的内存空间
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            //将byte数组拷到分配好的内存空间
            Marshal.Copy(bytes, startIndex, structPtr, size);
            //将内存空间转换为目标结构体
            var re = Marshal.PtrToStructure<T>(structPtr);
            //释放内存空间
            Marshal.FreeHGlobal(structPtr);
            //返回结构体
            return re;
        }

        /// 结构体转byte数组
        /// </summary>
        /// <param name="structObj">要转换的结构体</param>
        /// <returns>转换后的byte数组</returns>
        public static byte[] StructToBytes<T>(T structObj) where T : struct
        {
            //得到结构体的大小
            int size = Marshal.SizeOf(structObj);
            //创建byte数组
            byte[] bytes = new byte[size];
            //分配结构体大小的内存空间
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            //将结构体拷到分配好的内存空间
            Marshal.StructureToPtr(structObj, structPtr, false);
            //从内存空间拷到byte数组
            Marshal.Copy(structPtr, bytes, 0, size);
            //释放内存空间
            Marshal.FreeHGlobal(structPtr);
            //返回byte数组
            return bytes;
        }

        /// <summary>
        /// int16转byte
        /// </summary>
        /// <param name="arrInt16"></param>
        /// <param name="nInt16Count"></param>
        /// <param name="destByteArr"></param>
        public static void Int16ToByte(Int16[] arrInt16, int nInt16Count, ref Byte[] destByteArr)
        {
            //遵守X86规则，低字节放在前面，高字节放在后面
            for (int i = 0; i < nInt16Count; i++)
            {
                destByteArr[2 * i + 0] = Convert.ToByte((arrInt16[i] & 0x00FF));
                destByteArr[2 * i + 1] = Convert.ToByte((arrInt16[i] & 0xFF00) >> 8);
            }
        }
    }
}
