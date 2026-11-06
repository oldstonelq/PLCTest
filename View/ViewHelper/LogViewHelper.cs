using System;
using System.Windows.Forms;

namespace PlcTest.View
{
    /// <summary>
    /// 日志文本框的统一写入逻辑，客户端窗体与服务端窗体共用。
    /// <para>
    /// 两个要点都收在这里，避免每个窗体抄一遍：
    /// 1) 协议线程 / 读取线程也会调进来，用 <c>BeginInvoke</c> 投递——
    ///    <c>Invoke</c> 会阻塞等待 UI，而 UI 线程关窗时可能在等读取线程，两者互等会卡死；
    /// 2) 超过行数上限从头部裁掉，长时间联调不至于把文本框撑爆。
    /// </para>
    /// </summary>
    internal static class LogViewHelper
    {
        /// <summary>
        /// 追加一行日志（自动切回 UI 线程并裁剪超长内容）。
        /// </summary>
        /// <param name="box">日志文本框</param>
        /// <param name="message">日志内容</param>
        /// <param name="maxLines">最多保留的行数</param>
        public static void Append(TextBox box, string message, int maxLines)
        {
            if (box == null || box.IsDisposed)
            {
                return;
            }

            if (box.InvokeRequired)
            {
                try
                {
                    box.BeginInvoke(new Action<TextBox, string, int>(Append), box, message, maxLines);
                }
                catch (ObjectDisposedException)
                {
                    // 控件已销毁，丢弃这条日志即可
                }
                catch (InvalidOperationException)
                {
                    // 窗体句柄已销毁，丢弃这条日志即可
                }
                return;
            }

            box.AppendText(message + "\r\n");
            Trim(box, maxLines);
        }

        /// <summary>
        /// 行数超限后从头部裁掉，并把光标移到末尾（保持自动滚到底部）。
        /// </summary>
        private static void Trim(TextBox box, int maxLines)
        {
            try
            {
                if (maxLines <= 0 || box.Lines.Length <= maxLines)
                {
                    return;
                }

                string[] lines = box.Lines;
                string[] kept = new string[maxLines];
                Array.Copy(lines, lines.Length - maxLines, kept, 0, maxLines);
                box.Lines = kept;
                box.SelectionStart = box.TextLength;
                box.ScrollToCaret();
            }
            catch
            {
                // 裁剪失败不影响日志展示
            }
        }
    }
}
