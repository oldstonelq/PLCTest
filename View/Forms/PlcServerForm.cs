
using PlcTest.Models;
using PlcTest.Server.PlcServer;
using PlcTest.Server.ServerInterface;
using PlcTest.Tool;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace PlcTest.View.Forms
{
    public partial class PlcServerForm : Form
    {
        /// <summary>
        /// PLC服务实例
        /// </summary>
        IPlcServer PlcServer = null;
        /// <summary>
        /// 模拟寄存器表（并非所有服务端都实现 IRegisterTable，为 null 表示不支持地址表管理）
        /// </summary>
        IRegisterTable RegisterTable = null;
        /// <summary>
        /// 当前读写寄存器
        /// </summary>
        MemoryArea CurrentReadArea = MemoryArea.D;
        /// <summary>
        /// 当前读取起始地址
        /// </summary>
        int CurrentReadAddress = 0;
        /// <summary>
        /// 当前读取长度
        /// </summary>
        int CurrentReadLength = 0;
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
        /// <param name="plcServer"></param>
        public PlcServerForm(IPlcServer plcServer)
        {
            InitializeComponent();
            this.PlcServer = plcServer;
            this.RegisterTable = plcServer as IRegisterTable;
        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlcServerForm_Load(object sender, EventArgs e)
        {
            InitCombox();
        }
        /// <summary>
        /// 初始化下拉控件
        /// </summary>
        private void InitCombox()
        {
            cmb_Area.Items.Clear();
            cmb_Area.Items.AddRange(Enum.GetNames(typeof(MemoryArea)));

            cmb_WriteBitArea.Items.Clear();
            cmb_WriteBitArea.Items.AddRange(Enum.GetNames(typeof(MemoryArea)));

            cmb_WriteWordArea.Items.Clear();
            cmb_WriteWordArea.Items.AddRange(Enum.GetNames(typeof(MemoryArea)));
        }
        /// <summary>
        /// 初始化当前区域的模拟寄存器范围
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void btn_StartRead_Click(object sender, EventArgs e)
        {
            if (CheckCanStartRead())
            {
                RegisterTable?.ClearRegisters(CurrentReadArea);
                // 注册 [起始地址, 起始地址 + 长度) 区间：原写成 i < CurrentReadLength，起始 100/长度 10 时一个都不注册
                for (int i = CurrentReadAddress; i < CurrentReadAddress + CurrentReadLength; i++)
                {
                    RegisterTable?.TryAddRegister(CurrentReadArea, i);
                }
            }
            else
            {
                MessageBox.Show("Please Check Address Information ");
            }
        }
   
        /// <summary>
        /// 清空当前区域的模拟寄存器
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void btn_EndRead_Click(object sender, EventArgs e)
        {
            RegisterTable?.ClearRegisters(CurrentReadArea);
        }
        /// <summary>
        /// 写入单个位到模拟寄存器
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void btn_WriteBitData_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cmb_WriteBitArea.Text))
            {
                return;
            }
            if (!Enum.TryParse(cmb_WriteBitArea.Text, out MemoryArea WriteArea))
            {
                return;
            }
            if (!OtherTool .StringTryParseInt (tb_WriteBitAddress.Text, out var WriteAddress,0))
            {
                return;
            }
            if (!short.TryParse(tb_WriteBitValue.Text, out var WriteValue))
            {
                return;
            }
            if (WriteArea == MemoryArea.D) return;

            PlcServer.WriteBit(PlcAddress.Bit(WriteArea, WriteAddress), WriteValue == 1);
        }
        /// <summary>
        /// 写入单个字到模拟寄存器
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void btn_WriteWordData_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cmb_WriteWordArea.Text))
            {
                return;
            }
            if (!Enum.TryParse(cmb_WriteWordArea.Text, out MemoryArea WriteArea))
            {
                return;
            }
            if (!OtherTool.StringTryParseInt(tb_WriteWordAddress.Text, out var WriteAddress,0))
            {
                return;
            }
            if (!short.TryParse(tb_WriteWordValue.Text, out var WriteValue))
            {
                return;
            }
            if (WriteArea != MemoryArea.D) return;
            PlcServer.WriteWord(PlcAddress.Word(WriteArea, WriteAddress), WriteValue);
        }
        /// <summary>
        /// 将当前区域指定长度的寄存器重置为默认值
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>

        private void btn_ClearLength_Click(object sender, EventArgs e)
        {
            if (!OtherTool.StringTryParseInt(tb_ClearLength.Text, out var length, 1))
            {
                MessageBox.Show("Please Check ClearLength ");
                return;
            }
            if (length > (RegisterTable?.GetRegisterCount(CurrentReadArea) ?? 0))
            {
                return;
            }
            RegisterTable?.ResetRegisterValues(CurrentReadArea, CurrentReadAddress, length);
        }
        /// <summary>
        /// 将当前区域全部寄存器重置为默认值
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void btn_ClearAll_Click(object sender, EventArgs e)
        {
            RegisterTable?.ResetAllRegisterValues(CurrentReadArea);
        }
        /// <summary>
        /// 定时刷新服务端状态和表格显示。
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            RefreshBtn();
            RefreshDgv();
        }
        /// <summary>
        /// 根据服务端运行状态更新按钮可用性
        /// </summary>
        private void RefreshBtn()
        {
            if (PlcServer != null && PlcServer.ServerIsOpen)
            {
                btn_WriteBitData.Enabled = true;
                btn_WriteWordData.Enabled = true;
                btn_ClearLength.Enabled = true;
                btn_ClearAll.Enabled = true;
            }
            else
            {
                btn_WriteBitData.Enabled = false;
                btn_WriteWordData.Enabled = false;
                btn_ClearLength.Enabled = false;
                btn_ClearAll.Enabled = false;
            }
        }
        /// <summary>
        /// 刷新模拟寄存器表格视图
        /// </summary>
        private void RefreshDgv()
        {
            EnsureDataGridViewRowCount(CurrentReadLength);
        }

        /// <summary>
        /// 安全调整表格行数，并将模拟寄存器值同步到表格
        /// </summary>
        /// <param name="targetCount">目标行数</param>
        private void EnsureDataGridViewRowCount(int targetCount)
        {
            if (DGV == null)
                return;

            // 跨线程安全：在 UI 线程上执行实际更新
            if (DGV.InvokeRequired)
            {
                DGV.BeginInvoke((Action)(() => EnsureDataGridViewRowCount(targetCount)));
                return;
            }

            DGV.SuspendLayout();
            try
            {

                // 如果启用了 AllowUserToAddRows，最后一行为“新建行”，不计入实际数据行数
                bool hasNewRow = DGV.AllowUserToAddRows;
                int actualCount = DGV.Rows.Count - (hasNewRow ? 1 : 0);
                if (actualCount < 0) actualCount = 0;

                // 减少行数：从末尾安全删除（不删除“新建行”）
                while (actualCount > targetCount)
                {
                    int removeIndex = DGV.Rows.Count - (hasNewRow ? 2 : 1);
                    if (removeIndex < 0) break;
                    DGV.Rows.RemoveAt(removeIndex);
                    actualCount--;
                }

                // 增加行数：逐行添加空行（如需性能优化，可批量构建数组并一次 AddRange）
                while (actualCount < targetCount)
                {
                    DGV.Rows.Add();
                    actualCount++;
                }

                for (int i = 0; i < DGV.Rows.Count; i++)
                {
                    DGV.Rows[i].Cells[0].Value = CurrentReadArea.ToString() + (i + CurrentReadAddress).ToString();

                }

                if (CurrentReadArea == MemoryArea.D)
                {
                    if ((RegisterTable?.GetRegisterCount(CurrentReadArea) ?? 0) > 0)
                    {
                        for (int i = 0; i < DGV.Rows.Count; i++)
                        {
                            var wordResult = PlcServer.ReadWord(PlcAddress.Word(CurrentReadArea, CurrentReadAddress + i));
                            short value = wordResult.IsSuccess ? wordResult.Data : (short)0;
                            var bits = ConverterTool.ShortToBoolArray(value);
                            if (bits == null || bits.Length < 16) continue;

                            for (int j = 0; j < 16; j++)
                            {
                                int bitIndex = 15 - j; // map bits[15]..bits[0] to columns 1..16
                                DGV.Rows[i].Cells[j + 1].Value = bits[bitIndex] ? 1 : 0;
                                if (bits[bitIndex])
                                {
                                    DGV.Rows[i].Cells[j + 1].Style.BackColor = Color.LightBlue;
                                }
                                else
                                {
                                    DGV.Rows[i].Cells[j + 1].Style.BackColor = Color.Empty;
                                }
                            }

                            DGV.Rows[i].Cells[17].Value = ConverterTool.GetHighByteOfShort(value);
                            DGV.Rows[i].Cells[18].Value = ConverterTool.GetLowByteOfShort(value);
                            DGV.Rows[i].Cells[19].Value = value;
                            DGV.Rows[i].Cells[20].Value = ConverterTool.ShortToAscii(value);
                        }
                    }
                    else
                    {
                        DGV.Rows.Clear();
                    }
                }
                else
                {
                    if ((RegisterTable?.GetRegisterCount(CurrentReadArea) ?? 0) > 0)
                    {

                        for (int i = 0; i < DGV.Rows.Count; i++)
                        {
                            var bitResult = PlcServer.ReadBit(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress + i));
                            bool bitValue = bitResult.IsSuccess && bitResult.Data;
                            DGV.Rows[i].Cells[16].Value = bitValue ? 1 : 0;
                            if (bitValue)
                            {
                                DGV.Rows[i].Cells[16].Style.BackColor = Color.LightBlue;
                            }
                            else
                            {
                                DGV.Rows[i].Cells[16].Style.BackColor = Color.Empty;
                            }
                        }
                    }
                    else
                    {
                        DGV.Rows.Clear();
                    }
                }
            }
            finally
            {
                DGV.ResumeLayout();
            }
        }
        /// <summary>
        /// 处理表格单元格双击，切换位值或修改字中的指定 bit
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void DGV_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                //这个地方操作比较特殊，双击的时候，获取当前的行和列，然后重新换算数据到PLC里面去，写入数据
                int rowindex = DGV.CurrentCell.RowIndex;
                int columnindex = DGV.CurrentCell.ColumnIndex;
                if (columnindex <= 0 || columnindex > 16)
                {
                    return;
                }
                if (rowindex < 0)
                {
                    return;
                }
                if (CurrentReadArea == MemoryArea.D)
                {
                    var wordResult = PlcServer.ReadWord(PlcAddress.Word(CurrentReadArea, CurrentReadAddress + rowindex));
                    short currentValue = wordResult.IsSuccess ? wordResult.Data : (short)0;
                    //2.根据当前的值和列数，计算出要写入PLC的值
                    int bitPosition = 16 - columnindex; // 列1对应bit15，列16对应bit0  
                    short writeValue = (short)ConverterTool.ToggleBit(currentValue, bitPosition);
                    PlcServer.WriteWord(PlcAddress.Word(CurrentReadArea, CurrentReadAddress + rowindex), writeValue);
                }
                else
                {
                    var bitResult = PlcServer.ReadBit(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress + rowindex));
                    bool currentBit = bitResult.IsSuccess && bitResult.Data;
                    PlcServer.WriteBit(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress + rowindex), !currentBit);
                }
            }
            catch (Exception)
            {
                if (this.IsHandleCreated)
                {
                    //this.BeginInvoke((Action)(() => MessageBox.Show(this, ex.Message)));
                }
            }
        }
        /// <summary>
        /// 检查当前输入是否满足初始化模拟寄存器的条件
        /// </summary>
        /// <returns>是否允许初始化</returns>
        private bool CheckCanStartRead()
        {
            if (string.IsNullOrEmpty(cmb_Area.Text))
            {
                return false;
            }
            if (!Enum.TryParse(cmb_Area.Text, out MemoryArea Area))
            {
                return false;
            }
            if (!OtherTool.StringTryParseInt(tb_StartAddress.Text, out var StartAddress,0))
            {
                return false;
            }
            if (!OtherTool.StringTryParseInt(tb_ReadLength.Text, out var ReadLength,1))
            {
                return false;
            }
            CurrentReadArea = Area;
            CurrentReadAddress = StartAddress;
            CurrentReadLength = ReadLength;
            return true;
        }
    }
}
