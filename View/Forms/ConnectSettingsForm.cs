using System;
using System.Drawing;
using System.Windows.Forms;
using PlcTest.Models;

namespace PlcTest.View.Forms
{
    /// <summary>
    /// 连接/服务端设置对话框：选择通信方式与通讯协议。
    /// 主界面的 IP/端口输入框在 TCP 模式下分别表示 IP 与端口，串口模式下分别表示串口名与波特率。
    /// 协议与通讯方式都使用枚举，下拉框内容由 <see cref="ProtocolTypeExtensions.GetProtocols"/> 自动生成，
    /// 新增协议只需在 <see cref="ProtocolType"/> 枚举中增加一项。
    /// </summary>
    public class ConnectSettingsForm : Form
    {
        private readonly ComboBox _cmbCommunicationKind;
        private readonly ComboBox _cmbProtocol;
        private readonly bool _isServer;

        /// <summary>
        /// 选中的通信方式
        /// </summary>
        public CommunicationType Communication { get; private set; }

        /// <summary>
        /// 选中的通讯协议
        /// </summary>
        public ProtocolType Protocol { get; private set; }

        /// <summary>
        /// 客户端连接设置对话框
        /// </summary>
        public ConnectSettingsForm() : this(false)
        {
        }

        /// <summary>
        /// 设置对话框
        /// </summary>
        /// <param name="isServer">true 表示服务端模式（只列服务端协议）</param>
        public ConnectSettingsForm(bool isServer)
        {
            _isServer = isServer;
            Text = isServer ? "服务端设置" : "连接设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(320, 150);

            var labCommunicationKind = new Label
            {
                AutoSize = true,
                Location = new Point(24, 22),
                Text = "通信方式"
            };
            var labProtocol = new Label
            {
                AutoSize = true,
                Location = new Point(24, 62),
                Text = "通讯协议"
            };

            _cmbCommunicationKind = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 19),
                Width = 180
            };
            _cmbProtocol = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 59),
                Width = 180
            };

            // 通讯方式同样用枚举，显示名由 ToString 决定
            _cmbCommunicationKind.Items.Add(new ComboOption(CommunicationType.Tcp, "TCP"));
            _cmbCommunicationKind.Items.Add(new ComboOption(CommunicationType.Serial, "串口"));
            _cmbCommunicationKind.SelectedIndexChanged += (s, e) => RefreshProtocols();

            var btnOk = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(100, 105),
                Size = new Size(80, 28),
                Text = "确定"
            };
            var btnCancel = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(200, 105),
                Size = new Size(80, 28),
                Text = "取消"
            };
            btnOk.Click += (s, e) =>
            {
                if (_cmbCommunicationKind.SelectedItem is ComboOption communication &&
                    communication.Value is CommunicationType communicationType)
                {
                    Communication = communicationType;
                }
                if (_cmbProtocol.SelectedItem is ComboOption protocol &&
                    protocol.Value is ProtocolType protocolType)
                {
                    Protocol = protocolType;
                }
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                labCommunicationKind, _cmbCommunicationKind,
                labProtocol, _cmbProtocol,
                btnOk, btnCancel
            });

            _cmbCommunicationKind.SelectedIndex = 0;
        }

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
