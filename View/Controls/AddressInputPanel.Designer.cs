namespace PlcTest.View.Controls
{
    partial class AddressInputPanel
    {
        /// <summary>
        /// 设计器容器（当前没有需要释放的组件，保留是为了符合 Designer 的约定）。
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
        /// 设计器生成：五个字段与提示行。同步规则（文本 ⇄ 下拉）都在主文件里。
        /// </summary>
        private void InitializeComponent()
        {
            this._flow = new System.Windows.Forms.FlowLayoutPanel();
            this._labAddress = new System.Windows.Forms.Label();
            this._tbAddress = new System.Windows.Forms.TextBox();
            this._labArea = new System.Windows.Forms.Label();
            this._cmbArea = new System.Windows.Forms.ComboBox();
            this._labOffset = new System.Windows.Forms.Label();
            this._tbOffset = new System.Windows.Forms.TextBox();
            this._labLength = new System.Windows.Forms.Label();
            this._tbLength = new System.Windows.Forms.TextBox();
            this._labKind = new System.Windows.Forms.Label();
            this._cmbKind = new System.Windows.Forms.ComboBox();
            this._labHint = new System.Windows.Forms.Label();
            this._flow.SuspendLayout();
            this.SuspendLayout();
            // 
            // _flow
            // 
            this._flow.AutoScroll = true;
            this._flow.Controls.Add(this._labAddress);
            this._flow.Controls.Add(this._tbAddress);
            this._flow.Controls.Add(this._labArea);
            this._flow.Controls.Add(this._cmbArea);
            this._flow.Controls.Add(this._labOffset);
            this._flow.Controls.Add(this._tbOffset);
            this._flow.Controls.Add(this._labLength);
            this._flow.Controls.Add(this._tbLength);
            this._flow.Controls.Add(this._labKind);
            this._flow.Controls.Add(this._cmbKind);
            this._flow.Controls.Add(this._labHint);
            this._flow.Dock = System.Windows.Forms.DockStyle.Fill;
            this._flow.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this._flow.Location = new System.Drawing.Point(0, 0);
            this._flow.Name = "_flow";
            this._flow.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this._flow.Size = new System.Drawing.Size(600, 34);
            this._flow.TabIndex = 0;
            this._flow.WrapContents = true;
            // 
            // _labAddress
            // 
            this._labAddress.AutoSize = true;
            this._labAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labAddress.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._labAddress.Location = new System.Drawing.Point(0, 11);
            this._labAddress.Margin = new System.Windows.Forms.Padding(0, 7, 2, 0);
            this._labAddress.Name = "_labAddress";
            this._labAddress.Size = new System.Drawing.Size(29, 12);
            this._labAddress.TabIndex = 0;
            this._labAddress.Text = "地址";
            // 
            // _tbAddress
            // 
            this._tbAddress.Font = new System.Drawing.Font("Consolas", 9F);
            this._tbAddress.Location = new System.Drawing.Point(31, 7);
            this._tbAddress.Name = "_tbAddress";
            this._tbAddress.Size = new System.Drawing.Size(130, 22);
            this._tbAddress.TabIndex = 1;
            // 
            // _labArea
            // 
            this._labArea.AutoSize = true;
            this._labArea.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labArea.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._labArea.Location = new System.Drawing.Point(164, 11);
            this._labArea.Margin = new System.Windows.Forms.Padding(0, 7, 2, 0);
            this._labArea.Name = "_labArea";
            this._labArea.Size = new System.Drawing.Size(29, 12);
            this._labArea.TabIndex = 2;
            this._labArea.Text = "区域";
            // 
            // _cmbArea
            // 
            this._cmbArea.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cmbArea.FormattingEnabled = true;
            this._cmbArea.Location = new System.Drawing.Point(195, 7);
            this._cmbArea.Name = "_cmbArea";
            this._cmbArea.Size = new System.Drawing.Size(72, 20);
            this._cmbArea.TabIndex = 3;
            // 
            // _labOffset
            // 
            this._labOffset.AutoSize = true;
            this._labOffset.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labOffset.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._labOffset.Location = new System.Drawing.Point(270, 11);
            this._labOffset.Margin = new System.Windows.Forms.Padding(0, 7, 2, 0);
            this._labOffset.Name = "_labOffset";
            this._labOffset.Size = new System.Drawing.Size(29, 12);
            this._labOffset.TabIndex = 4;
            this._labOffset.Text = "偏移";
            // 
            // _tbOffset
            // 
            this._tbOffset.Location = new System.Drawing.Point(301, 7);
            this._tbOffset.Name = "_tbOffset";
            this._tbOffset.Size = new System.Drawing.Size(70, 21);
            this._tbOffset.TabIndex = 5;
            // 
            // _labLength
            // 
            this._labLength.AutoSize = true;
            this._labLength.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labLength.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._labLength.Location = new System.Drawing.Point(379, 11);
            this._labLength.Margin = new System.Windows.Forms.Padding(8, 7, 2, 0);
            this._labLength.Name = "_labLength";
            this._labLength.Size = new System.Drawing.Size(29, 12);
            this._labLength.TabIndex = 6;
            this._labLength.Text = "长度";
            // 
            // _tbLength
            // 
            this._tbLength.Location = new System.Drawing.Point(410, 7);
            this._tbLength.Name = "_tbLength";
            this._tbLength.Size = new System.Drawing.Size(48, 21);
            this._tbLength.TabIndex = 7;
            this._tbLength.Text = "1";
            // 
            // _labKind
            // 
            this._labKind.AutoSize = true;
            this._labKind.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labKind.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._labKind.Location = new System.Drawing.Point(466, 11);
            this._labKind.Margin = new System.Windows.Forms.Padding(8, 7, 2, 0);
            this._labKind.Name = "_labKind";
            this._labKind.Size = new System.Drawing.Size(29, 12);
            this._labKind.TabIndex = 8;
            this._labKind.Text = "方式";
            // 
            // _cmbKind
            // 
            this._cmbKind.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cmbKind.FormattingEnabled = true;
            this._cmbKind.Items.AddRange(new object[] {
            "位",
            "字"});
            this._cmbKind.Location = new System.Drawing.Point(497, 7);
            this._cmbKind.Name = "_cmbKind";
            this._cmbKind.SelectedIndex = 0;
            this._cmbKind.Size = new System.Drawing.Size(56, 20);
            this._cmbKind.TabIndex = 9;
            // 
            // _labHint
            // 
            this._labHint.AutoSize = true;
            this._labHint.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._labHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(95)))), ((int)(((byte)(94)))), ((int)(((byte)(90)))));
            this._labHint.Location = new System.Drawing.Point(561, 10);
            this._labHint.Margin = new System.Windows.Forms.Padding(8, 6, 0, 0);
            this._labHint.Name = "_labHint";
            this._labHint.Size = new System.Drawing.Size(0, 12);
            this._labHint.TabIndex = 10;
            // 
            // AddressInputPanel
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this._flow);
            this.Name = "AddressInputPanel";
            this.Size = new System.Drawing.Size(600, 34);
            this._flow.ResumeLayout(false);
            this._flow.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.FlowLayoutPanel _flow;
        private System.Windows.Forms.Label _labAddress;
        private System.Windows.Forms.TextBox _tbAddress;
        private System.Windows.Forms.Label _labArea;
        private System.Windows.Forms.ComboBox _cmbArea;
        private System.Windows.Forms.Label _labOffset;
        private System.Windows.Forms.TextBox _tbOffset;
        private System.Windows.Forms.Label _labLength;
        private System.Windows.Forms.TextBox _tbLength;
        private System.Windows.Forms.Label _labKind;
        private System.Windows.Forms.ComboBox _cmbKind;
        private System.Windows.Forms.Label _labHint;
    }
}
