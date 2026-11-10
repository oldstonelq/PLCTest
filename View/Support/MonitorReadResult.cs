namespace PlcTest.View.Support
{
    /// <summary>
    /// 一次监视读取的结果：位视图给 <see cref="Bits"/>，字视图给 <see cref="Words"/>。
    /// <para>
    /// 单独成文件的原因：Visual Studio 的设计器只认「文件里的第一个类」，
    /// 之前它和 <c>MonitorGrid</c> 挤在一个文件里且排在前面，设计器就报
    /// 「MonitorGrid 可以进行设计，但不是文件中的第一个类」。
    /// </para>
    /// </summary>
    public sealed class MonitorReadResult
    {
        /// <summary>是否读到</summary>
        public bool Success { get; private set; }

        /// <summary>失败原因</summary>
        public string Error { get; private set; }

        /// <summary>位数据（按位读取时非 null）</summary>
        public bool[] Bits { get; private set; }

        /// <summary>字数据（按字读取时非 null）</summary>
        public short[] Words { get; private set; }

        /// <summary>位数据结果</summary>
        public static MonitorReadResult OfBits(bool[] values)
        {
            return new MonitorReadResult { Success = true, Bits = values ?? new bool[0] };
        }

        /// <summary>字数据结果</summary>
        public static MonitorReadResult OfWords(short[] values)
        {
            return new MonitorReadResult { Success = true, Words = values ?? new short[0] };
        }

        /// <summary>失败结果</summary>
        public static MonitorReadResult Fail(string message)
        {
            return new MonitorReadResult { Success = false, Error = message };
        }
    }
}
