using PlcTest.Models;
using PlcTest.View.Support;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PlcTest.View.Controls
{
    /// <summary>
    /// 监视列表：一行一个地址，按固定周期轮询，值与状态就地刷新。
    /// <para>
    /// 为什么不再用「区域 + 起始 + 长度 + 21 列固定表」：那种表只能看一段连续区域，
    /// 且列是按 16 位字画的，位地址与字地址混不进同一屏。联调时真正想看的是
    /// 「M100 通没通、D100 现在多少、DB1 有没有超时」——不同区域、不同位/字，一张表解决。
    /// </para>
    /// <para>
    /// 读取在主线程之外执行：单次读写超时 1 秒，同步调用会把界面卡住；
    /// 但写回控件必须在 UI 线程，所以是「后台读 → BeginInvoke 写」。
    /// <see cref="PollOnce"/> 是同步版本，只给测试与「立即刷新」按钮用。
    /// </para>
    /// </summary>
    public partial class MonitorGrid : UserControl
    {
        private const int ColAddress = 0;
        private const int ColKind = 1;
        private const int ColLength = 2;
        private const int ColValue = 3;
        private const int ColStatus = 4;

        /// <summary>正在读（防止上一次没读完又发起一轮）</summary>
        private bool _polling;

        /// <summary>下次点「添加」时的建议地址（由宿主的软元件列表决定）</summary>
        private string _lastSuggested = "M0";

        /// <summary>建议地址对应的位/字方式</summary>
        private AddressValueKind _suggestedKind = AddressValueKind.Bit;

        /// <summary>地址解析与读取的实现，由宿主窗体提供（客户端或服务端）</summary>
        public Func<PlcAddress, MonitorReadResult> Reader { get; set; }

        /// <summary>当前协议能力，用于解析地址文本</summary>
        public ProtocolCapability Capability { get; set; }

        /// <summary>轮询周期（毫秒）</summary>
        public int Period
        {
            get
            {
                int period;
                return int.TryParse(_tbPeriod.Text, out period) && period >= 50 ? period : 500;
            }
        }

        /// <summary>是否正在自动刷新</summary>
        public bool IsAutoRefresh
        {
            get { return _timer.Enabled; }
        }

        /// <summary>
        /// 构造：控件树（工具栏、表格、五个列、轮询定时器）都在 Designer 里，
        /// 这里只绑事件与默认值。
        /// </summary>
        public MonitorGrid()
        {
            InitializeComponent();

            Capability = ProtocolCapability.Empty;

            _btnAdd.Click += (s, e) => AddRow(_lastSuggested, _suggestedKind, 1);
            _btnRemove.Click += (s, e) => RemoveSelectedRows();
            _btnRefresh.Click += (s, e) => PollAsync();
            _btnPause.Click += (s, e) => ToggleAutoRefresh();

            _tbPeriod.TextChanged += (s, e) => _timer.Interval = Period;

            _grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex != ColValue && e.ColumnIndex != ColStatus)
                {
                    ValidateRow(e.RowIndex);
                }
            };

            _timer.Tick += (s, e) => PollAsync();
        }

        /// <summary>
        /// 建议一个地址，之后点「添加」就用它（例如左侧列表选中 M 区时传 "M0"）。
        /// </summary>
        public void SuggestAddress(string addressText, AddressValueKind kind)
        {
            _lastSuggested = string.IsNullOrEmpty(addressText) ? _lastSuggested : addressText;
            _suggestedKind = kind;
        }

        /// <summary>
        /// 追加一行。
        /// </summary>
        public void AddRow(string addressText, AddressValueKind kind, int length)
        {
            int index = _grid.Rows.Add(
                addressText ?? string.Empty,
                kind == AddressValueKind.Word ? "字" : "位",
                length < 1 ? 1 : length,
                string.Empty,
                string.Empty);

            SetStatus(index, string.IsNullOrEmpty(addressText) ? "待填地址" : "待刷新", false);
            _grid.CurrentCell = _grid.Rows[index].Cells[ColAddress];
        }

        /// <summary>
        /// 删除选中的行（从后往前删，避免索引漂移）。
        /// </summary>
        public void RemoveSelectedRows()
        {
            var indexes = new List<int>();
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                indexes.Add(row.Index);
            }
            indexes.Sort();
            for (int i = indexes.Count - 1; i >= 0; i--)
            {
                _grid.Rows.RemoveAt(indexes[i]);
            }
        }

        /// <summary>当前行数（测试与宿主统计用）</summary>
        public int RowCount
        {
            get { return _grid.Rows.Count; }
        }

        /// <summary>读某一行的单元格文本（测试与宿主用）</summary>
        public string GetCellText(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count
                || columnIndex < 0 || columnIndex >= _grid.Columns.Count)
            {
                return null;
            }
            return Convert.ToString(_grid.Rows[rowIndex].Cells[columnIndex].Value);
        }

        /// <summary>地址列</summary>
        public const int AddressColumn = ColAddress;
        /// <summary>方式列</summary>
        public const int KindColumn = ColKind;
        /// <summary>长度列</summary>
        public const int LengthColumn = ColLength;
        /// <summary>值列</summary>
        public const int ValueColumn = ColValue;
        /// <summary>状态列</summary>
        public const int StatusColumn = ColStatus;

        /// <summary>开启 / 停止自动刷新</summary>
        public void ToggleAutoRefresh()
        {
            _timer.Enabled = !_timer.Enabled;
            _btnPause.Text = _timer.Enabled ? "暂停" : "开始";
        }

        /// <summary>
        /// 同步刷新一轮：在调用线程上直接读完并写回。测试和「立即刷新」用，
        /// 自动刷新走 <see cref="PollAsync"/>，不阻塞界面。
        /// </summary>
        public void PollOnce()
        {
            if (IsDisposed || Reader == null)
            {
                return;
            }

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                ApplyOne(i, ReadRow(i));
            }
        }

        /// <summary>
        /// 后台读一轮，回 UI 线程写。上一轮没结束时直接跳过，避免超时叠加把线程池占满。
        /// </summary>
        public void PollAsync()
        {
            if (IsDisposed || Reader == null || _polling)
            {
                return;
            }

            // 先在 UI 线程把「这一轮要读什么」定下来，后台线程只做解析 + 读 + 取值
            var jobs = new List<KeyValuePair<int, PlcAddress>>();
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                PlcAddress address;
                string error;
                if (TryResolveRow(i, out address, out error))
                {
                    jobs.Add(new KeyValuePair<int, PlcAddress>(i, address));
                }
                else if (error != null)
                {
                    SetStatus(i, error, true);
                }
            }

            if (jobs.Count == 0)
            {
                return;
            }

            _polling = true;
            Task.Run(() =>
            {
                var results = new List<KeyValuePair<int, MonitorReadResult>>(jobs.Count);
                foreach (var job in jobs)
                {
                    results.Add(new KeyValuePair<int, MonitorReadResult>(job.Key, Read(job.Value)));
                }

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        foreach (var item in results)
                        {
                            ApplyOne(item.Key, item.Value);
                        }
                        _polling = false;
                    }));
                }
                catch (ObjectDisposedException) { _polling = false; }
                catch (InvalidOperationException) { _polling = false; }
            });
        }

        /// <summary>
        /// 读一行（同步），供 <see cref="PollOnce"/> 使用。
        /// </summary>
        private MonitorReadResult ReadRow(int rowIndex)
        {
            PlcAddress address;
            string error;
            if (!TryResolveRow(rowIndex, out address, out error))
            {
                return MonitorReadResult.Fail(error);
            }
            return Read(address);
        }

        private MonitorReadResult Read(PlcAddress address)
        {
            try
            {
                Func<PlcAddress, MonitorReadResult> reader = Reader;
                return reader == null ? MonitorReadResult.Fail("未连接") : reader(address);
            }
            catch (Exception ex)
            {
                return MonitorReadResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 把一行的文本解析成地址；解析失败返回 false 并带出原因。
        /// <paramref name="error"/> 为 null 表示这一行是空的（不算错，跳过）。
        /// </summary>
        private bool TryResolveRow(int rowIndex, out PlcAddress address, out string error)
        {
            address = null;
            error = null;

            DataGridViewRow row = _grid.Rows[rowIndex];
            string text = Convert.ToString(row.Cells[ColAddress].Value);
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                return false;
            }

            AddressValueKind kind = Convert.ToString(row.Cells[ColKind].Value) == "字"
                ? AddressValueKind.Word
                : AddressValueKind.Bit;

            int length;
            if (!int.TryParse(Convert.ToString(row.Cells[ColLength].Value), out length) || length < 1)
            {
                length = 1;
            }

            Result<PlcAddress> parsed = AddressTextParser.Parse(text, Capability, kind);
            if (!parsed.IsSuccess)
            {
                error = parsed.ErrorMsg;
                return false;
            }

            parsed.Data.Length = length;
            address = parsed.Data;
            return true;
        }

        /// <summary>
        /// 把一次读取结果写回表格。
        /// </summary>
        private void ApplyOne(int rowIndex, MonitorReadResult result)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            if (result == null)
            {
                SetStatus(rowIndex, "无结果", true);
                return;
            }

            if (!result.Success)
            {
                SetStatus(rowIndex, result.Error, true);
                return;
            }

            _grid.Rows[rowIndex].Cells[ColValue].Value = Describe(result);
            SetStatus(rowIndex, "OK", false);
        }

        /// <summary>
        /// 值的显示：位用 0/1 串、字用十进制；超出 16 个只显示前 16 个，避免撑爆列宽。
        /// </summary>
        private static string Describe(MonitorReadResult result)
        {
            const int limit = 16;

            if (result.Bits != null)
            {
                int count = Math.Min(result.Bits.Length, limit);
                var parts = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    parts.Add(result.Bits[i] ? "1" : "0");
                }
                string text = string.Join(" ", parts);
                return result.Bits.Length > limit ? text + " …" : text;
            }

            if (result.Words != null)
            {
                int count = Math.Min(result.Words.Length, limit);
                var parts = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    parts.Add(result.Words[i].ToString());
                }
                string text = string.Join(", ", parts);
                return result.Words.Length > limit ? text + " …" : text;
            }

            return string.Empty;
        }

        /// <summary>
        /// 地址/方式/长度被改写后立刻校验一次，把错误提前暴露在「状态」列，不必等到下一轮。
        /// </summary>
        private void ValidateRow(int rowIndex)
        {
            PlcAddress address;
            string error;
            if (TryResolveRow(rowIndex, out address, out error))
            {
                SetStatus(rowIndex, "待刷新", false);
            }
            else
            {
                SetStatus(rowIndex, error ?? "待填地址", error != null);
            }
        }

        private void SetStatus(int rowIndex, string text, bool isError)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            DataGridViewCell cell = _grid.Rows[rowIndex].Cells[ColStatus];
            cell.Value = text;
            cell.Style.ForeColor = isError ? Color.FromArgb(163, 45, 45) : Color.FromArgb(15, 110, 86);
        }
        /// <summary>释放时停掉定时器，避免窗体关了还在轮询</summary>
    }
}
