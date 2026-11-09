namespace PlcTest.View.Controls
{
    partial class MonitorGrid
    {
        /// <summary>
        /// 设计器容器：轮询定时器由它统一释放。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理正在使用的资源。
        /// </summary>
        /// <param name="disposing">true 表示释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 设计器生成：工具栏、监视表格与列定义。轮询逻辑都在主文件里。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._bar = new System.Windows.Forms.Panel();
            this._btnAdd = new System.Windows.Forms.Button();
            this._btnRemove = new System.Windows.Forms.Button();
            this._btnRefresh = new System.Windows.Forms.Button();
            this._btnPause = new System.Windows.Forms.Button();
            this._tbPeriod = new System.Windows.Forms.TextBox();
            this._labPeriod = new System.Windows.Forms.Label();
            this._grid = new System.Windows.Forms.DataGridView();
            this.colAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colKind = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._timer = new System.Windows.Forms.Timer(this.components);
            this._bar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
            this.SuspendLayout();
            // 
            // _bar
            // 
            this._bar.BackColor = System.Drawing.SystemColors.Control;
            this._bar.Controls.Add(this._btnAdd);
            this._bar.Controls.Add(this._btnRemove);
            this._bar.Controls.Add(this._btnRefresh);
            this._bar.Controls.Add(this._btnPause);
            this._bar.Controls.Add(this._tbPeriod);
            this._bar.Controls.Add(this._labPeriod);
            this._bar.Dock = System.Windows.Forms.DockStyle.Top;
            this._bar.Location = new System.Drawing.Point(0, 0);
            this._bar.Name = "_bar";
            this._bar.Size = new System.Drawing.Size(600, 30);
            this._bar.TabIndex = 0;
            // 
            // _btnAdd
            // 
            this._btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnAdd.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._btnAdd.Location = new System.Drawing.Point(2, 4);
            this._btnAdd.Name = "_btnAdd";
            this._btnAdd.Size = new System.Drawing.Size(54, 22);
            this._btnAdd.TabIndex = 0;
            this._btnAdd.Text = "添加";
            // 
            // _btnRemove
            // 
            this._btnRemove.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnRemove.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._btnRemove.Location = new System.Drawing.Point(58, 4);
            this._btnRemove.Name = "_btnRemove";
            this._btnRemove.Size = new System.Drawing.Size(54, 22);
            this._btnRemove.TabIndex = 1;
            this._btnRemove.Text = "删除";
            // 
            // _btnRefresh
            // 
            this._btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnRefresh.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._btnRefresh.Location = new System.Drawing.Point(114, 4);
            this._btnRefresh.Name = "_btnRefresh";
            this._btnRefresh.Size = new System.Drawing.Size(76, 22);
            this._btnRefresh.TabIndex = 2;
            this._btnRefresh.Text = "立即刷新";
            // 
            // _btnPause
            // 
            this._btnPause.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnPause.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._btnPause.Location = new System.Drawing.Point(192, 4);
            this._btnPause.Name = "_btnPause";
            this._btnPause.Size = new System.Drawing.Size(54, 22);
            this._btnPause.TabIndex = 3;
            this._btnPause.Text = "开始";
            // 
            // _tbPeriod
            // 
            this._tbPeriod.Location = new System.Drawing.Point(268, 4);
            this._tbPeriod.Name = "_tbPeriod";
            this._tbPeriod.Size = new System.Drawing.Size(52, 21);
            this._tbPeriod.TabIndex = 4;
            this._tbPeriod.Text = "500";
            // 
            // _labPeriod
            // 
            this._labPeriod.AutoSize = true;
            this._labPeriod.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labPeriod.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._labPeriod.Location = new System.Drawing.Point(324, 8);
            this._labPeriod.Name = "_labPeriod";
            this._labPeriod.Size = new System.Drawing.Size(50, 12);
            this._labPeriod.TabIndex = 5;
            this._labPeriod.Text = "周期(ms)";
            // 
            // _grid
            // 
            this._grid.AllowUserToAddRows = false;
            this._grid.AllowUserToDeleteRows = false;
            this._grid.BackgroundColor = System.Drawing.Color.White;
            this._grid.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colAddress,
            this.colKind,
            this.colLength,
            this.colValue,
            this.colStatus});
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this._grid.Location = new System.Drawing.Point(0, 30);
            this._grid.MultiSelect = true;
            this._grid.Name = "_grid";
            this._grid.RowHeadersWidth = 26;
            this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._grid.Size = new System.Drawing.Size(600, 270);
            this._grid.TabIndex = 1;
            // 
            // colAddress
            // 
            this.colAddress.HeaderText = "地址";
            this.colAddress.Name = "colAddress";
            this.colAddress.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colAddress.Width = 120;
            // 
            // colKind
            // 
            this.colKind.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.colKind.HeaderText = "方式";
            this.colKind.Items.AddRange(new object[] {
            "位",
            "字"});
            this.colKind.Name = "colKind";
            this.colKind.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colKind.Width = 56;
            // 
            // colLength
            // 
            this.colLength.HeaderText = "长度";
            this.colLength.Name = "colLength";
            this.colLength.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colLength.Width = 50;
            // 
            // colValue
            // 
            this.colValue.HeaderText = "值";
            this.colValue.Name = "colValue";
            this.colValue.ReadOnly = true;
            this.colValue.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.colValue.Width = 260;
            // 
            // colStatus
            // 
            this.colStatus.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colStatus.HeaderText = "状态";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            this.colStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // _timer
            // 
            this._timer.Interval = 500;
            // 
            // MonitorGrid
            // 
            this.Controls.Add(this._grid);
            this.Controls.Add(this._bar);
            this.Name = "MonitorGrid";
            this.Size = new System.Drawing.Size(600, 300);
            this._bar.ResumeLayout(false);
            this._bar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel _bar;
        private System.Windows.Forms.Button _btnAdd;
        private System.Windows.Forms.Button _btnRemove;
        private System.Windows.Forms.Button _btnRefresh;
        private System.Windows.Forms.Button _btnPause;
        private System.Windows.Forms.TextBox _tbPeriod;
        private System.Windows.Forms.Label _labPeriod;
        private System.Windows.Forms.DataGridView _grid;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAddress;
        private System.Windows.Forms.DataGridViewComboBoxColumn colKind;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLength;
        private System.Windows.Forms.DataGridViewTextBoxColumn colValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.Timer _timer;
    }
}
