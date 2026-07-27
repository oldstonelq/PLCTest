using PLCTest.ClientCommunication;
using PLCTest.PLCClient;
using PLCTest.PLCClient.Melsec;
using PLCTest.PLCInterface;
using PLCTest.PLCSever;
using PLCTest.PLCSever.Melsec;
using PLCTest.SeverCommunication;
using PLCTest.SeverInterface;
using PLCTest.Tool;
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

namespace PLCTest.View
{
    public partial class aMainForm : Form
    {
        /// <summary>
        /// PLC实例
        /// </summary>
        IPLCClient pLCControl = null;
        /// <summary>
        /// PLC窗体实例
        /// </summary>
        PLCClientForm pLCClientForm=null;
        /// <summary>
        /// PLC服务实例
        /// </summary>
        IPLCServer PLCSever = null;
        /// <summary>
        /// PLC服务窗体
        /// </summary>
        PLCSeverForm pLCSeverForm = null;
        /// <summary>
        /// 构造函数
        /// </summary>
        public aMainForm()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aMainForm_Load(object sender, EventArgs e)
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
            pLCControl = new MelsecMc3EPLCClient(new TcpClientCommunication(tb_PLCInformationIP.Text, port));
            pLCControl.Connect();
        }
        /// <summary>
        /// 断开PLC客户端连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Disconnect_Click(object sender, EventArgs e)
        {
            if (pLCControl != null)
            {
                pLCControl.Disconnect();
                pLCControl = null;
            }
            else
            {
                pLCControl = null;
            }
        }
        /// <summary>
        /// PLC寄存器操作视图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_PLCInformationMemoryView_Click(object sender, EventArgs e)
        {
            if ((pLCControl != null && pLCControl.IsConnected))
            {
                if (pLCClientForm==null|| pLCClientForm.IsDisposed)
                {
                    pLCClientForm = new PLCClientForm(pLCControl);
                    pLCClientForm.Show();
                }
                else
                {
                    pLCClientForm.Show();
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
            PLCSever = new MelsecMc3EPLCSever(new TcpSeverCommunication(tb_ServiceInformationIP.Text, port));
            PLCSever.OpenServer();
        }
        /// <summary>
        /// 关闭PLC服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_CloseServer_Click(object sender, EventArgs e)
        {
            if (PLCSever != null)
            {
                PLCSever.CloseServer();
                PLCSever = null;
            }
        }
        /// <summary>
        /// PLC服务端寄存器视图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_ServiceInformationMemoryView_Click(object sender, EventArgs e)
        {
            if ((PLCSever != null && PLCSever.SeverIsOpen))
            {
                if (pLCSeverForm == null || pLCSeverForm.IsDisposed)
                {
                    pLCSeverForm = new PLCSeverForm(PLCSever);
                    pLCSeverForm.Show();
                }
                else
                {
                    pLCSeverForm.Show();
                }
            }
            else
            {
                MessageBox.Show("Please Check PLCSever Open");
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

            RefreshSeverUI();

        }
        /// <summary>
        /// 刷新服务端相关UI
        /// </summary>
        private void RefreshSeverUI()
        {
            if (PLCSever != null && PLCSever.SeverIsOpen)
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
                if ((pLCSeverForm != null && pLCSeverForm.IsDisposed == false))
                {
                    pLCSeverForm.Dispose();
                }
            }
        }
        /// <summary>
        /// 刷新PLC客户端相关UI
        /// </summary>
        private void RefreshPLCUI()
        {
            if (pLCControl != null && pLCControl.IsConnected)
            {
                lab_ConnectStatus.Text = "Connected";
                lab_ConnectStatus.BackColor = Color.Green;

                tb_PLCInformationIP.ReadOnly = true;
                tb_PLCInformationPort.ReadOnly = true;

                btn_Connect.Enabled = false;
                btn_Disconnect.Enabled = true;
                btn_PLCInformationMemoryView.Enabled = true;
            }
            else if (pLCControl != null && pLCControl.IsConnected == false)
            {
                lab_ConnectStatus.Text = "waiting";
                lab_ConnectStatus.BackColor = Color.Orange;
                btn_Connect.Enabled = false;
                btn_Disconnect.Enabled = true;
                btn_PLCInformationMemoryView.Enabled = false;
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

                if ((pLCClientForm != null && pLCClientForm.IsDisposed == false))
                {
                    pLCClientForm.Dispose();
                }
            }
        }
    }
}
