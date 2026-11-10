using PlcTest.Models;
using PlcTest.Protocols;
using PlcTest.Server.ServerInterface;
using PlcTest.View.Controls;
using PlcTest.View.Support;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PlcTest.View.Forms
{
    /// <summary>
    /// 服务端调试工作台：维护模拟寄存器、直接改写模拟值、看报文。
    /// <para>
    /// 取代了原来那张 21 列的 16 位表格（只能看一段连续区域）。这里与客户端工作台共用
    /// 「地址 / 方式 / 长度」视图和报文控件，两边看到的东西一致，联调时不用在两套表格之间换算。
    /// </para>
    /// <para>
    /// 三种「清」的区别在界面上要能分清（这是之前的界面踩过的坑）：
    /// <list type="bullet">
    /// <item><b>清零区间 / 全部清零</b>：只把值置 0，<b>地址保留</b>，客户端仍能读到；</item>
    /// <item><b>移除区域</b>：把地址从模拟表里删掉，客户端再访问该地址就会报「地址不存在」。</item>
    /// </list>
    /// </para>
    /// </summary>
    public partial class PlcServerWorkspaceForm : Form
    {
        private IPlcServer _server;
        private IRegisterTable _registerTable;
        private ProtocolCapability _capability;
        /// <summary>当前生效的协议私有选项的显示文本，空串表示该协议没有可配项</summary>
        private string _optionText;
        private readonly Random _random = new Random();

        private MemoryArea? _lastAreaPicked;

        /// <summary>
        /// 设计器专用构造：只建控件树。Visual Studio 用无参构造实例化窗体，
        /// 所以这里不能碰 IPlcServer，否则设计器一打开就抛。
        /// </summary>
        public PlcServerWorkspaceForm()
        {
            InitializeComponent();

            // SplitterDistance 要等控件有真实尺寸才能设，设计器里不执行 Load
            Load += (s, e) => { try { _middle.SplitterDistance = 190; } catch { } };
        }

        /// <summary>
        /// 构造服务端工作台。
        /// </summary>
        /// <param name="server">已启动（或至少已创建）的模拟服务端</param>
        /// <param name="protocol">当前协议</param>
        /// <param name="endpoint">监听地址，仅用于标题</param>
        /// <param name="options">启动时使用的协议私有选项，仅用于状态条显示，可传 null</param>
        public PlcServerWorkspaceForm(IPlcServer server, ProtocolType protocol, string endpoint = null,
            ProtocolOptions options = null)
            : this()
        {
            InitializeRuntime(server, protocol, endpoint, options);
        }

        /// <summary>
        /// 运行期初始化：绑数据、挂事件、起状态定时器。控件树在 Designer 里。
        /// </summary>
        private void InitializeRuntime(IPlcServer server, ProtocolType protocol, string endpoint, ProtocolOptions options)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                // DesignMode 在构造函数里恒为 false，只能用 LicenseManager 判断
                return;
            }

            if (server == null)
            {
                throw new ArgumentNullException("server");
            }

            _server = server;
            _registerTable = server as IRegisterTable;
            _capability = ProtocolCapability.FromServer(server, protocol);
            _optionText = (options ?? new ProtocolOptions()).Describe(ProtocolOptionCatalog.ForServer(protocol));

            Text = "PLC 服务端工作台 - " + protocol.GetDisplayName()
                + (string.IsNullOrEmpty(endpoint) ? string.Empty : " · " + endpoint);

            FillAreaList();
            _leftTitle.Text = "  软元件（" + _capability.Areas.Count + " 项）";
            _addressPanel.Capability = _capability;
            _monitor.Capability = _capability;
            _monitor.Reader = ReadForMonitor;

            // 没有模拟寄存器表的服务端，把提示文案换成说明而不是让人点了才弹窗
            if (_registerTable == null)
            {
                _registerHint.Text = "该服务端不维护模拟寄存器表";
            }

            _lstAreas.SelectedIndexChanged += (s, e) => OnAreaPicked();
            _addressPanel.AddressChanged += (s, e) => RefreshButtons();

            _btnInit.Click += (s, e) => InitializeRange();
            _btnResetRange.Click += (s, e) => ResetRange();
            _btnResetAll.Click += (s, e) => ResetAll();
            _btnRemoveArea.Click += (s, e) => RemoveArea();
            _btnRandom.Click += (s, e) => FillRandom();
            _btnRead.Click += OnReadClick;
            _btnClearResult.Click += (s, e) => _tbReadResult.Clear();
            _btnWrite.Click += OnWriteClick;

            _server.FrameEvent += _frameLog.Append;

            _statusTimer.Tick += (s, e) => RefreshStatus();
            _statusTimer.Start();

            if (_capability.Areas.Count > 0)
            {
                _lstAreas.SelectedIndex = 0;
            }
            RefreshStatus();
            RefreshButtons();
        }

        #region 软元件列表

        private void FillAreaList()
        {
            foreach (DeviceAreaInfo info in _capability.Areas)
            {
                // 全角空格补齐：中文字宽是西文两倍，按字符数补位会参差不齐
                string access = info.SupportsBit && info.SupportsWord ? "位字"
                    : info.SupportsBit ? "位\u3000" : "\u3000字";
                _lstAreas.Items.Add(string.Format("{0,-4} {1} {2}", info.Area, access, info.Name));
            }
        }

        private void OnAreaPicked()
        {
            if (_lstAreas.SelectedIndex < 0 || _lstAreas.SelectedIndex >= _capability.Areas.Count)
            {
                return;
            }

            DeviceAreaInfo info = _capability.Areas[_lstAreas.SelectedIndex];
            if (_lastAreaPicked.HasValue && info.Area == _lastAreaPicked.Value)
            {
                return;
            }
            _lastAreaPicked = info.Area;

            AddressValueKind kind = info.SupportsBit ? AddressValueKind.Bit : AddressValueKind.Word;
            _addressPanel.Kind = kind;
            _addressPanel.SetAddress(info.Area.ToString() + "0");
            _monitor.SuggestAddress(info.Area.ToString() + "0", kind);
        }

        #endregion

        #region 寄存器操作

        /// <summary>
        /// 把地址栏指定的区间注册进模拟表（可重复调用，已存在的地址不会重复添加）。
        /// </summary>
        /// <summary>
        /// 复制地址、只改偏移。DB 区的块号在 <see cref="PlcAddress.DbNumber"/> 里，
        /// 逐个注册时必须带上，否则每个地址都会落到 0 号块。
        /// </summary>
        private static PlcAddress WithOffset(PlcAddress address, int offset)
        {
            return address.IsBit
                ? PlcAddress.Bit(address.Area, offset, address.DbNumber)
                : PlcAddress.Word(address.Area, offset, address.DbNumber);
        }

        private void InitializeRange()
        {
            if (!RequireRegisters(out PlcAddress address))
            {
                return;
            }

            int added = 0;
            for (int i = address.Address; i < address.Address + address.Length; i++)
            {
                // 必须带上 DbNumber：S7 的 DB1.DBW0 与 DB2.DBW0 是两块内存，
                // 用 (area, address) 注册会把它们合并到同一块（等于只有 0 号块生效）
                if (_registerTable.TryAddRegister(WithOffset(address, i)))
                {
                    added++;
                }
            }

            _frameLog.Append(string.Format("已初始化 {0} 起 {1} 个地址（新增 {2} 个）",
                AddressTextParser.Format(address), address.Length, added));
            _monitor.PollOnce();
        }

        /// <summary>
        /// 只把区间内的值置 0，地址保留——客户端仍然能读到 0，而不是"地址不存在"。
        /// </summary>
        private void ResetRange()
        {
            if (!RequireRegisters(out PlcAddress address))
            {
                return;
            }

            _registerTable.ResetRegisterValues(address, address.Length);
            _frameLog.Append(string.Format("{0} 起 {1} 个地址已清零（地址保留）",
                AddressTextParser.Format(address), address.Length));
            _monitor.PollOnce();
        }

        private void ResetAll()
        {
            if (!RequireRegisters(out PlcAddress address))
            {
                return;
            }

            _registerTable.ResetAllRegisterValues(address.Area);
            _frameLog.Append(address.Area + " 区域全部地址已清零（地址保留）");
            _monitor.PollOnce();
        }

        /// <summary>
        /// 删掉整片区域的地址。这是破坏性操作，先确认——之后客户端访问会报地址不存在。
        /// </summary>
        private void RemoveArea()
        {
            if (!RequireRegisters(out PlcAddress address))
            {
                return;
            }

            if (MessageBox.Show(
                    "将把 " + address.Area + " 区域的模拟地址全部删除，之后客户端访问该区域会报地址不存在。要继续吗？",
                    "移除区域", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            _registerTable.ClearRegisters(address.Area);
            _frameLog.Append(address.Area + " 区域的模拟地址已全部移除");
            _monitor.PollOnce();
        }

        /// <summary>
        /// 给区间内的模拟寄存器填随机值，用来观察客户端侧的变化（波形、报警、数值刷新）。
        /// </summary>
        private void FillRandom()
        {
            if (!RequireRegisters(out PlcAddress address))
            {
                return;
            }

            bool isBit = _addressPanel.Kind == AddressValueKind.Bit
                && _addressPanel.Address != null
                && _addressPanel.Address.IsBit;

            for (int i = 0; i < address.Length; i++)
            {
                int offset = address.Address + i;
                if (!_registerTable.ContainsRegister(address.Area, offset))
                {
                    continue;
                }

                if (isBit)
                {
                    _server.WriteBit(PlcAddress.Bit(address.Area, offset), _random.Next(2) == 1);
                }
                else
                {
                    _server.WriteWord(PlcAddress.Word(address.Area, offset), (short)_random.Next(0, 1000));
                }
            }

            _frameLog.Append(string.Format("{0}{1} 起 {2} 个地址已随机填充", address.Area, address.Address, address.Length));
            _monitor.PollOnce();
        }

        /// <summary>
        /// 寄存器操作的前置校验：需要地址有效 + 服务端实现了 IRegisterTable。
        /// </summary>
        private bool RequireRegisters(out PlcAddress address)
        {
            address = null;

            if (_registerTable == null)
            {
                MessageBox.Show("当前服务端不维护模拟寄存器表，无法执行该操作", "提示");
                return false;
            }

            if (!_addressPanel.IsValid)
            {
                MessageBox.Show("地址不可用：" + (_addressPanel.ErrorMessage ?? "请先输入地址"), "提示");
                return false;
            }

            address = _addressPanel.Address;
            address.Length = _addressPanel.Length;
            return true;
        }

        #endregion

        #region 读取 / 写入

        private MonitorReadResult ReadForMonitor(PlcAddress address)
        {
            if (!_server.ServerIsOpen)
            {
                return MonitorReadResult.Fail("服务未启动");
            }

            if (address.IsBit)
            {
                Result<bool[]> bits = _server.ReadBitArray(address);
                return bits.IsSuccess ? MonitorReadResult.OfBits(bits.Data) : MonitorReadResult.Fail(bits.ErrorMsg);
            }

            Result<short[]> words = _server.ReadWordArray(address);
            return words.IsSuccess ? MonitorReadResult.OfWords(words.Data) : MonitorReadResult.Fail(words.ErrorMsg);
        }

        private async void OnReadClick(object sender, EventArgs e)
        {
            if (!_addressPanel.IsValid)
            {
                _tbReadResult.Text = "地址不可用：" + (_addressPanel.ErrorMessage ?? "请先输入地址");
                return;
            }

            PlcAddress address = _addressPanel.Address;
            address.Length = _addressPanel.Length;

            SetBusy(true);
            try
            {
                if (address.IsBit)
                {
                    Result<bool[]> result = await Task.Run(() => _server.ReadBitArray(address));
                    _tbReadResult.Text = result.IsSuccess
                        ? DescribeBits(address, result.Data)
                        : "读取失败：" + result.ErrorMsg;
                }
                else
                {
                    Result<short[]> result = await Task.Run(() => _server.ReadWordArray(address));
                    _tbReadResult.Text = result.IsSuccess
                        ? DescribeWords(address, result.Data)
                        : "读取失败：" + result.ErrorMsg;
                }
            }
            catch (Exception ex)
            {
                _tbReadResult.Text = "读取异常：" + ex.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void OnWriteClick(object sender, EventArgs e)
        {
            if (!_addressPanel.IsValid)
            {
                MessageBox.Show("地址不可用：" + (_addressPanel.ErrorMessage ?? "请先输入地址"), "提示");
                return;
            }

            PlcAddress address = _addressPanel.Address;
            string text = _tbWriteValue.Text;

            SetBusy(true);
            try
            {
                if (address.IsBit)
                {
                    bool[] values;
                    string error;
                    if (!TryParseBits(text, out values, out error))
                    {
                        MessageBox.Show(error, "写入值有误");
                        return;
                    }

                    address.Length = values.Length;
                    Result<bool> result = values.Length == 1
                        ? await Task.Run(() => _server.WriteBit(address, values[0]))
                        : await Task.Run(() => _server.WriteBitArray(address, values));

                    ShowWriteResult(result, address, values.Length);
                }
                else
                {
                    short[] values;
                    string error;
                    if (!TryParseWords(text, out values, out error))
                    {
                        MessageBox.Show(error, "写入值有误");
                        return;
                    }

                    address.Length = values.Length;
                    Result<bool> result = values.Length == 1
                        ? await Task.Run(() => _server.WriteWord(address, values[0]))
                        : await Task.Run(() => _server.WriteWordArray(address, values));

                    ShowWriteResult(result, address, values.Length);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("写入异常：" + ex.Message, "提示");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ShowWriteResult(Result<bool> result, PlcAddress address, int count)
        {
            if (result.IsSuccess)
            {
                _frameLog.Append("模拟值已写入：" + AddressTextParser.Format(address) + " × " + count);
                _monitor.PollOnce();
                return;
            }

            MessageBox.Show("写入失败：" + result.ErrorMsg, "提示");
        }

        private static bool TryParseBits(string text, out bool[] values, out string error)
        {
            values = null;
            error = null;

            string[] parts = SplitValues(text);
            if (parts.Length == 0)
            {
                error = "请输入写入值，例如 1 或 1,0,1";
                return false;
            }

            values = new bool[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i];
                if (token == "1" || string.Equals(token, "true", StringComparison.OrdinalIgnoreCase))
                {
                    values[i] = true;
                }
                else if (token == "0" || string.Equals(token, "false", StringComparison.OrdinalIgnoreCase))
                {
                    values[i] = false;
                }
                else
                {
                    error = "第 " + (i + 1) + " 个值不是 0/1：" + token;
                    return false;
                }
            }
            return true;
        }

        private static bool TryParseWords(string text, out short[] values, out string error)
        {
            values = null;
            error = null;

            string[] parts = SplitValues(text);
            if (parts.Length == 0)
            {
                error = "请输入写入值，例如 100 或 100,200";
                return false;
            }

            values = new short[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i];
                long value;
                bool ok = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? long.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value)
                    : long.TryParse(token, out value);

                if (!ok || value < short.MinValue || value > ushort.MaxValue)
                {
                    error = "第 " + (i + 1) + " 个值超出 16 位范围：" + token;
                    return false;
                }

                values[i] = unchecked((short)value);
            }
            return true;
        }

        private static string[] SplitValues(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new string[0];
            }

            return text.Split(new char[] { ' ', ',', '\t', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string DescribeBits(PlcAddress address, bool[] values)
        {
            if (values == null || values.Length == 0)
            {
                return "没有数据";
            }

            var builder = new StringBuilder();
            builder.AppendLine(AddressTextParser.Format(address) + " 起 " + values.Length + " 点（按位）");

            int on = 0;
            for (int i = 0; i < values.Length; i++)
            {
                builder.Append(values[i] ? "1" : "0");
                builder.Append(i % 8 == 7 ? "  " : " ");
                if (values[i]) { on++; }
            }

            builder.AppendLine();
            builder.AppendLine();
            builder.Append("共 " + values.Length + " 点，其中 " + on + " 点为 1");
            return builder.ToString();
        }

        private static string DescribeWords(PlcAddress address, short[] values)
        {
            if (values == null || values.Length == 0)
            {
                return "没有数据";
            }

            var builder = new StringBuilder();
            builder.AppendLine(AddressTextParser.Format(address) + " 起 " + values.Length + " 字（按字）");

            var hex = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                builder.Append(values[i]);
                builder.Append(i % 16 == 15 ? "  " : ", ");
                hex.Append(((ushort)values[i]).ToString("X4"));
                hex.Append(i % 16 == 15 ? "  " : " ");
            }

            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("HEX: " + hex.ToString().TrimEnd());
            return builder.ToString();
        }

        #endregion

        #region 状态

        private void RefreshStatus()
        {
            bool running = _server.ServerIsOpen;
            _labStatus.Text = _capability.DisplayName
                + (running ? " · 运行中" : " · 已停止")
                + " · 客户端 " + _server.ClientCount
                + " · 软元件 " + _capability.Areas.Count + " 项"
                + (string.IsNullOrEmpty(_optionText) ? string.Empty : " · " + _optionText);
            _labStatus.ForeColor = running ? Color.FromArgb(15, 110, 86) : Color.FromArgb(163, 45, 45);
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool ready = _server.ServerIsOpen && _registerTable != null;
            bool addressOk = _addressPanel.IsValid;

            _btnInit.Enabled = ready && addressOk;
            _btnResetRange.Enabled = ready && addressOk;
            _btnResetAll.Enabled = ready && addressOk;
            _btnRemoveArea.Enabled = ready && addressOk;
            _btnRandom.Enabled = ready && addressOk;

            _btnRead.Enabled = _server.ServerIsOpen && addressOk;
            _btnWrite.Enabled = _server.ServerIsOpen && addressOk;
        }

        private void SetBusy(bool busy)
        {
            if (busy)
            {
                _btnRead.Enabled = false;
                _btnWrite.Enabled = false;
                Cursor = Cursors.WaitCursor;
            }
            else
            {
                RefreshButtons();
                Cursor = Cursors.Default;
            }
        }

        #endregion

        /// <summary>
        /// 释放运行期资源：退订帧事件、停状态定时器。
        /// 由 Designer 的 Dispose 调用——Designer 已经声明了 Dispose(bool)，这里再 override 会重复定义。
        /// </summary>
        private void ReleaseRuntime()
        {
            if (_server != null)
            {
                try { _server.FrameEvent -= _frameLog.Append; } catch { }
            }

            try
            {
                if (_statusTimer != null)
                {
                    _statusTimer.Stop();
                }
            }
            catch
            {
                // 停表失败不影响关闭
            }
        }

        /// <summary>
        /// 关窗：与 Dispose 相同的清理，走 Close() 路径时执行。
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ReleaseRuntime();
            base.OnFormClosing(e);
        }
    }
}
