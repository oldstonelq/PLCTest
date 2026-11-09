using PlcTest.Models;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PlcTest.View.CustomControl
{
    /// <summary>
    /// 通讯报文视图：带方向着色的收发报文 + 纯文本提示，替代原来的单色 <c>TextBox</c>。
    /// <para>
    /// 与旧日志框的差别只有三点，但正是联调时最缺的三点：
    /// 1) 发送 / 接收用颜色区分，错了哪一边一眼能看出来；
    /// 2) 报文按空格分字节（<c>01 FF 0A</c>），可以整段复制到别处比对；
    /// 3) 可以暂停——轮询场景里日志刷得飞快，不暂停根本看不住某一帧。
    /// </para>
    /// <para>
    /// 线程模型沿用原日志助手（已移除）的做法：协议线程调进来时用 <c>BeginInvoke</c> 投递
    /// （不能用 <c>Invoke</c>，关窗时 UI 线程可能在等读取线程，互等会死锁）；
    /// 控件已销毁或句柄已释放时直接丢弃这条日志。
    /// </para>
    /// </summary>
    public sealed class FrameLogView : UserControl
    {
        private readonly RichTextBox _box;
        private readonly Button _btnPause;
        private readonly Button _btnClear;
        private readonly Button _btnCopy;
        private readonly CheckBox _chkAutoScroll;

        /// <summary>暂停期间收到的日志，恢复时按顺序补上（有上限，避免暂停一整天把内存吃满）</summary>
        private readonly System.Collections.Generic.Queue<CommFrame> _pending =
            new System.Collections.Generic.Queue<CommFrame>();

        /// <summary>暂停期间最多缓存多少条</summary>
        private const int MaxPending = 2000;

        private int _lineCount;
        private bool _paused;

        /// <summary>当前保留的最大行数，超出后从头部裁掉</summary>
        public int MaxLines { get; set; }

        /// <summary>是否处于暂停状态</summary>
        public bool IsPaused
        {
            get { return _paused; }
        }

        /// <summary>
        /// 当前全部日志文本（复制、测试、排查用）。暂停期间的日志不在其中。
        /// </summary>
        public string LogText
        {
            get { return _box.Text; }
        }

        /// <summary>
        /// 构造：工具栏 + 只读 RichTextBox，全部代码生成（不依赖 Designer，避免老式 csproj 漏文件）。
        /// </summary>
        public FrameLogView()
        {
            MaxLines = 500;

            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = SystemColors.Control,
            };

            _btnPause = CreateBarButton("暂停", 2, 52);
            _btnPause.Click += (s, e) => TogglePause();

            _btnClear = CreateBarButton("清空", 58, 52);
            _btnClear.Click += (s, e) => ClearLog();

            _btnCopy = CreateBarButton("复制", 114, 52);
            _btnCopy.Click += (s, e) => CopyAll();

            _chkAutoScroll = new CheckBox
            {
                Text = "自动滚动",
                Checked = true,
                AutoSize = true,
                Location = new Point(174, 6),
                Font = new Font("Microsoft Sans Serif", 8.25f),
            };

            bar.Controls.Add(_btnPause);
            bar.Controls.Add(_btnClear);
            bar.Controls.Add(_btnCopy);
            bar.Controls.Add(_chkAutoScroll);

            _box = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(44, 44, 42),
                Font = new Font("Consolas", 9f),
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                DetectUrls = false,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
            };

            Controls.Add(_box);
            Controls.Add(bar);
        }

        /// <summary>
        /// 追加一条结构化日志。
        /// </summary>
        public void Append(CommFrame frame)
        {
            if (frame == null || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<CommFrame>(Append), frame);
                }
                catch (ObjectDisposedException)
                {
                    // 控件已销毁，丢弃这条日志
                }
                catch (InvalidOperationException)
                {
                    // 句柄已释放，丢弃这条日志
                }
                return;
            }

            if (_paused)
            {
                if (_pending.Count < MaxPending)
                {
                    _pending.Enqueue(frame);
                    RefreshPauseButton();
                }
                return;
            }

            AppendCore(frame);
        }

        /// <summary>
        /// 追加一行纯文本（兼容旧窗体的 <c>AddInfo(string)</c>）。
        /// </summary>
        public void Append(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            Append(CommFrame.Note(message));
        }

        /// <summary>
        /// 清空日志与暂停缓存。
        /// </summary>
        public void ClearLog()
        {
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(ClearLog));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }

            _box.Clear();
            _lineCount = 0;
            _pending.Clear();
            RefreshPauseButton();
        }

        #region 渲染

        private void AppendCore(CommFrame frame)
        {
            string line = Format(frame);
            Color color = PickColor(frame);

            try
            {
                // 逐段着色：先定位到末尾、给这一段设置颜色，再追加文本
                _box.SelectionStart = _box.TextLength;
                _box.SelectionLength = 0;
                _box.SelectionColor = color;
                _box.AppendText(line + Environment.NewLine);
                _box.SelectionColor = _box.ForeColor;
            }
            catch (Exception)
            {
                // 句柄未创建时彩色写入可能失败：退回纯文本，至少内容不丢
                try { _box.AppendText(line + Environment.NewLine); } catch { return; }
            }

            _lineCount++;
            TrimHead();
            ScrollToEndIfNeeded();
        }

        /// <summary>
        /// 一行日志的格式：时间 + 方向 + 报文；提示类不显示方向。
        /// 说明文本若不是默认的「发送报文 / 接收报文」，附在行尾，避免丢掉「节点地址请求」这类语义。
        /// </summary>
        private static string Format(CommFrame frame)
        {
            string time = frame.Time.ToString("HH:mm:ss.fff");

            if (!frame.IsTraffic)
            {
                return time + "  --  " + frame.Caption;
            }

            string tag = frame.Direction == CommFrameDirection.Send ? "TX" : "RX";
            string body = frame.HasData ? frame.HexSpaced : "<空>";
            string suffix = string.Empty;

            string defaultCaption = frame.Direction == CommFrameDirection.Send
                ? CommFrame.SendCaption
                : CommFrame.ReceiveCaption;
            if (!string.Equals(frame.Caption, defaultCaption, StringComparison.Ordinal))
            {
                suffix = "    (" + frame.Caption + ")";
            }

            return time + "  " + tag + "  " + body + suffix;
        }

        /// <summary>
        /// 方向决定主色；提示类里带「异常 / 失败 / 非法 / 丢弃」的标红，
        /// 让现场一眼能从几十行报文里挑出问题帧。
        /// </summary>
        private static Color PickColor(CommFrame frame)
        {
            switch (frame.Direction)
            {
                case CommFrameDirection.Send:
                    return Color.FromArgb(24, 95, 165);
                case CommFrameDirection.Receive:
                    return Color.FromArgb(15, 110, 86);
                default:
                    return IsWarning(frame.Caption)
                        ? Color.FromArgb(163, 45, 45)
                        : Color.FromArgb(95, 94, 90);
            }
        }

        private static bool IsWarning(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            return text.IndexOf("异常", StringComparison.Ordinal) >= 0
                || text.IndexOf("失败", StringComparison.Ordinal) >= 0
                || text.IndexOf("非法", StringComparison.Ordinal) >= 0
                || text.IndexOf("丢弃", StringComparison.Ordinal) >= 0
                || text.IndexOf("超时", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// 超过行数上限后从头部裁掉。两个坑都在这里收着：
        /// <list type="number">
        /// <item><c>RichTextBox</c> 没有 <c>TextBox.Lines</c> 那种整体赋值，只能按换行符定位后删除；</item>
        /// <item><b>只读状态下给 <c>SelectedText</c> 赋值会被静默忽略</b>——不报错、界面也不变，
        /// 所以删除前后要临时放开 <c>ReadOnly</c>，否则日志会无限增长。</item>
        /// </list>
        /// 一次把所有超出上限的行一起删掉，避免每加一行就来回改一次只读开关。
        /// </summary>
        private void TrimHead()
        {
            if (MaxLines <= 0 || _lineCount <= MaxLines)
            {
                return;
            }

            try
            {
                int remove = _lineCount - MaxLines;

                // 定位到「第 remove 行之后」的偏移：跨 remove 个换行符
                string text = _box.Text;
                int cut = 0;
                for (int i = 0; i < remove; i++)
                {
                    int next = text.IndexOf('\n', cut);
                    if (next < 0)
                    {
                        // 实际换行数比计数少（例如句柄未创建时写入的行），以实际为准
                        cut = -1;
                        break;
                    }
                    cut = next + 1;
                }

                if (cut <= 0 || cut > text.Length)
                {
                    _lineCount = MaxLines;
                    return;
                }

                _box.ReadOnly = false;
                try
                {
                    _box.Select(0, cut);
                    _box.SelectedText = string.Empty;
                }
                finally
                {
                    _box.ReadOnly = true;
                }

                _lineCount = MaxLines;
            }
            catch
            {
                // 裁剪失败不影响日志展示（下次追加会再试）
            }
        }

        private void ScrollToEndIfNeeded()
        {
            if (_chkAutoScroll == null || !_chkAutoScroll.Checked)
            {
                return;
            }

            try
            {
                _box.SelectionStart = _box.TextLength;
                _box.SelectionLength = 0;
                _box.ScrollToCaret();
            }
            catch
            {
                // 句柄未创建时定位失败，忽略
            }
        }

        #endregion

        #region 工具栏

        private static Button CreateBarButton(string text, int left, int width)
        {
            return new Button
            {
                Text = text,
                Left = left,
                Top = 2,
                Width = width,
                Height = 22,
                FlatStyle = FlatStyle.System,
                Font = new Font("Microsoft Sans Serif", 8.25f),
            };
        }

        private void TogglePause()
        {
            _paused = !_paused;

            if (!_paused)
            {
                // 恢复：按收到的顺序补上暂停期间的日志
                while (_pending.Count > 0)
                {
                    AppendCore(_pending.Dequeue());
                }
            }

            RefreshPauseButton();
        }

        private void RefreshPauseButton()
        {
            if (_btnPause == null)
            {
                return;
            }

            if (_paused)
            {
                _btnPause.Text = _pending.Count > 0 ? "继续(" + _pending.Count + ")" : "继续";
                _btnPause.ForeColor = Color.FromArgb(163, 45, 45);
            }
            else
            {
                _btnPause.Text = "暂停";
                _btnPause.ForeColor = SystemColors.ControlText;
            }
        }

        private void CopyAll()
        {
            if (string.IsNullOrEmpty(_box.Text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(_box.Text);
            }
            catch (Exception)
            {
                // 剪贴板被别的进程占用时会抛异常，不该把界面搞崩
                MessageBox.Show("复制失败：剪贴板暂时不可用，请稍后再试", "提示");
            }
        }

        #endregion
    }
}
