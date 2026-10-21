using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using PlcTest.Tool;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PlcTest.View.Forms
{
    public partial class PlcClientForm : Form
    {
        /// <summary>
        /// PLC实例
        /// </summary>
        IPlcClient PLCControl = null;
        /// <summary>
        /// 当前读取寄存器区域
        /// </summary>
        MemoryArea CurrentReadArea = MemoryArea.D;
        /// <summary>
        /// 当前读取寄存器起始地址
        /// </summary>
        int CurrentReadAddress = 0;
        /// <summary>
        /// 当前读取寄存器长度
        /// </summary>
        int CurrentReadLength = 0;
        /// <summary>
        /// 是否在工作
        /// </summary>
        bool IsWorking = false;
        /// <summary>
        /// 读取到的字节数组
        /// /// </summary>
        short[] CurrentReadshort = new short[0];
        /// <summary>
        /// 读取到的位数组
        /// </summary>
        bool[] CurrentReadBit = new bool[0];
       /// <summary>
       /// 线程工作令牌
       /// </summary>
        private CancellationTokenSource _readCts;
        /// <summary>
        /// 工作线程
        /// </summary>
        private Task _readTask;
        /// <summary>
        /// 上一次读取失败的信息，用于抑制每秒重复刷屏
        /// </summary>
        private string _lastReadError;
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
        /// <param name="plcControl"></param>
        public PlcClientForm(IPlcClient plcControl)
        {
            InitializeComponent();
            this.PLCControl = plcControl;
            PLCControl.ShowCommMessageEvent += AddInfo;
        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            InitCombox();
        }
        /// <summary>
        /// 加载下拉控件
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

        #region Button
        /// <summary>
        /// 开始读取
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_StartRead_Click(object sender, EventArgs e)
        {
            if (CheckCanStartRead())
            {
                // 若已有未完成任务，先尝试取消并等待
                if (_readTask != null && !_readTask.IsCompleted)
                {
                    try
                    {
                        _readCts?.Cancel();
                        _readTask?.Wait(500);
                    }
                    catch { }
                    finally
                    {
                        _readTask = null;
                        _readCts?.Dispose();
                        _readCts = null;
                    }
                }

                IsWorking = true;
                _readCts = new CancellationTokenSource();
                _readTask = Task.Run(() => ReadDataThread(_readCts.Token), _readCts.Token);
            }
            else
            {
                MessageBox.Show("Please Check Address Information ");
            }
        }
        /// <summary>
        /// 停止
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_EndRead_Click(object sender, EventArgs e)
        {
            IsWorking = false;
        }
        /// <summary>
        /// 写入单个位数据到 PLC。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
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
            if (!OtherTool.StringTryParseInt(tb_WriteBitAddress.Text, out var WriteAddress,0))
            {
                return;
            }
            if (!short.TryParse(tb_WriteBitValue.Text, out var WriteValue))
            {
                return;
            }

            if (PLCControl != null && PLCControl.IsConnected)
            {
                var writeresult=PLCControl.WriteBit(PlcAddress.Bit(WriteArea, WriteAddress), WriteValue == 1);
                if (!writeresult.IsSuccess)
                {
                    MessageBox.Show("Write Error Reson :" + writeresult.ErrorMsg);
                }
            }
            else
            {
                MessageBox.Show("Please Check Connection Status ");
            }
        }
        /// <summary>
        /// 写入单个字数据到 PLC
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
            if (!OtherTool.StringTryParseInt(tb_WriteWordAddress.Text, out var WriteAddress, 0))
            {
                return;
            }
            if (!short.TryParse(tb_WriteWordValue.Text, out var WriteValue))
            {
                return;
            }

            if (PLCControl != null && PLCControl.IsConnected)
            {
                var writeresult= PLCControl.WriteWord(PlcAddress.Word(WriteArea, WriteAddress), WriteValue);
                if (!writeresult.IsSuccess)
                {
                    MessageBox.Show("Write Error Reson :"+writeresult .ErrorMsg);
                }
            }
            else
            {
                MessageBox.Show("Please Check Connection Status ");
            }
        }
    
        /// <summary>
        /// 用默认值覆盖指定长度的寄存器数据
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void btn_ClearLength_Click(object sender, EventArgs e)
        {
            if (PLCControl != null && PLCControl.IsConnected)
            {
                if (!OtherTool.StringTryParseInt(tb_ClearLength.Text, out var ClearLength,1))
                {
                    MessageBox.Show("Please Check ClearLength ");
                }
                else
                {
                    if (CurrentReadArea == MemoryArea.D)
                    {
                        short[] clearData = new short[ClearLength];
                        var res = PLCControl.WriteWordArray(PlcAddress.Word(CurrentReadArea, CurrentReadAddress, null, ClearLength), clearData);
                    }
                    else
                    {
                        bool[] clearData = new bool[ClearLength];
                        var res = PLCControl.WriteBitArray(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress, null, ClearLength), clearData);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please Check Connection Status ");
            }
        }
        /// <summary>
        /// 用默认值覆盖当前区域的全部寄存器数据。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void btn_ClearAll_Click(object sender, EventArgs e)
        {
            if (PLCControl != null && PLCControl.IsConnected)
            {
                if (CurrentReadArea == MemoryArea.D)
                {
                    short[] clearData = new short[CurrentReadLength];
                    PLCControl.WriteWordArray(PlcAddress.Word(CurrentReadArea, CurrentReadAddress, null, CurrentReadLength), clearData);
                }
                else
                {
                    bool[] clearData = new bool[CurrentReadLength];
                    PLCControl.WriteBitArray(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress, null, CurrentReadLength), clearData);
                }
            }
            else
            {
                MessageBox.Show("Please Check Connection Status ");
            }
        }
        #endregion

        #region UIChangge
        /// <summary>
        /// 定时刷新界面状态和数据表格
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                RefreshUI();
            }
            catch
            {
                // 界面刷新异常不允许穿透到 UI 线程导致程序闪退，下一轮定时器继续尝试
            }
        }
        /// <summary>
        /// 刷新按钮状态和数据表格
        /// </summary>
        private void RefreshUI()
        {
            RefreshBtn();
            RefreshDgv();
        }
        /// <summary>
        /// 根据连接状态和工作状态更新按钮可用性
        /// </summary>
        private void RefreshBtn()
        {
            if (PLCControl != null && PLCControl.IsConnected)
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

            if (IsWorking)
            {
                btn_StartRead.Enabled = false;
                btn_EndRead.Enabled = true;
            }
            else
            {
                btn_StartRead.Enabled = true;
                btn_EndRead.Enabled = false;
            }
        }
        /// <summary>
        /// 刷新数据表格的行数和显示内容
        /// </summary>
        private void RefreshDgv()
        {
            EnsureDataGridViewRowCount(CurrentReadLength);
        }

        /// <summary>
        /// 安全调整数据表格行数，并同步当前寄存器值到表格
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
                    if (CurrentReadshort.Length > 0)
                    {
                        int dRows = Math.Min(DGV.Rows.Count, CurrentReadshort.Length);
                        for (int i = 0; i < dRows; i++)
                        {
                            var bits = ConverterTool.ShortToBoolArray(CurrentReadshort[i]);
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
                            DGV.Rows[i].Cells[17].Value = ConverterTool.GetHighByteOfShort(CurrentReadshort[i]);
                            DGV.Rows[i].Cells[18].Value = ConverterTool.GetLowByteOfShort(CurrentReadshort[i]);
                            DGV.Rows[i].Cells[19].Value = CurrentReadshort[i];
                            DGV.Rows[i].Cells[20].Value = ConverterTool.ShortToAscii(CurrentReadshort[i]);
                        }
                    }
                }
                else
                {
                    if (CurrentReadBit.Length > 0)
                    {
                        int bitRows = Math.Min(DGV.Rows.Count, CurrentReadBit.Length);
                        for (int i = 0; i < bitRows; i++)
                        {
                            DGV.Rows[i].Cells[16].Value = CurrentReadBit[i] ? 1 : 0;
                            if (CurrentReadBit[i])
                            {
                                DGV.Rows[i].Cells[16].Style.BackColor = Color.LightBlue;
                            }
                            else
                            {
                                DGV.Rows[i].Cells[16].Style.BackColor = Color.Empty;
                            }
                        }
                    }
                }
                ///放到最后，如果没有工作，就清空表格
                if (!IsWorking||PLCControl == null || PLCControl.IsConnected == false)
                {
                    DGV.Rows.Clear();
                }
            }
            finally
            {
                DGV.ResumeLayout();
            }
        }
        #endregion

        #region Work
        /// <summary>
        /// 后台读取循环，按固定周期读取 PLC 数据并更新缓存。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        private async Task ReadDataThread(CancellationToken token)
        {
            while (IsWorking && !token.IsCancellationRequested)
            {
                try
                {
                    if (PLCControl != null && PLCControl.IsConnected)
                    {
                        if (CurrentReadArea== MemoryArea.D)
                        {
                            var readresult = PLCControl.ReadWordArray(PlcAddress.Word(CurrentReadArea, CurrentReadAddress, null, CurrentReadLength));
                            if (readresult.IsSuccess)
                            {
                                CurrentReadshort = readresult.Data;
                                ReportReadError(null);
                            }
                            else
                            {
                                ReportReadError("读取失败: " + readresult.ErrorMsg);
                            }
                        }
                        else
                        {
                            var readresult = PLCControl.ReadBitArray(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress, null, CurrentReadLength));
                            if (readresult.IsSuccess)
                            {
                                CurrentReadBit = readresult.Data;
                                ReportReadError(null);
                            }
                            else
                            {
                                ReportReadError("读取失败: " + readresult.ErrorMsg);
                            }
                        }
                    }
                    // 使用 WaitHandle 或 Task.Delay 支持取消
                    try
                    {
                        Task.Delay(1000, token).Wait(token);
                    }
                    catch (OperationCanceledException) { break; }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 通讯异常不再静默丢弃，否则断线时界面毫无反应
                    ReportReadError("读取异常: " + ex.Message);
                }
            }
        }
        #endregion

        #region CheckHelp
        /// <summary>
        /// 检查当前输入是否满足读取条件，并同步当前读取参数
        /// </summary>
        /// <returns>是否允许开始读取</returns>
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
            if (!OtherTool .StringTryParseInt(tb_StartAddress.Text, out var StartAddress,0))
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
        #endregion
        /// <summary>
        /// 处理数据表格单元格双击，根据选中位反向写回 PLC
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
                if (rowindex < 0)
                {
                    return;
                }
                if (CurrentReadArea== MemoryArea.D)
                {
                    if (columnindex <= 0 || columnindex > 16)
                    {
                        return;
                    }
                    //1.拿到当前选中单元格的值
                    if (!short.TryParse(DGV.Rows[rowindex].Cells[19].Value.ToString(), out var CurrentValue))
                    {
                        return;
                    }

                    //2.根据当前的值和列数，计算出要写入PLC的值
                    int bitPosition = 16 - columnindex; // 列1对应bit15，列16对应bit0  
                    short writeValue = (short)ConverterTool.ToggleBit(CurrentValue, bitPosition);
                    var res = PLCControl.WriteWord(PlcAddress.Word(CurrentReadArea, CurrentReadAddress + rowindex), writeValue);
                    if (!res.IsSuccess)
                    {
                        MessageBox.Show("Write Error ");
                    }
                }
                else
                {
                    if (columnindex != 16)
                    {
                        return;
                    }
                    if (!short.TryParse(DGV.Rows[rowindex].Cells[16].Value.ToString(), out var CurrentValue))
                    {
                        return;
                    }
                    var res = PLCControl.WriteBit(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress + rowindex), CurrentValue==0);
                    if (!res.IsSuccess)
                    {
                        MessageBox.Show("Write Error ");
                    }
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
        /// 窗体关闭
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 停止 UI 定时器，避免关闭后 UI 回调
            try
            {
                if (timer1 != null && timer1.Enabled)
                {
                    timer1.Stop();
                }
            }
            catch { }
            // 请求停止读取
            IsWorking = false;
            try
            {
                _readCts?.Cancel();
            }
            catch { }
            // 等待任务结束（短超时）
            try
            {
                _readTask?.Wait(2000);
            }
            catch { }
            // 清理本地 CTS/Task 引用（不 Dispose plcControl，因为它由外部管理）
            try
            {
                _readCts?.Dispose();
            }
            catch { }
            _readCts = null;
            _readTask = null;
            base.OnFormClosing(e);
        }
        /// <summary>
        /// 上报读取失败信息：同一条错误只写一次日志，避免每秒刷屏
        /// </summary>
        /// <param name="message">错误信息，传 null 表示读取已恢复</param>
        private void ReportReadError(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                _lastReadError = null;
                return;
            }
            if (_lastReadError == message)
            {
                return;
            }
            _lastReadError = message;
            AddInfo(message);
        }
        /// <summary>
        /// 增加日志打印到文本框
        /// </summary>
        /// <param name="info"></param>
        private void AddInfo(string info)
        {
            if (tb_Info.InvokeRequired)
            {
                tb_Info.Invoke(new Action<string>(AddInfo), info);
            }
            else
            {
                tb_Info.AppendText(info + "\r\n");
            }
        }
    }
}





