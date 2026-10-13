using PlcTest.Client.ClientProtocolManagement.Inovance;
using PlcTest.Client.PlcClient.Inovance;
using PlcTest.Client.ClientCommunication;
using PlcTest.Models;
using PlcTest.Client.PlcClient.Melsec;
using PlcTest.Client.PlcInterface;
using PlcTest.Server.PlcServer;
using PlcTest.Server.PlcServer.Melsec;
using PlcTest.Server.ServerCommunication;
using PlcTest.Server.ServerInterface;
using PlcTest.Tool;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace PlcTest.View
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// PLC实例
        /// </summary>
        IPlcClient plcControl = null;
        /// <summary>
        /// PLC窗体实例
        /// </summary>
        PlcClientForm plcClientForm=null;
        /// <summary>
        /// PLC服务实例
        /// </summary>
        IPlcServer PlcServer = null;
        /// <summary>
        /// PLC服务窗体
        /// </summary>
        PlcServerForm pLCServerForm = null;
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
            if (!OtherTool.CheckStandardIPv4(tb_PLCInformationIP.Text, out var iPAddress))
            {
                tb_PLCInformationIP.Focus();
                tb_PLCInformationIP.SelectAll();
                MessageBox.Show("Please Check PLCInformation IP");
                return;
            }
            if (!OtherTool.StringTryParseInt(tb_PLCInformationPort.Text, out var port))
            {
                tb_PLCInformationPort.Focus();
                tb_PLCInformationPort.SelectAll();
                MessageBox.Show("Please Check PLCInformation Port");
                return;
            }
            // 使用依赖注入：将通信实现和协议实现注入到客户端，避免硬编码

             var builder = new PlcClientBuilder()
            .UseTcp(tb_PLCInformationIP.Text, port)
            .UseMelsecMc1EAscii();
            plcControl = builder.BuildMelsecMcPlcClient();
            plcControl.Connect();
            //var comm = new TcpClientCommunication(tb_PLCInformationIP.Text, port);
            //var protocol = new InovanceModbusRtuClientProtocol();
            //plcControl = new InovanceTcpPlcClient(comm, protocol);
            //plcControl.Connect();
        }
        /// <summary>
        /// 断开PLC客户端连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Disconnect_Click(object sender, EventArgs e)
        {
            if (plcControl != null)
            {
                plcControl.Disconnect();
                plcControl = null;
            }
            else
            {
                plcControl = null;
            }
        }
        /// <summary>
        /// PLC寄存器操作视图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_PLCInformationMemoryView_Click(object sender, EventArgs e)
        {
            if ((plcControl != null && plcControl.IsConnected))
            {
                if (plcClientForm==null|| plcClientForm.IsDisposed)
                {
                    plcClientForm = new PlcClientForm(plcControl);
                    plcClientForm.Show();
                }
                else
                {
                    plcClientForm.Show();
                }
            }
            else
            {
                MessageBox.Show("Please Check PLC Connected");
            }
        }
        /// <summary>
        /// 开启PLC服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_OpenServer_Click(object sender, EventArgs e)
        {
            if (!OtherTool .CheckStandardIPv4(tb_ServiceInformationIP.Text ,out var iPAddress))
            {
                tb_ServiceInformationIP.Focus();
                tb_ServiceInformationIP.SelectAll();
                MessageBox.Show("Please Check ServiceInformation IP");
                return;
            }
            if (!OtherTool.StringTryParseInt(tb_ServiceInformationPort.Text, out var port))
            {
                tb_ServiceInformationPort.Focus ();
                tb_ServiceInformationPort.SelectAll ();
                MessageBox.Show("Please Check ServiceInformation Port");
                return;
            } 
            PlcServer = new MelsecMc3EPlcServer(new TcpServerCommunication(tb_ServiceInformationIP.Text, port));
            PlcServer.OpenServer();
        }
        /// <summary>
        /// 关闭PLC服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_CloseServer_Click(object sender, EventArgs e)
        {
            if (PlcServer != null)
            {
                PlcServer.CloseServer();
                PlcServer = null;
            }
        }
        /// <summary>
        /// PLC服务端寄存器视图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_ServiceInformationMemoryView_Click(object sender, EventArgs e)
        {
            if ((PlcServer != null && PlcServer.ServerIsOpen))
            {
                if (pLCServerForm == null || pLCServerForm.IsDisposed)
                {
                    pLCServerForm = new PlcServerForm(PlcServer);
                    pLCServerForm.Show();
                }
                else
                {
                    pLCServerForm.Show();
                }
            }
            else
            {
                MessageBox.Show("Please Check PlcServer Open");
            }
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
                btn_ServiceInformationMemoryView.Enabled = true;
                btn_Trigger.Enabled = true;
            }
            else
            {
                tb_ServiceInformationIP.ReadOnly = false;
                tb_ServiceInformationPort.ReadOnly = false;
                btn_OpenServer.Enabled = true;
                btn_CloseServer.Enabled = false;
                btn_ServiceInformationMemoryView.Enabled = false;
                btn_Trigger.Enabled = false;
                if ((pLCServerForm != null && pLCServerForm.IsDisposed == false))
                {
                    pLCServerForm.Dispose();
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
                    btn_PLCInformationMemoryView.Enabled = true;
                }
                else
                {
                    lab_ConnectStatus.Text = "waiting";
                    lab_ConnectStatus.BackColor = Color.Orange;
                    btn_Connect.Enabled = false;
                    btn_Disconnect.Enabled = true;
                    btn_PLCInformationMemoryView.Enabled = false;
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
                btn_PLCInformationMemoryView.Enabled = false;

                if ((plcClientForm != null && plcClientForm.IsDisposed == false))
                {
                    plcClientForm.Dispose();
                }
            }
        }
    }
}
