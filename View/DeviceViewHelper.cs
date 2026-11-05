using PlcTest.Models;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PlcTest.View
{
    /// <summary>
    /// 「软元件视图」表格的统一初始化与填充，客户端窗体与服务端窗体共用一套列定义，
    /// 避免两边列顺序、符号写法不一致。
    /// </summary>
    internal static class DeviceViewHelper
    {
        /// <summary>支持用 √ 表示，不支持用 — 表示</summary>
        private const string Yes = "√";
        private const string No = "—";

        /// <summary>
        /// 建列并设置只读样式。列宽按比例分配：描述与备注占大头。
        /// </summary>
        public static void Initialize(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.Columns.Clear();
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            AddColumn(grid, "Column_DevArea", "区域", 12F);
            AddColumn(grid, "Column_DevName", "描述", 30F);
            AddColumn(grid, "Column_DevBit", "位", 8F);
            AddColumn(grid, "Column_DevWord", "字", 8F);
            AddColumn(grid, "Column_DevRemark", "备注", 42F);
        }

        private static void AddColumn(DataGridView grid, string name, string header, float weight)
        {
            var column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = weight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.ReadOnly = true;
            grid.Columns.Add(column);
        }

        /// <summary>
        /// 按行填充。备注暂时留空字符串（字段已就位，后续要写范围/机型差异直接在这填）。
        /// </summary>
        public static void Fill(DataGridView grid, IReadOnlyList<DeviceAreaInfo> infos)
        {
            if (grid == null)
            {
                return;
            }

            grid.Rows.Clear();
            if (infos == null)
            {
                return;
            }

            foreach (DeviceAreaInfo info in infos)
            {
                grid.Rows.Add(
                    info.Area.ToString(),
                    info.Name,
                    info.SupportsBit ? Yes : No,
                    info.SupportsWord ? Yes : No,
                    info.Remark ?? string.Empty);
            }
        }
    }
}
