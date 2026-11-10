using PlcTest.Client.ClientInterface;
using PlcTest.Protocols;
using PlcTest.Models;
using PlcTest.View.Controls;
using PlcTest.View.Support;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PlcTest.View.Forms
{
    /// <summary>
    /// 客户端调试工作台：一个连接一个窗口，地址为主输入、监视列表为常用视图。
    /// <para>
    /// 取代了原来那个「区域 + 起始 + 长度」的单段连续读窗体：现在是「多地址监视 + 单次读写 + 报文」，
    /// 从 MainForm 的 Function 区进入。
    /// </para>
    /// <para>
    /// 协议差异一律通过 <see cref="ProtocolCapability"/> 获取，本窗体里不出现任何厂商判断：
    /// 左侧软元件列表来自能力快照，地址解析与位/字校验由 <see cref="AddressTextParser"/> 负责。
    /// </para>
    /// </summary>
    public partial class PlcWorkspaceForm : Form
    {
        private IPlcClient _client;
        private ProtocolCapability _capability;
        /// <summary>当前生效的协议私有选项的显示文本（如「机型=Easy · 字节序=CDAB」），空串表示无选项</summary>
        private string _optionText;

        /// <summary>上一次自动填进地址框的软元件（切换列表项时才改，避免每次刷新都覆盖用户输入）</summary>
        private MemoryArea? _lastAreaPicked;

        /// <summary>
        /// 设计器专用构造：只建控件树，不碰数据。Visual Studio 用无参构造实例化窗体，
        /// 所以这里不能做任何依赖 IPlcClient 的事，否则设计器一打开就抛。
        /// </summary>
        public PlcWorkspaceForm()
        {
            InitializeComponent();

            // SplitterDistance 要等控件有真实尺寸才能设，设计器里不执行 Load
            Load += (s, e) => { try { _middle.SplitterDistance = 190; } catch { } };
        }

        /// <summary>
        /// 构造工作区。
        /// </summary>
        /// <param name="client">已连接的客户端</param>
        /// <param name="protocol">当前协议</param>
        /// <param name="endpoint">连接参数，仅用于标题</param>
        /// <param name="options">连接时使用的协议私有选项，仅用于在状态条上显示，可传 null</param>
        public PlcWorkspaceForm(IPlcClient client, ProtocolType protocol, string endpoint = null,
            ProtocolOptions options = null)
            : this()
        {
            InitializeRuntime(client, protocol, endpoint, options);
        }

        /// <summary>
        /// 运行期初始化：绑数据、挂事件、起状态定时器。与控件树（Designer）分开，
        /// 这样设计器只看到结构与属性，不会被真实客户端数据干扰。
        /// </summary>
        private void InitializeRuntime(IPlcClient client, ProtocolType protocol, string endpoint, ProtocolOptions options)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                // DesignMode 在构造函数里恒为 false，只能用 LicenseManager 判断
                return;
            }

            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            _client = client;
            _capability = ProtocolCapability.FromClient(client, protocol);
            _optionText = (options ?? new ProtocolOptions()).Describe(ProtocolOptionCatalog.ForClient(protocol));

            Text = "PLC 工作台 - " + protocol.GetDisplayName()
                + (string.IsNullOrEmpty(endpoint) ? string.Empty : " · " + endpoint);

            FillAreaList();
            _leftTitle.Text = "  软元件（" + _capability.Areas.Count + " 项）";
            _addressPanel.Capability = _capability;
            _monitor.Capability = _capability;
            _monitor.Reader = ReadForMonitor;

            _lstAreas.SelectedIndexChanged += (s, e) => OnAreaPicked();
            _addressPanel.AddressChanged += (s, e) => RefreshButtons();
            _btnRead.Click += OnReadClick;
            _btnClearResult.Click += (s, e) => _tbReadResult.Clear();
            _btnWrite.Click += OnWriteClick;

            _client.FrameEvent += _frameLog.Append;

            _statusTimer.Tick += (s, e) => RefreshStatus();
            _statusTimer.Start();

            if (_capability.Areas.Count > 0)
            {
                _lstAreas.SelectedIndex = 0;
            }
            RefreshStatus();
            RefreshButtons();
        }

        #region 左侧软元件

        private void FillAreaList()
        {
            foreach (DeviceAreaInfo info in _capability.Areas)
            {
                // 用全角空格补齐：中文字宽是西文两倍，用 string.Format 的字符数补位会参差不齐
                string access = info.SupportsBit && info.SupportsWord ? "位字"
                    : info.SupportsBit ? "位\u3000" : "\u3000字";
                _lstAreas.Items.Add(string.Format("{0,-4} {1} {2}", info.Area, access, info.Name));
            }
        }

        /// <summary>
        /// 选中软元件后，把地址框换成该区的 0 号点（只在真的换了区域时才覆盖，避免打断输入）。
        /// </summary>
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

        #region 读取 / 写入

        /// <summary>
        /// 监视列表的读取实现：位走 ReadBitArray，字走 ReadWordArray。
        /// </summary>
        private MonitorReadResult ReadForMonitor(PlcAddress address)
        {
            if (!_client.IsConnected)
            {
                return MonitorReadResult.Fail("未连接");
            }

            if (address.IsBit)
            {
                Result<bool[]> bits = _client.ReadBitArray(address);
                return bits.IsSuccess ? MonitorReadResult.OfBits(bits.Data) : MonitorReadResult.Fail(bits.ErrorMsg);
            }

            Result<short[]> words = _client.ReadWordArray(address);
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
                    Result<bool[]> result = await Task.Run(() => _client.ReadBitArray(address));
                    _tbReadResult.Text = result.IsSuccess
                        ? DescribeBits(address, result.Data)
                        : "读取失败：" + result.ErrorMsg;
                    if (!result.IsSuccess) { _frameLog.Append("读取失败：" + result.ErrorMsg); }
                }
                else
                {
                    Result<short[]> result = await Task.Run(() => _client.ReadWordArray(address));
                    _tbReadResult.Text = result.IsSuccess
                        ? DescribeWords(address, result.Data)
                        : "读取失败：" + result.ErrorMsg;
                    if (!result.IsSuccess) { _frameLog.Append("读取失败：" + result.ErrorMsg); }
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
                        ? await Task.Run(() => _client.WriteBit(address, values[0]))
                        : await Task.Run(() => _client.WriteBitArray(address, values));

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
                        ? await Task.Run(() => _client.WriteWord(address, values[0]))
                        : await Task.Run(() => _client.WriteWordArray(address, values));

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
                _frameLog.Append("写入成功：" + AddressTextParser.Format(address) + " × " + count);
                return;
            }

            MessageBox.Show("写入失败：" + result.ErrorMsg, "提示");
            _frameLog.Append("写入失败：" + result.ErrorMsg);
        }

        /// <summary>
        /// 位写入值：0/1，或 true/false，多个用空格或逗号分隔。
        /// </summary>
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

        /// <summary>
        /// 字写入值：十进制或 0x 前缀十六进制，多个用空格或逗号分隔。
        /// </summary>
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
            bool connected = _client.IsConnected;
            _labStatus.Text = _capability.DisplayName
                + (connected ? " · 已连接" : " · 未连接")
                + " · 软元件 " + _capability.Areas.Count + " 项"
                + (string.IsNullOrEmpty(_optionText) ? string.Empty : " · " + _optionText);
            _labStatus.ForeColor = connected ? Color.FromArgb(15, 110, 86) : Color.FromArgb(163, 45, 45);
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool ready = _client.IsConnected;
            _btnRead.Enabled = ready && _addressPanel.IsValid;
            _btnWrite.Enabled = ready && _addressPanel.IsValid;
        }

        private void SetBusy(bool busy)
        {
            _btnRead.Enabled = !busy && _client.IsConnected && _addressPanel.IsValid;
            _btnWrite.Enabled = !busy && _client.IsConnected && _addressPanel.IsValid;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        #endregion

        /// <summary>
        /// 释放：同样要退订事件、停表。Close() 会走 OnFormClosing，但 Designers/using 直接 Dispose 时不会，
        /// 少了这一层，定时器会在已销毁的控件上继续 Tick。
        /// </summary>
        /// <summary>
        /// 释放运行期资源：退订帧事件、停状态定时器。
        /// 由 Designer 的 Dispose 调用——Designer 已经声明了 Dispose(bool)，
        /// 这里再用 override 会重复定义，所以改成独立方法。
        /// </summary>
        private void ReleaseRuntime()
        {
            if (_client != null)
            {
                try { _client.FrameEvent -= _frameLog.Append; } catch { }
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
        /// 关窗：退订帧事件并停掉状态定时器，否则窗体释放后协议线程仍会回调。
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ReleaseRuntime();
            base.OnFormClosing(e);
        }
    }
}
