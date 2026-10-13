using PlcTest.View.CustomControl;

namespace PlcTest.View
{
    partial class PlcClientForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.gb_cleardata = new PlcTest.View.CustomControl.MGroupBox(this.components);
            this.btn_ClearAll = new System.Windows.Forms.Button();
            this.btn_ClearLength = new System.Windows.Forms.Button();
            this.tb_ClearLength = new System.Windows.Forms.TextBox();
            this.lab_ClearLength = new System.Windows.Forms.Label();
            this.gb_info = new PlcTest.View.CustomControl.MGroupBox(this.components);
            this.btn_EndRead = new System.Windows.Forms.Button();
            this.btn_StartRead = new System.Windows.Forms.Button();
            this.tb_ReadLength = new System.Windows.Forms.TextBox();
            this.tb_StartAddress = new System.Windows.Forms.TextBox();
            this.cmb_Area = new System.Windows.Forms.ComboBox();
            this.lab_ReadLength = new System.Windows.Forms.Label();
            this.lab_StartAddress = new System.Windows.Forms.Label();
            this.lab_Area = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tab_DataInfo = new System.Windows.Forms.TabControl();
            this.tp_DataView = new System.Windows.Forms.TabPage();
            this.DGV = new System.Windows.Forms.DataGridView();
            this.Column_Address = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit15 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit14 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit13 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit12 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit11 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit10 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_9 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit8 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_bit1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_0 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnHeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_Low = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_All = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column_ascII = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tp_LogView = new System.Windows.Forms.TabPage();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tb_Info = new System.Windows.Forms.TextBox();
            this.gb_WriteBit = new PlcTest.View.CustomControl.MGroupBox(this.components);
            this.cmb_WriteBitArea = new System.Windows.Forms.ComboBox();
            this.tb_WriteBitValue = new System.Windows.Forms.TextBox();
            this.tb_WriteBitAddress = new System.Windows.Forms.TextBox();
            this.btn_WriteBitData = new System.Windows.Forms.Button();
            this.lab_WriteBitValue = new System.Windows.Forms.Label();
            this.lab_WriteBitAddress = new System.Windows.Forms.Label();
            this.lab_WriteBitArea = new System.Windows.Forms.Label();
            this.gb_WriteWord = new PlcTest.View.CustomControl.MGroupBox(this.components);
            this.cmb_WriteWordArea = new System.Windows.Forms.ComboBox();
            this.tb_WriteWordValue = new System.Windows.Forms.TextBox();
            this.tb_WriteWordAddress = new System.Windows.Forms.TextBox();
            this.btn_WriteWordData = new System.Windows.Forms.Button();
            this.lab_WriteWordValue = new System.Windows.Forms.Label();
            this.lab_WriteWordAddress = new System.Windows.Forms.Label();
            this.lab_WriteWordArea = new System.Windows.Forms.Label();
            this.groupBox3 = new PlcTest.View.CustomControl.MGroupBox(this.components);
            this.btn_StartAddressAdd50 = new System.Windows.Forms.Button();
            this.btn_StartAddressDecrease50 = new System.Windows.Forms.Button();
            this.btn_StartAddressAdd20 = new System.Windows.Forms.Button();
            this.btn_StartAddressDecrease20 = new System.Windows.Forms.Button();
            this.btn_StartAddressAdd5 = new System.Windows.Forms.Button();
            this.btn_StartAddressDecrease5 = new System.Windows.Forms.Button();
            this.btn_StartAddressAdd1 = new System.Windows.Forms.Button();
            this.btn_StartAddressDecrease1 = new System.Windows.Forms.Button();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.gb_cleardata.SuspendLayout();
            this.gb_info.SuspendLayout();
            this.panel1.SuspendLayout();
            this.tab_DataInfo.SuspendLayout();
            this.tp_DataView.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.DGV)).BeginInit();
            this.tp_LogView.SuspendLayout();
            this.panel2.SuspendLayout();
            this.gb_WriteBit.SuspendLayout();
            this.gb_WriteWord.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 56.33484F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 43.66516F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 202F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 145F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 134F));
            this.tableLayoutPanel1.Controls.Add(this.gb_cleardata, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.gb_info, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.gb_WriteBit, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.gb_WriteWord, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox3, 3, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 27.11111F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 72.88889F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(934, 501);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // gb_cleardata
            // 
            this.gb_cleardata.BorderColor = System.Drawing.Color.Black;
            this.gb_cleardata.Controls.Add(this.btn_ClearAll);
            this.gb_cleardata.Controls.Add(this.btn_ClearLength);
            this.gb_cleardata.Controls.Add(this.tb_ClearLength);
            this.gb_cleardata.Controls.Add(this.lab_ClearLength);
            this.gb_cleardata.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gb_cleardata.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.gb_cleardata.Location = new System.Drawing.Point(802, 3);
            this.gb_cleardata.Name = "gb_cleardata";
            this.gb_cleardata.Size = new System.Drawing.Size(129, 129);
            this.gb_cleardata.TabIndex = 7;
            this.gb_cleardata.TabStop = false;
            this.gb_cleardata.Text = "Clear Data";
            // 
            // btn_ClearAll
            // 
            this.btn_ClearAll.Enabled = false;
            this.btn_ClearAll.Location = new System.Drawing.Point(11, 73);
            this.btn_ClearAll.Name = "btn_ClearAll";
            this.btn_ClearAll.Size = new System.Drawing.Size(112, 50);
            this.btn_ClearAll.TabIndex = 9;
            this.btn_ClearAll.Text = "ClearAll";
            this.btn_ClearAll.UseVisualStyleBackColor = true;
            this.btn_ClearAll.Click += new System.EventHandler(this.btn_ClearAll_Click);
            // 
            // btn_ClearLength
            // 
            this.btn_ClearLength.Enabled = false;
            this.btn_ClearLength.Location = new System.Drawing.Point(75, 40);
            this.btn_ClearLength.Name = "btn_ClearLength";
            this.btn_ClearLength.Size = new System.Drawing.Size(53, 23);
            this.btn_ClearLength.TabIndex = 8;
            this.btn_ClearLength.Text = "Clear";
            this.btn_ClearLength.UseVisualStyleBackColor = true;
            this.btn_ClearLength.Click += new System.EventHandler(this.btn_ClearLength_Click);
            // 
            // tb_ClearLength
            // 
            this.tb_ClearLength.Location = new System.Drawing.Point(6, 40);
            this.tb_ClearLength.Name = "tb_ClearLength";
            this.tb_ClearLength.Size = new System.Drawing.Size(63, 21);
            this.tb_ClearLength.TabIndex = 7;
            // 
            // lab_ClearLength
            // 
            this.lab_ClearLength.AutoSize = true;
            this.lab_ClearLength.Location = new System.Drawing.Point(6, 21);
            this.lab_ClearLength.Name = "lab_ClearLength";
            this.lab_ClearLength.Size = new System.Drawing.Size(71, 12);
            this.lab_ClearLength.TabIndex = 6;
            this.lab_ClearLength.Text = "ClearLength";
            // 
            // gb_info
            // 
            this.gb_info.BorderColor = System.Drawing.Color.Black;
            this.gb_info.Controls.Add(this.btn_EndRead);
            this.gb_info.Controls.Add(this.btn_StartRead);
            this.gb_info.Controls.Add(this.tb_ReadLength);
            this.gb_info.Controls.Add(this.tb_StartAddress);
            this.gb_info.Controls.Add(this.cmb_Area);
            this.gb_info.Controls.Add(this.lab_ReadLength);
            this.gb_info.Controls.Add(this.lab_StartAddress);
            this.gb_info.Controls.Add(this.lab_Area);
            this.gb_info.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gb_info.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.gb_info.Location = new System.Drawing.Point(3, 3);
            this.gb_info.Name = "gb_info";
            this.gb_info.Size = new System.Drawing.Size(249, 129);
            this.gb_info.TabIndex = 0;
            this.gb_info.TabStop = false;
            this.gb_info.Text = "Address Information\t";
            // 
            // btn_EndRead
            // 
            this.btn_EndRead.Location = new System.Drawing.Point(122, 76);
            this.btn_EndRead.Name = "btn_EndRead";
            this.btn_EndRead.Size = new System.Drawing.Size(113, 47);
            this.btn_EndRead.TabIndex = 7;
            this.btn_EndRead.Text = "EndRead";
            this.btn_EndRead.UseVisualStyleBackColor = true;
            this.btn_EndRead.Click += new System.EventHandler(this.btn_EndRead_Click);
            // 
            // btn_StartRead
            // 
            this.btn_StartRead.Location = new System.Drawing.Point(9, 73);
            this.btn_StartRead.Name = "btn_StartRead";
            this.btn_StartRead.Size = new System.Drawing.Size(107, 50);
            this.btn_StartRead.TabIndex = 6;
            this.btn_StartRead.Tag = "button1";
            this.btn_StartRead.Text = "StartRead";
            this.btn_StartRead.UseVisualStyleBackColor = true;
            this.btn_StartRead.Click += new System.EventHandler(this.btn_StartRead_Click);
            // 
            // tb_ReadLength
            // 
            this.tb_ReadLength.Location = new System.Drawing.Point(173, 40);
            this.tb_ReadLength.Name = "tb_ReadLength";
            this.tb_ReadLength.Size = new System.Drawing.Size(63, 21);
            this.tb_ReadLength.TabIndex = 5;
            this.tb_ReadLength.Text = "50";
            // 
            // tb_StartAddress
            // 
            this.tb_StartAddress.Location = new System.Drawing.Point(78, 40);
            this.tb_StartAddress.Name = "tb_StartAddress";
            this.tb_StartAddress.Size = new System.Drawing.Size(75, 21);
            this.tb_StartAddress.TabIndex = 4;
            this.tb_StartAddress.Text = "0";
            // 
            // cmb_Area
            // 
            this.cmb_Area.AutoCompleteCustomSource.AddRange(new string[] {
            "A",
            "B",
            "C",
            "D"});
            this.cmb_Area.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmb_Area.FormattingEnabled = true;
            this.cmb_Area.Items.AddRange(new object[] {
            "A",
            "B",
            "C",
            "D"});
            this.cmb_Area.Location = new System.Drawing.Point(24, 40);
            this.cmb_Area.Name = "cmb_Area";
            this.cmb_Area.Size = new System.Drawing.Size(40, 20);
            this.cmb_Area.TabIndex = 3;
            // 
            // lab_ReadLength
            // 
            this.lab_ReadLength.AutoSize = true;
            this.lab_ReadLength.Location = new System.Drawing.Point(170, 24);
            this.lab_ReadLength.Name = "lab_ReadLength";
            this.lab_ReadLength.Size = new System.Drawing.Size(65, 12);
            this.lab_ReadLength.TabIndex = 2;
            this.lab_ReadLength.Text = "ReadLength";
            // 
            // lab_StartAddress
            // 
            this.lab_StartAddress.AutoSize = true;
            this.lab_StartAddress.Location = new System.Drawing.Point(75, 24);
            this.lab_StartAddress.Name = "lab_StartAddress";
            this.lab_StartAddress.Size = new System.Drawing.Size(77, 12);
            this.lab_StartAddress.TabIndex = 1;
            this.lab_StartAddress.Text = "StartAddress";
            // 
            // lab_Area
            // 
            this.lab_Area.AutoSize = true;
            this.lab_Area.Location = new System.Drawing.Point(22, 24);
            this.lab_Area.Name = "lab_Area";
            this.lab_Area.Size = new System.Drawing.Size(29, 12);
            this.lab_Area.TabIndex = 0;
            this.lab_Area.Text = "Area";
            // 
            // panel1
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.panel1, 5);
            this.panel1.Controls.Add(this.tab_DataInfo);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 138);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(928, 360);
            this.panel1.TabIndex = 3;
            // 
            // tab_DataInfo
            // 
            this.tab_DataInfo.Controls.Add(this.tp_DataView);
            this.tab_DataInfo.Controls.Add(this.tp_LogView);
            this.tab_DataInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tab_DataInfo.Location = new System.Drawing.Point(0, 0);
            this.tab_DataInfo.Name = "tab_DataInfo";
            this.tab_DataInfo.SelectedIndex = 0;
            this.tab_DataInfo.Size = new System.Drawing.Size(928, 360);
            this.tab_DataInfo.TabIndex = 0;
            // 
            // tp_DataView
            // 
            this.tp_DataView.Controls.Add(this.DGV);
            this.tp_DataView.Location = new System.Drawing.Point(4, 22);
            this.tp_DataView.Name = "tp_DataView";
            this.tp_DataView.Padding = new System.Windows.Forms.Padding(3);
            this.tp_DataView.Size = new System.Drawing.Size(920, 334);
            this.tp_DataView.TabIndex = 0;
            this.tp_DataView.Text = "DataView";
            this.tp_DataView.UseVisualStyleBackColor = true;
            // 
            // DGV
            // 
            this.DGV.AllowUserToAddRows = false;
            this.DGV.AllowUserToDeleteRows = false;
            this.DGV.AllowUserToResizeColumns = false;
            this.DGV.AllowUserToResizeRows = false;
            this.DGV.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DGV.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Column_Address,
            this.Column_bit15,
            this.Column_bit14,
            this.Column_bit13,
            this.Column_bit12,
            this.Column_bit11,
            this.Column_bit10,
            this.Column_9,
            this.Column_bit8,
            this.Column_bit7,
            this.Column_bit6,
            this.Column_bit5,
            this.Column_bit4,
            this.Column_bit3,
            this.Column_bit2,
            this.Column_bit1,
            this.Column_0,
            this.columnHeight,
            this.Column_Low,
            this.Column_All,
            this.Column_ascII});
            this.DGV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DGV.Location = new System.Drawing.Point(3, 3);
            this.DGV.MultiSelect = false;
            this.DGV.Name = "DGV";
            this.DGV.RowTemplate.Height = 23;
            this.DGV.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.DGV.Size = new System.Drawing.Size(914, 328);
            this.DGV.TabIndex = 1;
            this.DGV.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DGV_CellDoubleClick);
            // 
            // Column_Address
            // 
            this.Column_Address.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_Address.FillWeight = 159.8985F;
            this.Column_Address.HeaderText = "Address";
            this.Column_Address.Name = "Column_Address";
            this.Column_Address.ReadOnly = true;
            this.Column_Address.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit15
            // 
            this.Column_bit15.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit15.FillWeight = 97.00508F;
            this.Column_bit15.HeaderText = "15";
            this.Column_bit15.Name = "Column_bit15";
            this.Column_bit15.ReadOnly = true;
            this.Column_bit15.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit14
            // 
            this.Column_bit14.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit14.FillWeight = 97.00508F;
            this.Column_bit14.HeaderText = "14";
            this.Column_bit14.Name = "Column_bit14";
            this.Column_bit14.ReadOnly = true;
            this.Column_bit14.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit13
            // 
            this.Column_bit13.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit13.FillWeight = 97.00508F;
            this.Column_bit13.HeaderText = "13";
            this.Column_bit13.Name = "Column_bit13";
            this.Column_bit13.ReadOnly = true;
            this.Column_bit13.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit12
            // 
            this.Column_bit12.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit12.FillWeight = 97.00508F;
            this.Column_bit12.HeaderText = "12";
            this.Column_bit12.Name = "Column_bit12";
            this.Column_bit12.ReadOnly = true;
            this.Column_bit12.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit11
            // 
            this.Column_bit11.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit11.FillWeight = 97.00508F;
            this.Column_bit11.HeaderText = "11";
            this.Column_bit11.Name = "Column_bit11";
            this.Column_bit11.ReadOnly = true;
            this.Column_bit11.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit10
            // 
            this.Column_bit10.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit10.FillWeight = 97.00508F;
            this.Column_bit10.HeaderText = "10";
            this.Column_bit10.Name = "Column_bit10";
            this.Column_bit10.ReadOnly = true;
            this.Column_bit10.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_9
            // 
            this.Column_9.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_9.FillWeight = 97.00508F;
            this.Column_9.HeaderText = "9";
            this.Column_9.Name = "Column_9";
            this.Column_9.ReadOnly = true;
            this.Column_9.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit8
            // 
            this.Column_bit8.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit8.FillWeight = 97.00508F;
            this.Column_bit8.HeaderText = "8";
            this.Column_bit8.Name = "Column_bit8";
            this.Column_bit8.ReadOnly = true;
            this.Column_bit8.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit7
            // 
            this.Column_bit7.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit7.FillWeight = 97.00508F;
            this.Column_bit7.HeaderText = "7";
            this.Column_bit7.Name = "Column_bit7";
            this.Column_bit7.ReadOnly = true;
            this.Column_bit7.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit6
            // 
            this.Column_bit6.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit6.FillWeight = 97.00508F;
            this.Column_bit6.HeaderText = "6";
            this.Column_bit6.Name = "Column_bit6";
            this.Column_bit6.ReadOnly = true;
            this.Column_bit6.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit5
            // 
            this.Column_bit5.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit5.FillWeight = 97.00508F;
            this.Column_bit5.HeaderText = "5";
            this.Column_bit5.Name = "Column_bit5";
            this.Column_bit5.ReadOnly = true;
            this.Column_bit5.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit4
            // 
            this.Column_bit4.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit4.FillWeight = 97.00508F;
            this.Column_bit4.HeaderText = "4";
            this.Column_bit4.Name = "Column_bit4";
            this.Column_bit4.ReadOnly = true;
            this.Column_bit4.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit3
            // 
            this.Column_bit3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit3.FillWeight = 97.00508F;
            this.Column_bit3.HeaderText = "3";
            this.Column_bit3.Name = "Column_bit3";
            this.Column_bit3.ReadOnly = true;
            this.Column_bit3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit2
            // 
            this.Column_bit2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit2.FillWeight = 97.00508F;
            this.Column_bit2.HeaderText = "2";
            this.Column_bit2.Name = "Column_bit2";
            this.Column_bit2.ReadOnly = true;
            this.Column_bit2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_bit1
            // 
            this.Column_bit1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_bit1.FillWeight = 97.00508F;
            this.Column_bit1.HeaderText = "1";
            this.Column_bit1.Name = "Column_bit1";
            this.Column_bit1.ReadOnly = true;
            this.Column_bit1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_0
            // 
            this.Column_0.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_0.FillWeight = 97.00508F;
            this.Column_0.HeaderText = "0";
            this.Column_0.Name = "Column_0";
            this.Column_0.ReadOnly = true;
            this.Column_0.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // columnHeight
            // 
            this.columnHeight.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.columnHeight.FillWeight = 97.00508F;
            this.columnHeight.HeaderText = "Height";
            this.columnHeight.Name = "columnHeight";
            this.columnHeight.ReadOnly = true;
            this.columnHeight.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_Low
            // 
            this.Column_Low.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_Low.FillWeight = 97.00508F;
            this.Column_Low.HeaderText = "Low";
            this.Column_Low.Name = "Column_Low";
            this.Column_Low.ReadOnly = true;
            this.Column_Low.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_All
            // 
            this.Column_All.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_All.FillWeight = 97.00508F;
            this.Column_All.HeaderText = "All";
            this.Column_All.Name = "Column_All";
            this.Column_All.ReadOnly = true;
            this.Column_All.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Column_ascII
            // 
            this.Column_ascII.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Column_ascII.FillWeight = 97.00508F;
            this.Column_ascII.HeaderText = "ASCII";
            this.Column_ascII.Name = "Column_ascII";
            this.Column_ascII.ReadOnly = true;
            this.Column_ascII.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // tp_LogView
            // 
            this.tp_LogView.Controls.Add(this.panel2);
            this.tp_LogView.Location = new System.Drawing.Point(4, 22);
            this.tp_LogView.Name = "tp_LogView";
            this.tp_LogView.Padding = new System.Windows.Forms.Padding(3);
            this.tp_LogView.Size = new System.Drawing.Size(920, 334);
            this.tp_LogView.TabIndex = 1;
            this.tp_LogView.Text = "LogView";
            this.tp_LogView.UseVisualStyleBackColor = true;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.tb_Info);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(3, 3);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(914, 328);
            this.panel2.TabIndex = 0;
            // 
            // tb_Info
            // 
            this.tb_Info.BackColor = System.Drawing.SystemColors.Info;
            this.tb_Info.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tb_Info.Location = new System.Drawing.Point(0, 0);
            this.tb_Info.Multiline = true;
            this.tb_Info.Name = "tb_Info";
            this.tb_Info.Size = new System.Drawing.Size(914, 328);
            this.tb_Info.TabIndex = 0;
            // 
            // gb_WriteBit
            // 
            this.gb_WriteBit.BorderColor = System.Drawing.Color.Black;
            this.gb_WriteBit.Controls.Add(this.cmb_WriteBitArea);
            this.gb_WriteBit.Controls.Add(this.tb_WriteBitValue);
            this.gb_WriteBit.Controls.Add(this.tb_WriteBitAddress);
            this.gb_WriteBit.Controls.Add(this.btn_WriteBitData);
            this.gb_WriteBit.Controls.Add(this.lab_WriteBitValue);
            this.gb_WriteBit.Controls.Add(this.lab_WriteBitAddress);
            this.gb_WriteBit.Controls.Add(this.lab_WriteBitArea);
            this.gb_WriteBit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gb_WriteBit.Location = new System.Drawing.Point(258, 3);
            this.gb_WriteBit.Name = "gb_WriteBit";
            this.gb_WriteBit.Size = new System.Drawing.Size(191, 129);
            this.gb_WriteBit.TabIndex = 4;
            this.gb_WriteBit.TabStop = false;
            this.gb_WriteBit.Text = "WriteBit";
            // 
            // cmb_WriteBitArea
            // 
            this.cmb_WriteBitArea.AutoCompleteCustomSource.AddRange(new string[] {
            "A",
            "B",
            "C",
            "D"});
            this.cmb_WriteBitArea.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmb_WriteBitArea.FormattingEnabled = true;
            this.cmb_WriteBitArea.Items.AddRange(new object[] {
            "A",
            "B",
            "C",
            "D"});
            this.cmb_WriteBitArea.Location = new System.Drawing.Point(110, 20);
            this.cmb_WriteBitArea.Name = "cmb_WriteBitArea";
            this.cmb_WriteBitArea.Size = new System.Drawing.Size(71, 20);
            this.cmb_WriteBitArea.TabIndex = 7;
            // 
            // tb_WriteBitValue
            // 
            this.tb_WriteBitValue.Location = new System.Drawing.Point(110, 70);
            this.tb_WriteBitValue.Name = "tb_WriteBitValue";
            this.tb_WriteBitValue.Size = new System.Drawing.Size(71, 21);
            this.tb_WriteBitValue.TabIndex = 6;
            // 
            // tb_WriteBitAddress
            // 
            this.tb_WriteBitAddress.Location = new System.Drawing.Point(110, 46);
            this.tb_WriteBitAddress.Name = "tb_WriteBitAddress";
            this.tb_WriteBitAddress.Size = new System.Drawing.Size(71, 21);
            this.tb_WriteBitAddress.TabIndex = 5;
            // 
            // btn_WriteBitData
            // 
            this.btn_WriteBitData.Enabled = false;
            this.btn_WriteBitData.Location = new System.Drawing.Point(39, 100);
            this.btn_WriteBitData.Name = "btn_WriteBitData";
            this.btn_WriteBitData.Size = new System.Drawing.Size(119, 23);
            this.btn_WriteBitData.TabIndex = 3;
            this.btn_WriteBitData.Text = "WriteBitData";
            this.btn_WriteBitData.UseVisualStyleBackColor = true;
            this.btn_WriteBitData.Click += new System.EventHandler(this.btn_WriteBitData_Click);
            // 
            // lab_WriteBitValue
            // 
            this.lab_WriteBitValue.AutoSize = true;
            this.lab_WriteBitValue.Location = new System.Drawing.Point(6, 73);
            this.lab_WriteBitValue.Name = "lab_WriteBitValue";
            this.lab_WriteBitValue.Size = new System.Drawing.Size(89, 12);
            this.lab_WriteBitValue.TabIndex = 2;
            this.lab_WriteBitValue.Text = "WriteBitValue:";
            // 
            // lab_WriteBitAddress
            // 
            this.lab_WriteBitAddress.AutoSize = true;
            this.lab_WriteBitAddress.Location = new System.Drawing.Point(6, 49);
            this.lab_WriteBitAddress.Name = "lab_WriteBitAddress";
            this.lab_WriteBitAddress.Size = new System.Drawing.Size(101, 12);
            this.lab_WriteBitAddress.TabIndex = 1;
            this.lab_WriteBitAddress.Text = "WriteBitAddress:";
            // 
            // lab_WriteBitArea
            // 
            this.lab_WriteBitArea.AutoSize = true;
            this.lab_WriteBitArea.Location = new System.Drawing.Point(6, 23);
            this.lab_WriteBitArea.Name = "lab_WriteBitArea";
            this.lab_WriteBitArea.Size = new System.Drawing.Size(83, 12);
            this.lab_WriteBitArea.TabIndex = 0;
            this.lab_WriteBitArea.Text = "WriteBitArea:";
            // 
            // gb_WriteWord
            // 
            this.gb_WriteWord.BorderColor = System.Drawing.Color.Black;
            this.gb_WriteWord.Controls.Add(this.cmb_WriteWordArea);
            this.gb_WriteWord.Controls.Add(this.tb_WriteWordValue);
            this.gb_WriteWord.Controls.Add(this.tb_WriteWordAddress);
            this.gb_WriteWord.Controls.Add(this.btn_WriteWordData);
            this.gb_WriteWord.Controls.Add(this.lab_WriteWordValue);
            this.gb_WriteWord.Controls.Add(this.lab_WriteWordAddress);
            this.gb_WriteWord.Controls.Add(this.lab_WriteWordArea);
            this.gb_WriteWord.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gb_WriteWord.Location = new System.Drawing.Point(455, 3);
            this.gb_WriteWord.Name = "gb_WriteWord";
            this.gb_WriteWord.Size = new System.Drawing.Size(196, 129);
            this.gb_WriteWord.TabIndex = 5;
            this.gb_WriteWord.TabStop = false;
            this.gb_WriteWord.Text = "WriteWord";
            // 
            // cmb_WriteWordArea
            // 
            this.cmb_WriteWordArea.AutoCompleteCustomSource.AddRange(new string[] {
            "A",
            "B",
            "C",
            "D"});
            this.cmb_WriteWordArea.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmb_WriteWordArea.FormattingEnabled = true;
            this.cmb_WriteWordArea.Items.AddRange(new object[] {
            "A",
            "B",
            "C",
            "D"});
            this.cmb_WriteWordArea.Location = new System.Drawing.Point(119, 19);
            this.cmb_WriteWordArea.Name = "cmb_WriteWordArea";
            this.cmb_WriteWordArea.Size = new System.Drawing.Size(71, 20);
            this.cmb_WriteWordArea.TabIndex = 14;
            // 
            // tb_WriteWordValue
            // 
            this.tb_WriteWordValue.Location = new System.Drawing.Point(119, 73);
            this.tb_WriteWordValue.Name = "tb_WriteWordValue";
            this.tb_WriteWordValue.Size = new System.Drawing.Size(71, 21);
            this.tb_WriteWordValue.TabIndex = 13;
            // 
            // tb_WriteWordAddress
            // 
            this.tb_WriteWordAddress.Location = new System.Drawing.Point(119, 45);
            this.tb_WriteWordAddress.Name = "tb_WriteWordAddress";
            this.tb_WriteWordAddress.Size = new System.Drawing.Size(71, 21);
            this.tb_WriteWordAddress.TabIndex = 12;
            // 
            // btn_WriteWordData
            // 
            this.btn_WriteWordData.Enabled = false;
            this.btn_WriteWordData.Location = new System.Drawing.Point(27, 99);
            this.btn_WriteWordData.Name = "btn_WriteWordData";
            this.btn_WriteWordData.Size = new System.Drawing.Size(119, 23);
            this.btn_WriteWordData.TabIndex = 10;
            this.btn_WriteWordData.Text = "WriteWordData";
            this.btn_WriteWordData.UseVisualStyleBackColor = true;
            this.btn_WriteWordData.Click += new System.EventHandler(this.btn_WriteWordData_Click);
            // 
            // lab_WriteWordValue
            // 
            this.lab_WriteWordValue.AutoSize = true;
            this.lab_WriteWordValue.Location = new System.Drawing.Point(7, 76);
            this.lab_WriteWordValue.Name = "lab_WriteWordValue";
            this.lab_WriteWordValue.Size = new System.Drawing.Size(95, 12);
            this.lab_WriteWordValue.TabIndex = 9;
            this.lab_WriteWordValue.Text = "WriteWordValue:";
            // 
            // lab_WriteWordAddress
            // 
            this.lab_WriteWordAddress.AutoSize = true;
            this.lab_WriteWordAddress.Location = new System.Drawing.Point(7, 48);
            this.lab_WriteWordAddress.Name = "lab_WriteWordAddress";
            this.lab_WriteWordAddress.Size = new System.Drawing.Size(107, 12);
            this.lab_WriteWordAddress.TabIndex = 8;
            this.lab_WriteWordAddress.Text = "WriteWordAddress:";
            // 
            // lab_WriteWordArea
            // 
            this.lab_WriteWordArea.AutoSize = true;
            this.lab_WriteWordArea.Location = new System.Drawing.Point(7, 23);
            this.lab_WriteWordArea.Name = "lab_WriteWordArea";
            this.lab_WriteWordArea.Size = new System.Drawing.Size(89, 12);
            this.lab_WriteWordArea.TabIndex = 7;
            this.lab_WriteWordArea.Text = "WriteWordArea:";
            // 
            // groupBox3
            // 
            this.groupBox3.BorderColor = System.Drawing.Color.Black;
            this.groupBox3.Controls.Add(this.btn_StartAddressAdd50);
            this.groupBox3.Controls.Add(this.btn_StartAddressDecrease50);
            this.groupBox3.Controls.Add(this.btn_StartAddressAdd20);
            this.groupBox3.Controls.Add(this.btn_StartAddressDecrease20);
            this.groupBox3.Controls.Add(this.btn_StartAddressAdd5);
            this.groupBox3.Controls.Add(this.btn_StartAddressDecrease5);
            this.groupBox3.Controls.Add(this.btn_StartAddressAdd1);
            this.groupBox3.Controls.Add(this.btn_StartAddressDecrease1);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(657, 3);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(139, 129);
            this.groupBox3.TabIndex = 6;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Address Movement";
            // 
            // btn_StartAddressAdd50
            // 
            this.btn_StartAddressAdd50.Location = new System.Drawing.Point(74, 98);
            this.btn_StartAddressAdd50.Name = "btn_StartAddressAdd50";
            this.btn_StartAddressAdd50.Size = new System.Drawing.Size(47, 23);
            this.btn_StartAddressAdd50.TabIndex = 7;
            this.btn_StartAddressAdd50.Tag = "50";
            this.btn_StartAddressAdd50.Text = "+50";
            this.btn_StartAddressAdd50.UseVisualStyleBackColor = true;
            // 
            // btn_StartAddressDecrease50
            // 
            this.btn_StartAddressDecrease50.Location = new System.Drawing.Point(7, 98);
            this.btn_StartAddressDecrease50.Name = "btn_StartAddressDecrease50";
            this.btn_StartAddressDecrease50.Size = new System.Drawing.Size(50, 20);
            this.btn_StartAddressDecrease50.TabIndex = 6;
            this.btn_StartAddressDecrease50.Tag = "-50";
            this.btn_StartAddressDecrease50.Text = "-50";
            this.btn_StartAddressDecrease50.UseVisualStyleBackColor = true;
            // 
            // btn_StartAddressAdd20
            // 
            this.btn_StartAddressAdd20.Location = new System.Drawing.Point(74, 72);
            this.btn_StartAddressAdd20.Name = "btn_StartAddressAdd20";
            this.btn_StartAddressAdd20.Size = new System.Drawing.Size(50, 20);
            this.btn_StartAddressAdd20.TabIndex = 5;
            this.btn_StartAddressAdd20.Tag = "20";
            this.btn_StartAddressAdd20.Text = "+20";
            this.btn_StartAddressAdd20.UseVisualStyleBackColor = true;
            // 
            // btn_StartAddressDecrease20
            // 
            this.btn_StartAddressDecrease20.Location = new System.Drawing.Point(7, 72);
            this.btn_StartAddressDecrease20.Name = "btn_StartAddressDecrease20";
            this.btn_StartAddressDecrease20.Size = new System.Drawing.Size(50, 20);
            this.btn_StartAddressDecrease20.TabIndex = 4;
            this.btn_StartAddressDecrease20.Tag = "-20";
            this.btn_StartAddressDecrease20.Text = "-20";
            this.btn_StartAddressDecrease20.UseVisualStyleBackColor = true;
            // 
            // btn_StartAddressAdd5
            // 
            this.btn_StartAddressAdd5.Location = new System.Drawing.Point(74, 46);
            this.btn_StartAddressAdd5.Name = "btn_StartAddressAdd5";
            this.btn_StartAddressAdd5.Size = new System.Drawing.Size(50, 20);
            this.btn_StartAddressAdd5.TabIndex = 3;
            this.btn_StartAddressAdd5.Tag = "5";
            this.btn_StartAddressAdd5.Text = "+5";
            this.btn_StartAddressAdd5.UseVisualStyleBackColor = true;
            // 
            // btn_StartAddressDecrease5
            // 
            this.btn_StartAddressDecrease5.Location = new System.Drawing.Point(7, 46);
            this.btn_StartAddressDecrease5.Name = "btn_StartAddressDecrease5";
            this.btn_StartAddressDecrease5.Size = new System.Drawing.Size(50, 20);
            this.btn_StartAddressDecrease5.TabIndex = 2;
            this.btn_StartAddressDecrease5.Tag = "-5";
            this.btn_StartAddressDecrease5.Text = "-5";
            this.btn_StartAddressDecrease5.UseVisualStyleBackColor = true;
            // 
            // btn_StartAddressAdd1
            // 
            this.btn_StartAddressAdd1.Location = new System.Drawing.Point(74, 20);
            this.btn_StartAddressAdd1.Name = "btn_StartAddressAdd1";
            this.btn_StartAddressAdd1.Size = new System.Drawing.Size(50, 20);
            this.btn_StartAddressAdd1.TabIndex = 1;
            this.btn_StartAddressAdd1.Tag = "1";
            this.btn_StartAddressAdd1.Text = "+1";
            this.btn_StartAddressAdd1.UseVisualStyleBackColor = true;
            // 
            // btn_StartAddressDecrease1
            // 
            this.btn_StartAddressDecrease1.Location = new System.Drawing.Point(6, 20);
            this.btn_StartAddressDecrease1.Name = "btn_StartAddressDecrease1";
            this.btn_StartAddressDecrease1.Size = new System.Drawing.Size(50, 20);
            this.btn_StartAddressDecrease1.TabIndex = 0;
            this.btn_StartAddressDecrease1.Tag = "-1";
            this.btn_StartAddressDecrease1.Text = "-1";
            this.btn_StartAddressDecrease1.UseVisualStyleBackColor = true;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Interval = 500;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // PlcClientForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(934, 501);
            this.Controls.Add(this.tableLayoutPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PlcClientForm";
            this.Text = "PLCClient";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.gb_cleardata.ResumeLayout(false);
            this.gb_cleardata.PerformLayout();
            this.gb_info.ResumeLayout(false);
            this.gb_info.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.tab_DataInfo.ResumeLayout(false);
            this.tp_DataView.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.DGV)).EndInit();
            this.tp_LogView.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.gb_WriteBit.ResumeLayout(false);
            this.gb_WriteBit.PerformLayout();
            this.gb_WriteWord.ResumeLayout(false);
            this.gb_WriteWord.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TextBox tb_ReadLength;
        private System.Windows.Forms.TextBox tb_StartAddress;
        private System.Windows.Forms.ComboBox cmb_Area;
        private System.Windows.Forms.Label lab_ReadLength;
        private System.Windows.Forms.Label lab_StartAddress;
        private System.Windows.Forms.Label lab_Area;
        private System.Windows.Forms.Button btn_StartRead;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button btn_ClearAll;
        private System.Windows.Forms.Button btn_ClearLength;
        private System.Windows.Forms.TextBox tb_ClearLength;
        private System.Windows.Forms.Label lab_ClearLength;
        private System.Windows.Forms.TextBox tb_WriteBitValue;
        private System.Windows.Forms.TextBox tb_WriteBitAddress;
        private System.Windows.Forms.Button btn_WriteBitData;
        private System.Windows.Forms.Label lab_WriteBitValue;
        private System.Windows.Forms.Label lab_WriteBitAddress;
        private System.Windows.Forms.Label lab_WriteBitArea;
        private System.Windows.Forms.TextBox tb_WriteWordValue;
        private System.Windows.Forms.TextBox tb_WriteWordAddress;
        private System.Windows.Forms.Button btn_WriteWordData;
        private System.Windows.Forms.Label lab_WriteWordValue;
        private System.Windows.Forms.Label lab_WriteWordAddress;
        private System.Windows.Forms.Label lab_WriteWordArea;
        private System.Windows.Forms.ComboBox cmb_WriteBitArea;
        private System.Windows.Forms.ComboBox cmb_WriteWordArea;
        private System.Windows.Forms.Button btn_StartAddressAdd50;
        private System.Windows.Forms.Button btn_StartAddressDecrease50;
        private System.Windows.Forms.Button btn_StartAddressAdd20;
        private System.Windows.Forms.Button btn_StartAddressDecrease20;
        private System.Windows.Forms.Button btn_StartAddressAdd5;
        private System.Windows.Forms.Button btn_StartAddressDecrease5;
        private System.Windows.Forms.Button btn_StartAddressAdd1;
        private System.Windows.Forms.Button btn_StartAddressDecrease1;
        private System.Windows.Forms.Button btn_EndRead;
        private MGroupBox gb_info;
        private MGroupBox gb_cleardata;
        private MGroupBox gb_WriteBit;
        private MGroupBox gb_WriteWord;
        private MGroupBox groupBox3;
        private System.Windows.Forms.TabControl tab_DataInfo;
        private System.Windows.Forms.TabPage tp_DataView;
        private System.Windows.Forms.DataGridView DGV;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_Address;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit15;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit14;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit13;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit12;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit11;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit10;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_9;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit8;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit7;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit6;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit5;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit4;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit3;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit2;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_bit1;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_0;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnHeight;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_Low;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_All;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column_ascII;
        private System.Windows.Forms.TabPage tp_LogView;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.TextBox tb_Info;
    }
}

