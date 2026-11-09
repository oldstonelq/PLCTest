using PlcTest.View.Controls;

namespace PlcTest.View.Forms
{
    partial class PlcServerWorkspaceForm
    {
        /// <summary>
        /// 设计器容器：状态条刷新定时器由它统一释放。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理正在使用的资源。
        /// </summary>
        /// <param name="disposing">true 表示释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseRuntime();
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 设计器生成：整窗控件树（状态条 / 地址栏 / 左列表 + 右页签 / 底部报文）。
        /// 数据绑定与事件订阅都在主文件的 InitializeRuntime 里。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._root = new System.Windows.Forms.TableLayoutPanel();
            this._labStatus = new System.Windows.Forms.Label();
            this._addressPanel = new PlcTest.View.Controls.AddressInputPanel();
            this._middle = new System.Windows.Forms.SplitContainer();
            this._leftHost = new System.Windows.Forms.Panel();
            this._lstAreas = new System.Windows.Forms.ListBox();
            this._leftTitle = new System.Windows.Forms.Label();
            this._tabs = new System.Windows.Forms.TabControl();
            this._pageRegisters = new System.Windows.Forms.TabPage();
            this._registerLayout = new System.Windows.Forms.TableLayoutPanel();
            this._registerBar = new System.Windows.Forms.FlowLayoutPanel();
            this._btnInit = new System.Windows.Forms.Button();
            this._btnResetRange = new System.Windows.Forms.Button();
            this._btnResetAll = new System.Windows.Forms.Button();
            this._btnRemoveArea = new System.Windows.Forms.Button();
            this._btnRandom = new System.Windows.Forms.Button();
            this._registerHint = new System.Windows.Forms.Label();
            this._monitor = new PlcTest.View.Controls.MonitorGrid();
            this._pageSingle = new System.Windows.Forms.TabPage();
            this._singleLayout = new System.Windows.Forms.TableLayoutPanel();
            this._readBar = new System.Windows.Forms.FlowLayoutPanel();
            this._btnRead = new System.Windows.Forms.Button();
            this._btnClearResult = new System.Windows.Forms.Button();
            this._tbReadResult = new System.Windows.Forms.TextBox();
            this._writeBar = new System.Windows.Forms.FlowLayoutPanel();
            this._writeLabel = new System.Windows.Forms.Label();
            this._tbWriteValue = new System.Windows.Forms.TextBox();
            this._btnWrite = new System.Windows.Forms.Button();
            this._frameLog = new FrameLogView();
            this._statusTimer = new System.Windows.Forms.Timer(this.components);
            this._root.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._middle)).BeginInit();
            this._middle.Panel1.SuspendLayout();
            this._middle.Panel2.SuspendLayout();
            this._middle.SuspendLayout();
            this._leftHost.SuspendLayout();
            this._tabs.SuspendLayout();
            this._pageRegisters.SuspendLayout();
            this._registerLayout.SuspendLayout();
            this._registerBar.SuspendLayout();
            this._pageSingle.SuspendLayout();
            this._singleLayout.SuspendLayout();
            this._readBar.SuspendLayout();
            this._writeBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // _root
            // 
            this._root.ColumnCount = 1;
            this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._root.Controls.Add(this._labStatus, 0, 0);
            this._root.Controls.Add(this._addressPanel, 0, 1);
            this._root.Controls.Add(this._middle, 0, 2);
            this._root.Controls.Add(this._frameLog, 0, 3);
            this._root.Dock = System.Windows.Forms.DockStyle.Fill;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.RowCount = 4;
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 170F));
            this._root.Size = new System.Drawing.Size(1020, 660);
            this._root.TabIndex = 0;
            // 
            // _labStatus
            // 
            this._labStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this._labStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(44)))), ((int)(((byte)(42)))));
            this._labStatus.Location = new System.Drawing.Point(3, 0);
            this._labStatus.Name = "_labStatus";
            this._labStatus.Padding = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this._labStatus.Size = new System.Drawing.Size(1014, 26);
            this._labStatus.TabIndex = 0;
            this._labStatus.Text = "PLC 服务端工作台";
            this._labStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _addressPanel
            // 
            this._addressPanel.BackColor = System.Drawing.Color.Transparent;
            this._addressPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._addressPanel.Location = new System.Drawing.Point(3, 29);
            this._addressPanel.Name = "_addressPanel";
            this._addressPanel.Size = new System.Drawing.Size(1014, 34);
            this._addressPanel.TabIndex = 1;
            // 
            // _middle
            // 
            this._middle.Dock = System.Windows.Forms.DockStyle.Fill;
            this._middle.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this._middle.Location = new System.Drawing.Point(3, 69);
            this._middle.Name = "_middle";
            // 
            // _middle.Panel1
            // 
            this._middle.Panel1.Controls.Add(this._leftHost);
            // 
            // _middle.Panel2
            // 
            this._middle.Panel2.Controls.Add(this._tabs);
            this._middle.Size = new System.Drawing.Size(1014, 418);
            this._middle.SplitterDistance = 190;
            this._middle.SplitterWidth = 4;
            this._middle.TabIndex = 2;
            // 
            // _leftHost
            // 
            this._leftHost.Controls.Add(this._lstAreas);
            this._leftHost.Controls.Add(this._leftTitle);
            this._leftHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._leftHost.Location = new System.Drawing.Point(0, 0);
            this._leftHost.Name = "_leftHost";
            this._leftHost.Size = new System.Drawing.Size(190, 418);
            this._leftHost.TabIndex = 0;
            // 
            // _lstAreas
            // 
            this._lstAreas.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._lstAreas.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lstAreas.Font = new System.Drawing.Font("Consolas", 9F);
            this._lstAreas.IntegralHeight = false;
            this._lstAreas.Location = new System.Drawing.Point(0, 24);
            this._lstAreas.Name = "_lstAreas";
            this._lstAreas.Size = new System.Drawing.Size(190, 394);
            this._lstAreas.TabIndex = 1;
            // 
            // _leftTitle
            // 
            this._leftTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this._leftTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._leftTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._leftTitle.Location = new System.Drawing.Point(0, 0);
            this._leftTitle.Name = "_leftTitle";
            this._leftTitle.Size = new System.Drawing.Size(190, 24);
            this._leftTitle.TabIndex = 0;
            this._leftTitle.Text = "  软元件";
            this._leftTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _tabs
            // 
            this._tabs.Controls.Add(this._pageRegisters);
            this._tabs.Controls.Add(this._pageSingle);
            this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabs.Location = new System.Drawing.Point(0, 0);
            this._tabs.Name = "_tabs";
            this._tabs.SelectedIndex = 0;
            this._tabs.Size = new System.Drawing.Size(820, 418);
            this._tabs.TabIndex = 0;
            // 
            // _pageRegisters
            // 
            this._pageRegisters.Controls.Add(this._registerLayout);
            this._pageRegisters.Location = new System.Drawing.Point(4, 22);
            this._pageRegisters.Name = "_pageRegisters";
            this._pageRegisters.Padding = new System.Windows.Forms.Padding(3);
            this._pageRegisters.Size = new System.Drawing.Size(812, 392);
            this._pageRegisters.TabIndex = 0;
            this._pageRegisters.Text = "模拟寄存器";
            this._pageRegisters.UseVisualStyleBackColor = true;
            // 
            // _registerLayout
            // 
            this._registerLayout.ColumnCount = 1;
            this._registerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._registerLayout.Controls.Add(this._registerBar, 0, 0);
            this._registerLayout.Controls.Add(this._monitor, 0, 1);
            this._registerLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._registerLayout.Location = new System.Drawing.Point(3, 3);
            this._registerLayout.Name = "_registerLayout";
            this._registerLayout.Padding = new System.Windows.Forms.Padding(6);
            this._registerLayout.RowCount = 2;
            this._registerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this._registerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._registerLayout.Size = new System.Drawing.Size(806, 386);
            this._registerLayout.TabIndex = 0;
            // 
            // _registerBar
            // 
            this._registerBar.Controls.Add(this._btnInit);
            this._registerBar.Controls.Add(this._btnResetRange);
            this._registerBar.Controls.Add(this._btnResetAll);
            this._registerBar.Controls.Add(this._btnRemoveArea);
            this._registerBar.Controls.Add(this._btnRandom);
            this._registerBar.Controls.Add(this._registerHint);
            this._registerBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._registerBar.Location = new System.Drawing.Point(9, 9);
            this._registerBar.Name = "_registerBar";
            this._registerBar.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._registerBar.Size = new System.Drawing.Size(788, 26);
            this._registerBar.TabIndex = 0;
            this._registerBar.WrapContents = false;
            // 
            // _btnInit
            // 
            this._btnInit.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnInit.Location = new System.Drawing.Point(0, 5);
            this._btnInit.Margin = new System.Windows.Forms.Padding(0, 1, 6, 0);
            this._btnInit.Name = "_btnInit";
            this._btnInit.Size = new System.Drawing.Size(92, 24);
            this._btnInit.TabIndex = 0;
            this._btnInit.Text = "初始化区间";
            // 
            // _btnResetRange
            // 
            this._btnResetRange.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnResetRange.Location = new System.Drawing.Point(98, 5);
            this._btnResetRange.Margin = new System.Windows.Forms.Padding(0, 1, 6, 0);
            this._btnResetRange.Name = "_btnResetRange";
            this._btnResetRange.Size = new System.Drawing.Size(80, 24);
            this._btnResetRange.TabIndex = 1;
            this._btnResetRange.Text = "清零区间";
            // 
            // _btnResetAll
            // 
            this._btnResetAll.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnResetAll.Location = new System.Drawing.Point(184, 5);
            this._btnResetAll.Margin = new System.Windows.Forms.Padding(0, 1, 6, 0);
            this._btnResetAll.Name = "_btnResetAll";
            this._btnResetAll.Size = new System.Drawing.Size(80, 24);
            this._btnResetAll.TabIndex = 2;
            this._btnResetAll.Text = "全部清零";
            // 
            // _btnRemoveArea
            // 
            this._btnRemoveArea.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnRemoveArea.Location = new System.Drawing.Point(270, 5);
            this._btnRemoveArea.Margin = new System.Windows.Forms.Padding(0, 1, 6, 0);
            this._btnRemoveArea.Name = "_btnRemoveArea";
            this._btnRemoveArea.Size = new System.Drawing.Size(80, 24);
            this._btnRemoveArea.TabIndex = 3;
            this._btnRemoveArea.Text = "移除区域";
            // 
            // _btnRandom
            // 
            this._btnRandom.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnRandom.Location = new System.Drawing.Point(356, 5);
            this._btnRandom.Margin = new System.Windows.Forms.Padding(0, 1, 6, 0);
            this._btnRandom.Name = "_btnRandom";
            this._btnRandom.Size = new System.Drawing.Size(80, 24);
            this._btnRandom.TabIndex = 4;
            this._btnRandom.Text = "随机填充";
            // 
            // _registerHint
            // 
            this._registerHint.AutoSize = true;
            this._registerHint.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._registerHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._registerHint.Location = new System.Drawing.Point(450, 9);
            this._registerHint.Margin = new System.Windows.Forms.Padding(8, 5, 0, 0);
            this._registerHint.Name = "_registerHint";
            this._registerHint.Size = new System.Drawing.Size(233, 12);
            this._registerHint.TabIndex = 5;
            this._registerHint.Text = "清零只置 0（地址保留）；移除会删地址";
            // 
            // _monitor
            // 
            this._monitor.Dock = System.Windows.Forms.DockStyle.Fill;
            this._monitor.Location = new System.Drawing.Point(9, 41);
            this._monitor.Name = "_monitor";
            this._monitor.Size = new System.Drawing.Size(788, 336);
            this._monitor.TabIndex = 1;
            // 
            // _pageSingle
            // 
            this._pageSingle.Controls.Add(this._singleLayout);
            this._pageSingle.Location = new System.Drawing.Point(4, 22);
            this._pageSingle.Name = "_pageSingle";
            this._pageSingle.Padding = new System.Windows.Forms.Padding(3);
            this._pageSingle.Size = new System.Drawing.Size(812, 392);
            this._pageSingle.TabIndex = 1;
            this._pageSingle.Text = "单次读写";
            this._pageSingle.UseVisualStyleBackColor = true;
            // 
            // _singleLayout
            // 
            this._singleLayout.ColumnCount = 1;
            this._singleLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._singleLayout.Controls.Add(this._readBar, 0, 0);
            this._singleLayout.Controls.Add(this._tbReadResult, 0, 1);
            this._singleLayout.Controls.Add(this._writeBar, 0, 2);
            this._singleLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._singleLayout.Location = new System.Drawing.Point(3, 3);
            this._singleLayout.Name = "_singleLayout";
            this._singleLayout.Padding = new System.Windows.Forms.Padding(6);
            this._singleLayout.RowCount = 3;
            this._singleLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this._singleLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._singleLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this._singleLayout.Size = new System.Drawing.Size(806, 386);
            this._singleLayout.TabIndex = 0;
            // 
            // _readBar
            // 
            this._readBar.Controls.Add(this._btnRead);
            this._readBar.Controls.Add(this._btnClearResult);
            this._readBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._readBar.Location = new System.Drawing.Point(9, 9);
            this._readBar.Name = "_readBar";
            this._readBar.Size = new System.Drawing.Size(788, 26);
            this._readBar.TabIndex = 0;
            this._readBar.WrapContents = false;
            // 
            // _btnRead
            // 
            this._btnRead.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnRead.Location = new System.Drawing.Point(0, 1);
            this._btnRead.Margin = new System.Windows.Forms.Padding(0, 1, 6, 0);
            this._btnRead.Name = "_btnRead";
            this._btnRead.Size = new System.Drawing.Size(74, 24);
            this._btnRead.TabIndex = 0;
            this._btnRead.Text = "读取";
            // 
            // _btnClearResult
            // 
            this._btnClearResult.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnClearResult.Location = new System.Drawing.Point(80, 1);
            this._btnClearResult.Margin = new System.Windows.Forms.Padding(0, 1, 6, 0);
            this._btnClearResult.Name = "_btnClearResult";
            this._btnClearResult.Size = new System.Drawing.Size(80, 24);
            this._btnClearResult.TabIndex = 1;
            this._btnClearResult.Text = "清空结果";
            // 
            // _tbReadResult
            // 
            this._tbReadResult.BackColor = System.Drawing.Color.White;
            this._tbReadResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tbReadResult.Font = new System.Drawing.Font("Consolas", 9F);
            this._tbReadResult.Location = new System.Drawing.Point(9, 41);
            this._tbReadResult.Multiline = true;
            this._tbReadResult.Name = "_tbReadResult";
            this._tbReadResult.ReadOnly = true;
            this._tbReadResult.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._tbReadResult.Size = new System.Drawing.Size(788, 308);
            this._tbReadResult.TabIndex = 1;
            this._tbReadResult.WordWrap = false;
            // 
            // _writeBar
            // 
            this._writeBar.Controls.Add(this._writeLabel);
            this._writeBar.Controls.Add(this._tbWriteValue);
            this._writeBar.Controls.Add(this._btnWrite);
            this._writeBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._writeBar.Location = new System.Drawing.Point(9, 355);
            this._writeBar.Name = "_writeBar";
            this._writeBar.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this._writeBar.Size = new System.Drawing.Size(788, 26);
            this._writeBar.TabIndex = 2;
            this._writeBar.WrapContents = false;
            // 
            // _writeLabel
            // 
            this._writeLabel.AutoSize = true;
            this._writeLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._writeLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._writeLabel.Location = new System.Drawing.Point(0, 10);
            this._writeLabel.Margin = new System.Windows.Forms.Padding(0, 7, 4, 0);
            this._writeLabel.Name = "_writeLabel";
            this._writeLabel.Size = new System.Drawing.Size(41, 12);
            this._writeLabel.TabIndex = 0;
            this._writeLabel.Text = "写入值";
            // 
            // _tbWriteValue
            // 
            this._tbWriteValue.Font = new System.Drawing.Font("Consolas", 9F);
            this._tbWriteValue.Location = new System.Drawing.Point(45, 6);
            this._tbWriteValue.Name = "_tbWriteValue";
            this._tbWriteValue.Size = new System.Drawing.Size(240, 22);
            this._tbWriteValue.TabIndex = 1;
            // 
            // _btnWrite
            // 
            this._btnWrite.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnWrite.Location = new System.Drawing.Point(291, 7);
            this._btnWrite.Margin = new System.Windows.Forms.Padding(6, 1, 6, 0);
            this._btnWrite.Name = "_btnWrite";
            this._btnWrite.Size = new System.Drawing.Size(74, 24);
            this._btnWrite.TabIndex = 2;
            this._btnWrite.Text = "写入";
            // 
            // _frameLog
            // 
            this._frameLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this._frameLog.Location = new System.Drawing.Point(3, 493);
            this._frameLog.Name = "_frameLog";
            this._frameLog.Size = new System.Drawing.Size(1014, 164);
            this._frameLog.TabIndex = 3;
            // 
            // _statusTimer
            // 
            this._statusTimer.Interval = 500;
            // 
            // PlcServerWorkspaceForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1020, 660);
            this.Controls.Add(this._root);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PlcServerWorkspaceForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "PLC 服务端工作台";
            this._root.ResumeLayout(false);
            this._middle.Panel1.ResumeLayout(false);
            this._middle.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._middle)).EndInit();
            this._middle.ResumeLayout(false);
            this._leftHost.ResumeLayout(false);
            this._tabs.ResumeLayout(false);
            this._pageRegisters.ResumeLayout(false);
            this._registerLayout.ResumeLayout(false);
            this._registerBar.ResumeLayout(false);
            this._registerBar.PerformLayout();
            this._pageSingle.ResumeLayout(false);
            this._singleLayout.ResumeLayout(false);
            this._singleLayout.PerformLayout();
            this._readBar.ResumeLayout(false);
            this._writeBar.ResumeLayout(false);
            this._writeBar.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel _root;
        private System.Windows.Forms.Label _labStatus;
        private AddressInputPanel _addressPanel;
        private System.Windows.Forms.SplitContainer _middle;
        private System.Windows.Forms.Panel _leftHost;
        private System.Windows.Forms.ListBox _lstAreas;
        private System.Windows.Forms.Label _leftTitle;
        private System.Windows.Forms.TabControl _tabs;
        private System.Windows.Forms.TabPage _pageRegisters;
        private System.Windows.Forms.TableLayoutPanel _registerLayout;
        private System.Windows.Forms.FlowLayoutPanel _registerBar;
        private System.Windows.Forms.Button _btnInit;
        private System.Windows.Forms.Button _btnResetRange;
        private System.Windows.Forms.Button _btnResetAll;
        private System.Windows.Forms.Button _btnRemoveArea;
        private System.Windows.Forms.Button _btnRandom;
        private System.Windows.Forms.Label _registerHint;
        private MonitorGrid _monitor;
        private System.Windows.Forms.TabPage _pageSingle;
        private System.Windows.Forms.TableLayoutPanel _singleLayout;
        private System.Windows.Forms.FlowLayoutPanel _readBar;
        private System.Windows.Forms.Button _btnRead;
        private System.Windows.Forms.Button _btnClearResult;
        private System.Windows.Forms.TextBox _tbReadResult;
        private System.Windows.Forms.FlowLayoutPanel _writeBar;
        private System.Windows.Forms.Label _writeLabel;
        private System.Windows.Forms.TextBox _tbWriteValue;
        private System.Windows.Forms.Button _btnWrite;
        private FrameLogView _frameLog;
        private System.Windows.Forms.Timer _statusTimer;
    }
}
