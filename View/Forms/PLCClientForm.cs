using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using PlcTest.Tool;
using System;
using System.Collections.Generic;
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
        /// 是否在工作（后台读取线程会读它，UI 线程会写它，必须 volatile）
        /// </summary>
        volatile bool IsWorking = false;
        /// <summary>
        /// 日志框最多保留的行数，超出后从头部裁掉，避免长时间轮询把界面拖死
        /// </summary>
        private const int MaxLogLines = 500;
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
            // 区域列表来自实际连接的协议，PLC 上不存在的软元件不会出现在选择框里。
            // 例如汇川 Modbus 只有 M/SM/B/S/X/Y/TS/CS/D/SD/R/TN/CN，且随机型变化；
            // 三菱 3E 有 28 种，1E 只有 10 种。
            IReadOnlyList<MemoryArea> areas;
            if (PLCControl == null)
            {
                areas = new List<MemoryArea>();
            }
            else
            {
                areas = PLCControl.GetSupportedAreas();
            }

            FillAreaCombo(cmb_Area, areas);
            FillAreaCombo(cmb_WriteBitArea, FilterAreas(areas, AreaAccess.Bit));
            FillAreaCombo(cmb_WriteWordArea, FilterAreas(areas, AreaAccess.Word));

            // 让 CurrentReadArea 与下拉框的首选项保持一致，避免首次刷表格时区名对不上
            MemoryArea initialArea;
            if (cmb_Area.SelectedItem != null
                && Enum.TryParse(cmb_Area.SelectedItem.ToString(), out initialArea))
            {
                CurrentReadArea = initialArea;
            }
        }

        /// <summary>
        /// 填充区域下拉框，尽量默认选中 D（最常用），没有 D 时选第一项。
        /// </summary>
        private static void FillAreaCombo(ComboBox combo, IReadOnlyList<MemoryArea> areas)
        {
            combo.Items.Clear();
            foreach (MemoryArea area in areas)
            {
                combo.Items.Add(area.ToString());
            }

            if (combo.Items.Count == 0)
            {
                return;
            }

            int preferred = combo.Items.IndexOf(MemoryArea.D.ToString());
            combo.SelectedIndex = preferred >= 0 ? preferred : 0;
        }

        /// <summary>
        /// 按访问方式筛选区域（写位只看支持位的，写字只看支持字的）。
        /// </summary>
        private IReadOnlyList<MemoryArea> FilterAreas(IReadOnlyList<MemoryArea> areas, AreaAccess needed)
        {
            var result = new List<MemoryArea>();
            foreach (MemoryArea area in areas)
            {
                if ((PLCControl.GetAreaAccess(area) & needed) != 0)
                {
                    result.Add(area);
                }
            }
            return result;
        }

        /// <summary>
        /// 该区域当前是否按位处理。由协议声明的访问方式决定，不再硬编码"是不是 D"。
        /// 既能按位又能按字的区域（如三菱 M）按位处理，与界面原有的位表格展示方式一致。
        /// </summary>
        private bool IsBitArea(MemoryArea area)
        {
            if (PLCControl == null)
            {
                return area != MemoryArea.D;
            }
            return (PLCControl.GetAreaAccess(area) & AreaAccess.Bit) != 0;
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
                // 后台线程写，避免同步收发（超时 1 s）卡住界面
                WriteBitDataAsync(WriteArea, WriteAddress, WriteValue == 1);
            }
            else
            {
                MessageBox.Show("Please Check Connection Status ");
            }
        }
        /// <summary>
        /// 后台执行位写入
        /// </summary>
        private async void WriteBitDataAsync(MemoryArea area, int address, bool value)
        {
            Result<bool> writeresult = await Task.Run(() => PLCControl.WriteBit(PlcAddress.Bit(area, address), value));
            if (!writeresult.IsSuccess)
            {
                MessageBox.Show("Write Error Reson :" + writeresult.ErrorMsg);
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
                // 后台线程写，避免同步收发（超时 1 s）卡住界面
                WriteWordDataAsync(WriteArea, WriteAddress, WriteValue);
            }
            else
            {
                MessageBox.Show("Please Check Connection Status ");
            }
        }
        /// <summary>
        /// 后台执行字写入
        /// </summary>
        private async void WriteWordDataAsync(MemoryArea area, int address, short value)
        {
            Result<bool> writeresult = await Task.Run(() => PLCControl.WriteWord(PlcAddress.Word(area, address), value));
            if (!writeresult.IsSuccess)
            {
                MessageBox.Show("Write Error Reson :" + writeresult.ErrorMsg);
            }
        }
    
        /// <summary>
        /// 用默认值覆盖指定长度的寄存器数据
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        private void btn_ClearLength_Click(object sender, EventArgs e)
        {
            if (PLCControl == null || !PLCControl.IsConnected)
            {
                MessageBox.Show("Please Check Connection Status ");
                return;
            }
            if (!OtherTool.StringTryParseInt(tb_ClearLength.Text, out var ClearLength,1))
            {
                MessageBox.Show("Please Check ClearLength ");
                return;
            }
            // 后台线程写，避免同步收发（超时 1 s）卡住界面
            ClearRangeAsync(CurrentReadArea, CurrentReadAddress, ClearLength);
        }
        /// <summary>
        /// 用默认值覆盖当前区域的全部寄存器数据。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void btn_ClearAll_Click(object sender, EventArgs e)
        {
            if (PLCControl == null || !PLCControl.IsConnected)
            {
                MessageBox.Show("Please Check Connection Status ");
                return;
            }
            if (CurrentReadLength <= 0)
            {
                MessageBox.Show("Please Check Address Information ");
                return;
            }
            // 后台线程写，避免同步收发（超时 1 s）卡住界面
            ClearRangeAsync(CurrentReadArea, CurrentReadAddress, CurrentReadLength);
        }
        /// <summary>
        /// 后台清空指定区间的寄存器，失败时提示原因（原来这里连返回值都没看）
        /// </summary>
        private async void ClearRangeAsync(MemoryArea area, int startAddress, int length)
        {
            Result<bool> res = await Task.Run(() =>
            {
                if (!IsBitArea(area))
                {
                    return PLCControl.WriteWordArray(PlcAddress.Word(area, startAddress, null, length), new short[length]);
                }
                return PLCControl.WriteBitArray(PlcAddress.Bit(area, startAddress, null, length), new bool[length]);
            });

            if (!res.IsSuccess)
            {
                MessageBox.Show("Clear Error Reson :" + res.ErrorMsg);
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
                if (!IsBitArea(CurrentReadArea))
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
                        if (!IsBitArea(CurrentReadArea))
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
                    // 使用 Task.Delay 支持取消（原来是 .Wait(token)，在 async 方法里同步阻塞）
                    try
                    {
                        await Task.Delay(1000, token).ConfigureAwait(true);
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
        private async void DGV_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
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
                if (!IsBitArea(CurrentReadArea))
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
                    // 后台写，避免卡界面
                    var res = await Task.Run(() => PLCControl.WriteWord(PlcAddress.Word(CurrentReadArea, CurrentReadAddress + rowindex), writeValue));
                    if (!res.IsSuccess)
                    {
                        MessageBox.Show("Write Error :" + res.ErrorMsg);
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
                    var res = await Task.Run(() => PLCControl.WriteBit(PlcAddress.Bit(CurrentReadArea, CurrentReadAddress + rowindex), CurrentValue == 0));
                    if (!res.IsSuccess)
                    {
                        MessageBox.Show("Write Error :" + res.ErrorMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                // 双击反写失败不能静默，否则用户以为写进去了
                AddInfo("双击写入异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 窗体关闭
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 退订通讯日志事件：否则窗体释放后，协议线程仍会回调本窗体，抛出的异常会被
            // 通讯层 catch 成"通讯异常"，让后续读写莫名失败
            try
            {
                if (PLCControl != null)
                {
                    PLCControl.ShowCommMessageEvent -= AddInfo;
                }
            }
            catch { }

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
            // 等待任务结束（短超时）。AddInfo 已改为 BeginInvoke，不会再与 UI 线程互等
            try
            {
                _readTask?.Wait(1000);
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
        /// 增加日志打印到文本框。
        /// 协议线程/读取线程都会调到这里，用 BeginInvoke 投递（Invoke 会阻塞等待 UI，
        /// 而 UI 线程关窗时可能在等读取线程，两者互等会卡住）。
        /// </summary>
        /// <param name="info"></param>
        private void AddInfo(string info)
        {
            if (tb_Info == null || tb_Info.IsDisposed)
            {
                return;
            }
            if (tb_Info.InvokeRequired)
            {
                try
                {
                    tb_Info.BeginInvoke(new Action<string>(AddInfo), info);
                }
                catch (ObjectDisposedException)
                {
                    // 控件已销毁，丢弃这条日志即可
                }
                catch (InvalidOperationException)
                {
                    // 窗体句柄已销毁，丢弃这条日志即可
                }
            }
            else
            {
                tb_Info.AppendText(info + "\r\n");
                TrimLogIfTooLong();
            }
        }
        /// <summary>
        /// 日志超过上限后从头部裁掉，避免长时间轮询把文本框撑爆
        /// </summary>
        private void TrimLogIfTooLong()
        {
            try
            {
                if (tb_Info.Lines.Length <= MaxLogLines)
                {
                    return;
                }

                string[] lines = tb_Info.Lines;
                string[] kept = new string[MaxLogLines];
                Array.Copy(lines, lines.Length - MaxLogLines, kept, 0, MaxLogLines);
                tb_Info.Lines = kept;
                tb_Info.SelectionStart = tb_Info.TextLength;
                tb_Info.ScrollToCaret();
            }
            catch
            {
                // 裁剪失败不影响日志展示
            }
        }
    }
}





