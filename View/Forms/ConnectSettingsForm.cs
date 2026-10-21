using System;
using System.Drawing;
using System.Windows.Forms;

namespace PlcTest.View.Forms
{
    /// <summary>
    /// 连接/服务端设置对话框：选择通信方式与通讯协议。
    /// 主界面的 IP/端口输入框在 TCP 模式下分别表示 IP 与端口，串口模式下分别表示串口名与波特率。
    /// 服务端模式下只列出服务端已实现的协议（三菱 MC 系列）。
    /// </summary>
    public class ConnectSettingsForm : Form
    {
        private readonly ComboBox _cmbCommunicationKind;
        private readonly ComboBox _cmbProtocol;
        private readonly bool _isServer;

        /// <summary>
        /// 选中的通信方式："TCP" 或 "串口"
        /// </summary>
        public string CommunicationKind { get; private set; }

        /// <summary>
        /// 选中的通讯协议显示名
        /// </summary>
        public string ProtocolName { get; private set; }

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

            _cmbCommunicationKind.Items.AddRange(new object[] { "TCP", "串口" });
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
                CommunicationKind = _cmbCommunicationKind.SelectedItem as string;
                ProtocolName = _cmbProtocol.SelectedItem as string;
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
            _cmbProtocol.Items.Clear();

            if (_isServer)
            {
                if (_cmbCommunicationKind.SelectedIndex == 0)
                {
                    // TCP：3E / 4E 帧
                    _cmbProtocol.Items.AddRange(new object[]
                    {
                        "三菱 MC 3E Binary",
                        "三菱 MC 3E ASCII",
                        "三菱 MC 4E Binary",
                        "三菱 MC 4E ASCII"
                    });
                }
                else
                {
                    // 串口：1E 帧（无外层头，直接帧）
                    _cmbProtocol.Items.AddRange(new object[]
                    {
                        "三菱 MC 1E Binary",
                        "三菱 MC 1E ASCII"
                    });
                }
            }
            else if (_cmbCommunicationKind.SelectedIndex == 0)
            {
                _cmbProtocol.Items.AddRange(new object[]
                {
                    "三菱 MC 3E Binary",
                    "三菱 MC 3E ASCII",
                    "三菱 MC 1E Binary",
                    "三菱 MC 1E ASCII",
                    "三菱 MC 4E Binary",
                    "三菱 MC 4E ASCII",
                    "汇川 Modbus TCP",
                    "西门子 S7-1200"
                });
            }
            else
            {
                _cmbProtocol.Items.AddRange(new object[]
                {
                    "三菱 MC 1E Binary",
                    "三菱 MC 1E ASCII",
                    "汇川 Modbus RTU"
                });
            }

            if (_cmbProtocol.Items.Count > 0)
            {
                _cmbProtocol.SelectedIndex = 0;
            }
        }
    }
}
