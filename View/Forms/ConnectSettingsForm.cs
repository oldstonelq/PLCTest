using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PlcTest.Models;
using PlcTest.Protocols;

namespace PlcTest.View.Forms
{
    /// <summary>
    /// 连接/服务端设置对话框：选择通信方式、通讯协议，以及该协议的私有选项。
    /// 主界面的 IP/端口输入框在 TCP 模式下分别表示 IP 与端口，串口模式下分别表示串口名与波特率。
    /// <para>
    /// 协议选项由 <see cref="ProtocolOptionCatalog"/> 的描述动态生成：这个对话框不认识任何厂商类型，
    /// 新增一个参数只需在登记表里加一行，界面自动多一个控件。没有可配项的协议（如三菱 MC）
    /// 整块隐藏，不会留一片空白。
    /// </para>
    /// </summary>
    public class ConnectSettingsForm : Form
    {
        private readonly ComboBox _cmbCommunicationKind;
        private readonly ComboBox _cmbProtocol;
        private readonly GroupBox _gbOptions;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly ToolTip _toolTip = new ToolTip();
        private readonly bool _isServer;

        /// <summary>当前显示的选项编辑器，与 <see cref="ProtocolOptionCatalog"/> 返回的描述一一对应</summary>
        private readonly List<OptionEditor> _editors = new List<OptionEditor>();

        /// <summary>
        /// 当前协议是否有可配项。不能直接用 <c>_gbOptions.Visible</c> 判断：
        /// 窗体还没 Show 时，子控件的 Visible 恒为 false（它反映的是「实际可见」），
        /// 会导致首次布局算成没有选项区。
        /// </summary>
        private bool _hasOptions;

        /// <summary>
        /// 选中的通信方式
        /// </summary>
        public CommunicationType Communication { get; private set; }

        /// <summary>
        /// 选中的通讯协议
        /// </summary>
        public ProtocolType Protocol { get; private set; }

        /// <summary>
        /// 当前协议选项的取值（每次读取都从控件现取，取消对话框时调用方本来也不会用）。
        /// 直接传给 Builder 的 <c>UseOptions</c>。
        /// </summary>
        public ProtocolOptions Options
        {
            get
            {
                ProtocolOptions collected;
                string error;
                return TryCollectOptions(out collected, out error) ? collected : new ProtocolOptions();
            }
        }

        /// <summary>
        /// 客户端连接设置对话框
        /// </summary>
        public ConnectSettingsForm() : this(false)
        {
        }

        /// <summary>
        /// 设置对话框
        /// </summary>
        /// <param name="isServer">true 表示服务端模式（只列服务端协议与选项）</param>
        public ConnectSettingsForm(bool isServer)
        {
            _isServer = isServer;

            Text = isServer ? "服务端设置" : "连接设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(340, 190);

            var labCommunicationKind = new Label
            {
                AutoSize = true,
                Location = new Point(20, 20),
                Text = "通信方式"
            };
            var labProtocol = new Label
            {
                AutoSize = true,
                Location = new Point(20, 56),
                Text = "通讯协议"
            };

            _cmbCommunicationKind = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 17),
                Width = 200
            };
            _cmbProtocol = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 53),
                Width = 200
            };

            // 通讯方式同样用枚举，显示名由 ToString 决定
            _cmbCommunicationKind.Items.Add(new ComboOption(CommunicationType.Tcp, "TCP"));

            // 串口只在真的有协议支持时才列出：当前所有协议的串口标志都是 false，
            // 留着这一项只会让用户点进去看到空列表。以后打开某个协议的串口，它会自动回来。
            if (ProtocolTypeExtensions.GetProtocols(CommunicationType.Serial, _isServer).Any())
            {
                _cmbCommunicationKind.Items.Add(new ComboOption(CommunicationType.Serial, "串口"));
            }
            _cmbCommunicationKind.SelectedIndexChanged += (s, e) => RefreshProtocols();

            _cmbProtocol.SelectedIndexChanged += (s, e) => RefreshOptions();

            _gbOptions = new GroupBox
            {
                Text = "协议选项",
                Location = new Point(16, 88),
                Size = new Size(308, 0),
                Visible = false,
            };

            _btnOk = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(116, 120),
                Size = new Size(96, 28),
                Text = "确定"
            };
            _btnCancel = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(222, 120),
                Size = new Size(96, 28),
                Text = "取消"
            };
            _btnOk.Click += (s, e) =>
            {
                if (!(_cmbProtocol.SelectedItem is ComboOption protocol) ||
                    !(protocol.Value is ProtocolType protocolType))
                {
                    // 没有可选项时不能让对话框带着默认值 0 关掉，否则会用错协议去连
                    MessageBox.Show("请选择通讯协议", "提示");
                    DialogResult = DialogResult.None;
                    return;
                }

                ProtocolOptions ignored;
                string error;
                if (!TryCollectOptions(out ignored, out error))
                {
                    MessageBox.Show(error, "协议选项有误");
                    DialogResult = DialogResult.None;
                    return;
                }

                if (_cmbCommunicationKind.SelectedItem is ComboOption communication &&
                    communication.Value is CommunicationType communicationType)
                {
                    Communication = communicationType;
                }
                Protocol = protocolType;
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Controls.AddRange(new Control[]
            {
                labCommunicationKind, _cmbCommunicationKind,
                labProtocol, _cmbProtocol,
                _gbOptions,
                _btnOk, _btnCancel
            });

            _cmbCommunicationKind.SelectedIndex = 0;
        }

        #region 协议与选项

        /// <summary>
        /// 根据通信方式刷新可选的通讯协议列表。
        /// </summary>
        private void RefreshProtocols()
        {
            if (!(_cmbCommunicationKind.SelectedItem is ComboOption option) ||
                !(option.Value is CommunicationType communication))
            {
                return;
            }

            _cmbProtocol.Items.Clear();
            foreach (var protocol in ProtocolTypeExtensions.GetProtocols(communication, _isServer))
            {
                _cmbProtocol.Items.Add(new ComboOption(protocol, protocol.GetDisplayName()));
            }

            if (_cmbProtocol.Items.Count > 0)
            {
                _cmbProtocol.SelectedIndex = 0;
            }
            else
            {
                RefreshOptions();
            }
        }

        /// <summary>
        /// 按当前协议重建选项控件。没有可配项时整块隐藏，并让对话框缩回去。
        /// </summary>
        private void RefreshOptions()
        {
            ProtocolType protocol;
            if (!TryGetSelectedProtocol(out protocol))
            {
                _editors.Clear();
                _gbOptions.Controls.Clear();
                _hasOptions = false;
                _gbOptions.Visible = false;
                Relayout();
                return;
            }

            IReadOnlyList<ProtocolOption> schema = _isServer
                ? ProtocolOptionCatalog.ForServer(protocol)
                : ProtocolOptionCatalog.ForClient(protocol);

            _gbOptions.SuspendLayout();
            try
            {
                _gbOptions.Controls.Clear();
                _editors.Clear();

                int y = 22;
                foreach (ProtocolOption option in schema)
                {
                    _gbOptions.Controls.Add(new Label
                    {
                        Text = option.Label,
                        AutoSize = true,
                        Location = new Point(12, y + 3),
                    });

                    Control editor;
                    if (option.Kind == ProtocolOptionKind.Choice)
                    {
                        var combo = new ComboBox
                        {
                            Location = new Point(104, y),
                            Width = 190,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                        };
                        foreach (ProtocolOptionValue choice in option.Choices)
                        {
                            combo.Items.Add(choice);
                        }
                        combo.SelectedIndex = IndexOfValue(combo, option.DefaultValue);
                        editor = combo;
                    }
                    else
                    {
                        editor = new TextBox
                        {
                            Location = new Point(104, y),
                            Width = 190,
                            Text = Convert.ToString(option.DefaultValue),
                        };
                    }

                    if (!string.IsNullOrEmpty(option.Hint))
                    {
                        _toolTip.SetToolTip(editor, option.Hint);
                    }

                    _gbOptions.Controls.Add(editor);
                    _editors.Add(new OptionEditor(option, editor));
                    y += 30;
                }

                _hasOptions = schema.Count > 0;
                _gbOptions.Visible = _hasOptions;
                _gbOptions.Height = _hasOptions ? y + 4 : 0;
            }
            finally
            {
                _gbOptions.ResumeLayout();
            }

            Relayout();
        }

        /// <summary>
        /// 选项区高度变化后重排按钮与对话框高度。这里用固定像素而不是 AutoSize：
        /// 固定对话框 + 手动算高度更好预测，也不会出现 AutoSize 反复抖动的老问题。
        /// </summary>
        private void Relayout()
        {
            int top = _hasOptions ? _gbOptions.Top + _gbOptions.Height + 12 : _cmbProtocol.Bottom + 16;
            _btnOk.Location = new Point(116, top);
            _btnCancel.Location = new Point(222, top);
            ClientSize = new Size(340, top + 36);
        }

        /// <summary>
        /// 收集并校验选项：整数越界或为空时返回 false，由调用方阻止对话框关闭。
        /// </summary>
        private bool TryCollectOptions(out ProtocolOptions options, out string error)
        {
            options = new ProtocolOptions();
            error = null;

            foreach (OptionEditor editor in _editors)
            {
                object value;

                if (editor.Option.Kind == ProtocolOptionKind.Choice)
                {
                    var combo = editor.Editor as ComboBox;
                    if (combo == null || !(combo.SelectedItem is ProtocolOptionValue choice))
                    {
                        error = "请选择" + editor.Option.Label;
                        return false;
                    }
                    value = choice.Value;
                }
                else
                {
                    int number;
                    if (!int.TryParse(editor.Editor.Text.Trim(), out number))
                    {
                        error = editor.Option.Label + " 必须是整数";
                        return false;
                    }
                    value = number;
                }

                string reason;
                if (!editor.Option.TryAccept(value, out reason))
                {
                    error = reason;
                    return false;
                }

                options.Set(editor.Option.Key, value);
            }

            return true;
        }

        private bool TryGetSelectedProtocol(out ProtocolType protocol)
        {
            protocol = ProtocolType.MelsecMc3EBinary;
            return _cmbProtocol.SelectedItem is ComboOption option
                && option.Value is ProtocolType
                && Enum.TryParse(option.Value.ToString(), out protocol);
        }

        private static int IndexOfValue(ComboBox combo, object value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as ProtocolOptionValue;
                if (item != null && Equals(item.Value, value))
                {
                    return i;
                }
            }
            return combo.Items.Count > 0 ? 0 : -1;
        }

        #endregion

        /// <summary>
        /// 一个选项与它对应的编辑控件。
        /// </summary>
        private sealed class OptionEditor
        {
            public ProtocolOption Option { get; private set; }
            public Control Editor { get; private set; }

            public OptionEditor(ProtocolOption option, Control editor)
            {
                Option = option;
                Editor = editor;
            }
        }

        /// <summary>
        /// 下拉框项：Value 为枚举值，Text 为界面显示名。
        /// </summary>
        private sealed class ComboOption
        {
            public object Value { get; }
            public string Text { get; }

            public ComboOption(object value, string text)
            {
                Value = value;
                Text = text;
            }
            /// <summary>
            /// 没有设置 DisplayMember 指定属性
            /// 重写 ToString 方法返回 Text，确保下拉框显示界面名。
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return Text;
            }
        }
    }
}
