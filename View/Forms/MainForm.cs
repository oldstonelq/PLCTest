using PlcTest.Client;
using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using PlcTest.Server;
using PlcTest.Server.ServerInterface;
using PlcTest.Tool;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace PlcTest.View.Forms
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// PLC实例
        /// </summary>
        IPlcClient plcControl = null;
        /// <summary>
        /// 新的调试工作台（地址字符串输入 + 监视列表 + 报文），与旧窗体并存
        /// </summary>
        PlcWorkspaceForm plcWorkspaceForm = null;
        /// <summary>最近一次成功连接使用的协议（窗体标题与软元件视图要用）</summary>
        private ProtocolType? _clientProtocol;
        /// <summary>连接参数，如 192.168.1.10:5000 或 COM3 9600</summary>
        private string _clientEndpoint;
        /// <summary>连接时使用的协议私有选项，工作台用来在状态条上显示</summary>
        private ProtocolOptions _clientOptions;
        /// <summary>最近一次成功启动的服务端协议</summary>
        private ProtocolType? _serverProtocol;
        /// <summary>服务端监听参数</summary>
        private string _serverEndpoint;
        /// <summary>启动服务端时使用的协议私有选项</summary>
        private ProtocolOptions _serverOptions;
        /// <summary>
        /// PLC服务实例
        /// </summary>
        IPlcServer PlcServer = null;
        /// <summary>
        /// 服务端调试工作台（模拟寄存器 + 报文），与旧窗体并存
        /// </summary>
        PlcServerWorkspaceForm plcServerWorkspaceForm = null;
        /// <summary>
        ///  这个属性可以增加界面动态加载的流畅性
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // Turn on WS_EX_COMPOSITED 
                return cp;
            }
        }
        /// <summary>
        /// 构造函数
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Text = OtherTool.GetSoftwareVersion();
        }
        #region Button
        /// <summary>
        /// PLC客户端连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Connect_Click(object sender, EventArgs e)
        {
            bool useSerial;
            ProtocolType protocol;
            ProtocolOptions protocolOptions;
            using (var settings = new ConnectSettingsForm())
            {
                if (settings.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                useSerial = settings.Communication == CommunicationType.Serial;
                protocol = settings.Protocol;
                // 对话框是 using 的，选项要在块内取出来，否则释放后拿到的是空集合
                protocolOptions = settings.Options;
            }

            int number;
            if (useSerial)
            {
                // 串口模式下：IP 输入框填写串口名称（如 COM3），端口输入框填写波特率
                if (string.IsNullOrWhiteSpace(tb_PLCInformationIP.Text))
                {
                    tb_PLCInformationIP.Focus();
                    tb_PLCInformationIP.SelectAll();
                    MessageBox.Show("请在 IP 输入框填写串口名称（如 COM3）");
                    return;
                }
                if (!OtherTool.StringTryParseInt(tb_PLCInformationPort.Text, out number, 1200, 921600))
                {
                    tb_PLCInformationPort.Focus();
                    tb_PLCInformationPort.SelectAll();
                    MessageBox.Show("请在端口输入框填写波特率（1200-921600）");
                    return;
                }
            }
            else
            {
                if (!OtherTool.CheckStandardIPv4(tb_PLCInformationIP.Text, out var iPAddress))
                {
                    tb_PLCInformationIP.Focus();
                    tb_PLCInformationIP.SelectAll();
                    MessageBox.Show("Please Check PLCInformation IP");
                    return;
                }
                if (!OtherTool.StringTryParseInt(tb_PLCInformationPort.Text, out number, 1, 65535))
                {
                    tb_PLCInformationPort.Focus();
                    tb_PLCInformationPort.SelectAll();
                    MessageBox.Show("Please Check PLCInformation Port");
                    return;
                }
            }

            try
            {
                // 使用依赖注入：将通信实现和协议实现注入到客户端，避免硬编码
                var builder = new PlcClientBuilder();
                if (useSerial)
                {
                    builder.UseSerial(tb_PLCInformationIP.Text.Trim(), number, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
                }
                else
                {
                    builder.UseTcp(tb_PLCInformationIP.Text.Trim(), number);
                }

                // 协议分派收敛在 Builder 里：新增协议只需在 UseProtocol 补一个 case，
                // 界面这里不再需要知道「哪个厂商对应哪个 Build 方法」
                try
                {
                    plcControl = builder.UseProtocol(protocol).UseOptions(protocolOptions).Build();
                }
                catch (ArgumentOutOfRangeException)
                {
                    MessageBox.Show("不支持的协议：" + protocol.GetDisplayName());
                    return;
                }

                // 连接结果同步返回：失败原因能直接显示，不再出现"点了连接却没反应"
                Result<bool> connectResult = plcControl.Connect();
                if (!connectResult.IsSuccess)
                {
                    plcControl = null;
                    MessageBox.Show(connectResult.ErrorMsg, "连接失败");
                }
                else
                {
                    // 记下协议与连接参数，供子窗体标题与软元件视图使用
                    _clientProtocol = protocol;
                    _clientOptions = protocolOptions;
                    _clientEndpoint = useSerial
                        ? tb_PLCInformationIP.Text.Trim() + " " + number
                        : tb_PLCInformationIP.Text.Trim() + ":" + number;
                }
            }
            catch (Exception ex)
            {
                plcControl = null;
                MessageBox.Show($"连接失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 断开PLC客户端连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Disconnect_Click(object sender, EventArgs e)
        {
            if (plcWorkspaceForm != null && !plcWorkspaceForm.IsDisposed)
            {
                plcWorkspaceForm.Close();
            }
            plcWorkspaceForm = null;

            if (plcControl != null)
            {
                plcControl.Disconnect();
                plcControl = null;
            }
            else
            {
                plcControl = null;
            }

            _clientProtocol = null;
            _clientEndpoint = null;
            _clientOptions = null;
        }
        /// <summary>
        /// 打开调试工作台（唯一的寄存器视图入口）。
        /// </summary>
        private void btn_PLCWorkspace_Click(object sender, EventArgs e)
        {
            if (plcControl == null || !plcControl.IsConnected)
            {
                MessageBox.Show("Please Check PLC Connected");
                return;
            }

            if (plcWorkspaceForm == null || plcWorkspaceForm.IsDisposed)
            {
                ProtocolType protocol = _clientProtocol ?? ProtocolType.MelsecMc3EBinary;
                plcWorkspaceForm = new PlcWorkspaceForm(plcControl, protocol, _clientEndpoint, _clientOptions);
                plcWorkspaceForm.Show();
            }
            else
            {
                plcWorkspaceForm.Show();
                plcWorkspaceForm.Activate();
            }
        }
        /// <summary>
        /// 打开服务端调试工作台：维护模拟寄存器、改写模拟值、看报文。
        /// </summary>
        private void btn_ServerWorkspace_Click(object sender, EventArgs e)
        {
            if (PlcServer == null || !PlcServer.ServerIsOpen)
            {
                MessageBox.Show("Please Check PlcServer Open");
                return;
            }

            if (plcServerWorkspaceForm == null || plcServerWorkspaceForm.IsDisposed)
            {
                ProtocolType protocol = _serverProtocol ?? ProtocolType.MelsecMc3EBinary;
                plcServerWorkspaceForm = new PlcServerWorkspaceForm(PlcServer, protocol, _serverEndpoint, _serverOptions);
                plcServerWorkspaceForm.Show();
            }
            else
            {
                plcServerWorkspaceForm.Show();
                plcServerWorkspaceForm.Activate();
            }
        }
        /// <summary>
        /// 开启PLC服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_OpenServer_Click(object sender, EventArgs e)
        {
            // 先选择通信方式与协议，再校验地址/端口
            var settings = new ConnectSettingsForm(true);
            if (settings.ShowDialog(this) != DialogResult.OK)
                return;

            PlcServerBuilder builder;
            if (settings.Communication == CommunicationType.Serial)
            {
                // 串口模式：IP 输入框填串口名，端口输入框填波特率
                string portName = tb_ServiceInformationIP.Text.Trim();
                if (string.IsNullOrEmpty(portName))
                {
                    MessageBox.Show("请填写串口名（如 COM3）");
                    return;
                }
                if (!OtherTool.StringTryParseInt(tb_ServiceInformationPort.Text, out var baudRate, 1200, 921600))
                {
                    tb_ServiceInformationPort.Focus();
                    tb_ServiceInformationPort.SelectAll();
                    MessageBox.Show("Please Check ServiceInformation BaudRate");
                    return;
                }
                builder = new PlcServerBuilder().UseSerial(portName, baudRate, Parity.None, 8, StopBits.One);
            }
            else
            {
                if (!OtherTool.CheckStandardIPv4(tb_ServiceInformationIP.Text, out var iPAddress))
                {
                    tb_ServiceInformationIP.Focus();
                    tb_ServiceInformationIP.SelectAll();
                    MessageBox.Show("Please Check ServiceInformation IP");
                    return;
                }
                if (!OtherTool.StringTryParseInt(tb_ServiceInformationPort.Text, out var port, 1, 65535))
                {
                    tb_ServiceInformationPort.Focus();
                    tb_ServiceInformationPort.SelectAll();
                    MessageBox.Show("Please Check ServiceInformation Port");
                    return;
                }
                builder = new PlcServerBuilder().UseTcp(tb_ServiceInformationIP.Text.Trim(), port);
            }

            // 协议分派收敛在 Builder 里，界面不再逐个 case 地挑 UseXxx
            try
            {
                builder.UseProtocol(settings.Protocol).UseOptions(settings.Options);
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("不支持的协议：" + settings.Protocol.GetDisplayName(), "开启服务失败");
                return;
            }

            try
            {
                PlcServer = builder.Build();
                PlcServer.OpenServer();

                // 记下协议与监听参数，供服务端子窗体使用
                _serverProtocol = settings.Protocol;
                _serverOptions = settings.Options;
                _serverEndpoint = settings.Communication == CommunicationType.Serial
                    ? tb_ServiceInformationIP.Text.Trim() + " " + tb_ServiceInformationPort.Text.Trim()
                    : tb_ServiceInformationIP.Text.Trim() + ":" + tb_ServiceInformationPort.Text.Trim();
            }
            catch (Exception ex)
            {
                PlcServer = null;
                MessageBox.Show($"开启服务失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 关闭PLC服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_CloseServer_Click(object sender, EventArgs e)
        {
            if (plcServerWorkspaceForm != null && !plcServerWorkspaceForm.IsDisposed)
            {
                plcServerWorkspaceForm.Close();
            }
            plcServerWorkspaceForm = null;

            if (PlcServer != null)
            {
                PlcServer.CloseServer();
                PlcServer = null;
            }

            _serverProtocol = null;
            _serverEndpoint = null;
            _serverOptions = null;
        }
        #endregion
        /// <summary>
        /// 定时控件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            RefreshPLCUI();

            RefreshServerUI();

        }
        /// <summary>
        /// 刷新服务端相关UI
        /// </summary>
        private void RefreshServerUI()
        {
            if (PlcServer != null && PlcServer.ServerIsOpen)
            {
                tb_ServiceInformationIP.ReadOnly = true;
                tb_ServiceInformationPort.ReadOnly = true;
                btn_OpenServer.Enabled = false;
                btn_CloseServer.Enabled = true;
                btn_ServerWorkspace.Enabled = true;
                btn_Trigger.Enabled = true;
            }
            else
            {
                tb_ServiceInformationIP.ReadOnly = false;
                tb_ServiceInformationPort.ReadOnly = false;
                btn_OpenServer.Enabled = true;
                btn_CloseServer.Enabled = false;
                btn_ServerWorkspace.Enabled = false;
                btn_Trigger.Enabled = false;
                if ((plcServerWorkspaceForm != null && plcServerWorkspaceForm.IsDisposed == false))
                {
                    plcServerWorkspaceForm.Dispose();
                    plcServerWorkspaceForm = null;
                }
            }
        }
        /// <summary>
        /// 刷新PLC客户端相关UI
        /// </summary>
        private void RefreshPLCUI()
        {
            if (plcControl != null)
            {
                if (plcControl.IsConnected)
                {
                    lab_ConnectStatus.Text = "Connected";
                    lab_ConnectStatus.BackColor = Color.Green;

                    tb_PLCInformationIP.ReadOnly = true;
                    tb_PLCInformationPort.ReadOnly = true;

                    btn_Connect.Enabled = false;
                    btn_Disconnect.Enabled = true;
                    btn_PLCWorkspace.Enabled = true;
                }
                else
                {
                    lab_ConnectStatus.Text = "waiting";
                    lab_ConnectStatus.BackColor = Color.Orange;
                    btn_Connect.Enabled = false;
                    btn_Disconnect.Enabled = true;
                    btn_PLCWorkspace.Enabled = false;
                }
            }
            else
            {
                lab_ConnectStatus.Text = "Disconnected";
                lab_ConnectStatus.BackColor = Color.Red;

                tb_PLCInformationIP.ReadOnly = false;
                tb_PLCInformationPort.ReadOnly = false;

                btn_Connect.Enabled = true;
                btn_Disconnect.Enabled = false;
                btn_PLCWorkspace.Enabled = false;

                if ((plcWorkspaceForm != null && plcWorkspaceForm.IsDisposed == false))
                {
                    plcWorkspaceForm.Dispose();
                    plcWorkspaceForm = null;
                }
            }
        }
    }
}
