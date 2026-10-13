using System;
using static PlcTest.Models.Enums;

namespace PlcTest.Models
{
    /// <summary>
    /// 统一 PLC 地址模型，同时适配三菱、西门子 S7、OPC UA。
    /// </summary>
    public class PlcAddress
    {
        /// <summary>
        /// 存储区域。
        /// </summary>
        public MemoryArea Area { get; set; }
        /// <summary>
        /// 操作地址。
        /// </summary>
        public int Address { get; set; }
        /// <summary>
        /// 操作长度，默认值为 1。
        /// </summary>
        public int Length { get; set; }
        /// <summary>
        /// 是否是位操作。
        /// </summary>
        public bool IsBit { get; set; }
        /// <summary>
        /// DB块编号，西门子 DB 区使用，其他区域可为空。
        /// </summary>
        public int? DbNumber { get; set; }
        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="area">存储区域。</param>
        /// <param name="address">操作地址。</param>
        /// <param name="isBit">是否位操作。</param>
        /// <param name="dbNumber">DB块编号。</param>
        /// <param name="length">操作长度。</param>
        public PlcAddress(MemoryArea area, int address, bool isBit, int? dbNumber = null, int length = 1)
        {
            Area = area;
            Address = address;
            IsBit = isBit;
            DbNumber = dbNumber;
            Length = length;
        }

        /// <summary>
        /// 创建位地址。
        /// </summary>
        /// <param name="area">存储区域。</param>
        /// <param name="address">操作地址。</param>
        /// <param name="dbNumber">DB块编号。</param>
        /// <param name="length">位点数。</param>
        /// <returns>位地址对象。</returns>
        public static PlcAddress Bit(MemoryArea area, int address, int? dbNumber = null, int length = 1)
            => new PlcAddress(area, address, true, dbNumber, length);

        /// <summary>
        /// 创建字地址。
        /// </summary>
        /// <param name="area">存储区域。</param>
        /// <param name="address">操作地址。</param>
        /// <param name="dbNumber">DB块编号。</param>
        /// <param name="length">字点数。</param>
        /// <returns>字地址对象。</returns>
        public static PlcAddress Word(MemoryArea area, int address, int? dbNumber = null, int length = 1)
            => new PlcAddress(area, address, false, dbNumber, length);

        /// <summary>
        /// 返回地址的调试字符串。
        /// </summary>
        /// <returns>格式化后的地址文本。</returns>
        public override string ToString()
        {
            string db = DbNumber.HasValue ? $"DB{DbNumber.Value}." : "";
            string suff = IsBit ? "" : "W";
            return $"{db}{Area}{Address}{suff}";
        }
    }
}
