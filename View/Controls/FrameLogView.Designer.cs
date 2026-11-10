namespace PlcTest.View.Controls
{
    partial class FrameLogView
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
        /// 设计器生成：控件树与静态布局。行为（事件、暂停队列、裁剪）都在主文件里。
        /// </summary>
        private void InitializeComponent()
        {
            this._bar = new System.Windows.Forms.Panel();
            this._btnPause = new System.Windows.Forms.Button();
            this._btnClear = new System.Windows.Forms.Button();
            this._btnCopy = new System.Windows.Forms.Button();
            this._chkAutoScroll = new System.Windows.Forms.CheckBox();
            this._box = new System.Windows.Forms.RichTextBox();
            this._bar.SuspendLayout();
            this.SuspendLayout();
            // 
            // _bar
            // 
            this._bar.BackColor = System.Drawing.SystemColors.Control;
            this._bar.Controls.Add(this._btnPause);
            this._bar.Controls.Add(this._btnClear);
            this._bar.Controls.Add(this._btnCopy);
            this._bar.Controls.Add(this._chkAutoScroll);
            this._bar.Dock = System.Windows.Forms.DockStyle.Top;
            this._bar.Location = new System.Drawing.Point(0, 0);
            this._bar.Name = "_bar";
            this._bar.Size = new System.Drawing.Size(600, 26);
            this._bar.TabIndex = 0;
            // 
            // _btnPause
            // 
            this._btnPause.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnPause.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._btnPause.Location = new System.Drawing.Point(2, 2);
            this._btnPause.Name = "_btnPause";
            this._btnPause.Size = new System.Drawing.Size(52, 22);
            this._btnPause.TabIndex = 0;
            this._btnPause.Text = "暂停";
            // 
            // _btnClear
            // 
            this._btnClear.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnClear.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._btnClear.Location = new System.Drawing.Point(58, 2);
            this._btnClear.Name = "_btnClear";
            this._btnClear.Size = new System.Drawing.Size(52, 22);
            this._btnClear.TabIndex = 1;
            this._btnClear.Text = "清空";
            // 
            // _btnCopy
            // 
            this._btnCopy.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this._btnCopy.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._btnCopy.Location = new System.Drawing.Point(114, 2);
            this._btnCopy.Name = "_btnCopy";
            this._btnCopy.Size = new System.Drawing.Size(52, 22);
            this._btnCopy.TabIndex = 2;
            this._btnCopy.Text = "复制";
            // 
            // _chkAutoScroll
            // 
            this._chkAutoScroll.AutoSize = true;
            this._chkAutoScroll.Checked = true;
            this._chkAutoScroll.CheckState = System.Windows.Forms.CheckState.Checked;
            this._chkAutoScroll.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this._chkAutoScroll.Location = new System.Drawing.Point(174, 6);
            this._chkAutoScroll.Name = "_chkAutoScroll";
            this._chkAutoScroll.Size = new System.Drawing.Size(74, 16);
            this._chkAutoScroll.TabIndex = 3;
            this._chkAutoScroll.Text = "自动滚动";
            // 
            // _box
            // 
            this._box.BackColor = System.Drawing.Color.White;
            this._box.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._box.DetectUrls = false;
            this._box.Dock = System.Windows.Forms.DockStyle.Fill;
            this._box.Font = new System.Drawing.Font("Consolas", 9F);
            this._box.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(44)))), ((int)(((byte)(42)))));
            this._box.HideSelection = false;
            this._box.Location = new System.Drawing.Point(0, 26);
            this._box.Name = "_box";
            this._box.ReadOnly = true;
            this._box.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this._box.Size = new System.Drawing.Size(600, 334);
            this._box.TabIndex = 1;
            this._box.Text = "";
            this._box.WordWrap = false;
            // 
            // FrameLogView
            // 
            this.Controls.Add(this._box);
            this.Controls.Add(this._bar);
            this.Name = "FrameLogView";
            this.Size = new System.Drawing.Size(600, 360);
            this._bar.ResumeLayout(false);
            this._bar.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel _bar;
        private System.Windows.Forms.Button _btnPause;
        private System.Windows.Forms.Button _btnClear;
        private System.Windows.Forms.Button _btnCopy;
        private System.Windows.Forms.CheckBox _chkAutoScroll;
        private System.Windows.Forms.RichTextBox _box;
    }
}
