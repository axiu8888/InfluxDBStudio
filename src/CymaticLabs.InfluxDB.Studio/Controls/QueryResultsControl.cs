using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Renders the results for a single InfluxDB query.
    /// </summary>
    public partial class QueryResultsControl : UserControl
    {
        #region Fields

        int resultsCount = 0;
        InfluxDbSeries lastResult;
        bool allowLineProtocolExport = true;

        // Anchor for Shift+click column/row header ranges (Excel-like).
        int anchorColumnIndex = -1;
        int anchorRowIndex = -1;

        #endregion Fields

        #region Properties

        public InfluxDbClient InfluxDbClient { get; set; }
        public string Database { get; set; }

        /// <summary>
        /// Optional export file name stem (without extension). When empty, connection + database is used.
        /// </summary>
        public string ExportFileNameStem { get; set; }

        /// <summary>
        /// When false, Line Protocol export items are hidden (schema / SHOW results).
        /// </summary>
        public bool AllowLineProtocolExport
        {
            get { return allowLineProtocolExport; }
            set
            {
                allowLineProtocolExport = value;
                if (exportAllLineProtocolToolStripMenuItem != null)
                    exportAllLineProtocolToolStripMenuItem.Visible = value;
                if (exportSelectedLineProtocolToolStripMenuItem != null)
                    exportSelectedLineProtocolToolStripMenuItem.Visible = value;
            }
        }

        public int RowCount
        {
            get { return dataGridView.Rows.Count; }
        }

        public int SelectedRowCount
        {
            get { return GetSelectedRowIndexes().Count; }
        }

        public object SelectedRowTag
        {
            get
            {
                var indexes = GetSelectedRowIndexes();
                if (indexes.Count == 0) return null;
                return dataGridView.Rows[indexes[0]].Tag;
            }
        }

        public event EventHandler SelectedRowChanged;
        public event EventHandler RowDoubleClick;

        public bool MultiSelect
        {
            get { return dataGridView.MultiSelect; }
            set { dataGridView.MultiSelect = value; }
        }

        #endregion Properties

        #region Constructors

        public QueryResultsControl()
        {
            InitializeComponent();
            Localization.Apply(this);

            dataGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            dataGridView.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;

            dataGridView.SelectionChanged += dataGridView_SelectionChanged;
            dataGridView.CellDoubleClick += dataGridView_CellDoubleClick;
        }

        #endregion Constructors

        #region Event Handlers

        private async void exportAllCsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToCsv();
        }

        private void exportAllExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportToExcel();
        }

        private void jSONToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportToJson();
        }

        private async void exportAllLineProtocolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToLineProtocol();
        }

        private void exportSelectedExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportToExcel(true);
        }

        private async void exportSelectedCsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToCsv(true);
        }

        private void jSONToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ExportToJson(true);
        }

        private async void exportSelectedLineProtocolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToLineProtocol(true);
        }

        void dataGridView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            var hit = dataGridView.HitTest(e.X, e.Y);
            if (hit.Type == DataGridViewHitTestType.Cell
                && hit.RowIndex >= 0 && hit.ColumnIndex >= 0
                && !dataGridView[hit.ColumnIndex, hit.RowIndex].Selected)
            {
                dataGridView.ClearSelection();
                dataGridView[hit.ColumnIndex, hit.RowIndex].Selected = true;
                dataGridView.CurrentCell = dataGridView[hit.ColumnIndex, hit.RowIndex];
            }
        }

        void dataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.Button != MouseButtons.Left) return;
            if (dataGridView.Rows.Count == 0) return;

            var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
            var shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (!ctrl && !shift)
                dataGridView.ClearSelection();

            int from = e.ColumnIndex;
            int to = e.ColumnIndex;
            if (shift && anchorColumnIndex >= 0)
            {
                from = Math.Min(anchorColumnIndex, e.ColumnIndex);
                to = Math.Max(anchorColumnIndex, e.ColumnIndex);
            }
            else if (!ctrl)
            {
                anchorColumnIndex = e.ColumnIndex;
            }

            if (!shift)
                anchorColumnIndex = e.ColumnIndex;

            for (var c = from; c <= to; c++)
                SelectEntireColumn(c, true);

            dataGridView.Focus();
        }

        void dataGridView_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.Button != MouseButtons.Left) return;

            var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
            var shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (!ctrl && !shift)
                dataGridView.ClearSelection();

            int from = e.RowIndex;
            int to = e.RowIndex;
            if (shift && anchorRowIndex >= 0)
            {
                from = Math.Min(anchorRowIndex, e.RowIndex);
                to = Math.Max(anchorRowIndex, e.RowIndex);
            }
            else if (!ctrl)
            {
                anchorRowIndex = e.RowIndex;
            }

            if (!shift)
                anchorRowIndex = e.RowIndex;

            for (var r = from; r <= to; r++)
                SelectEntireRow(r, true);

            dataGridView.Focus();
        }

        void dataGridView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                dataGridView.SelectAll();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopySelectionAsTsv();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        void contextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var hasCells = dataGridView.GetCellCount(DataGridViewElementStates.Selected) > 0;
            var hasRows = GetSelectedRowIndexes().Count > 0;
            var hasHeaderCols = GetSelectedDataColumnIndexes().Count > 0;
            var hasAnyHeader = dataGridView.Columns.Count > 1;

            copySelectionToolStripMenuItem.Enabled = hasCells;
            copyRowsToolStripMenuItem.Enabled = hasRows;
            copyHeaderToolStripMenuItem.Enabled = hasHeaderCols;
            copyAllHeadersToolStripMenuItem.Enabled = hasAnyHeader;
            exportSelectedToolStripMenuItem.Enabled = hasRows;
        }

        void copySelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelectionAsTsv();
        }

        void copyRowsJsonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelectedRowsAsJson();
        }

        void copyRowsKeyValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelectedRowsAsKeyValue();
        }

        void copyHeaderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelectedHeaders();
        }

        void copyAllHeadersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyAllHeaders();
        }

        void dataGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (SelectedRowChanged != null)
                SelectedRowChanged(this, EventArgs.Empty);
        }

        void dataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (RowDoubleClick != null)
                RowDoubleClick(this, EventArgs.Empty);
        }

        #endregion Event Handlers

        #region Methods

        /// <summary>
        /// Replaces an existing child control (typically a ListView) with a results grid.
        /// </summary>
        public static QueryResultsControl Replace(Control existing, bool allowLineProtocolExport = false)
        {
            if (existing == null) throw new ArgumentNullException("existing");
            var parent = existing.Parent;
            if (parent == null) throw new InvalidOperationException("Control has no parent.");

            var grid = new QueryResultsControl
            {
                Dock = existing.Dock,
                Anchor = existing.Anchor,
                AllowLineProtocolExport = allowLineProtocolExport
            };
            if (existing.Dock == DockStyle.None)
                grid.Bounds = existing.Bounds;

            var index = parent.Controls.GetChildIndex(existing);
            parent.Controls.Remove(existing);
            parent.Controls.Add(grid);
            parent.Controls.SetChildIndex(grid, index);
            return grid;
        }

        public void PrependContextItems(params ToolStripItem[] items)
        {
            if (items == null || items.Length == 0) return;
            contextMenuStrip.Items.Insert(0, new ToolStripSeparator());
            for (var i = items.Length - 1; i >= 0; i--)
            {
                if (items[i] != null)
                    contextMenuStrip.Items.Insert(0, items[i]);
            }
        }

        public void SelectFirstRowWhere(Predicate<object> match)
        {
            if (match == null) return;
            dataGridView.ClearSelection();
            for (var r = 0; r < dataGridView.Rows.Count; r++)
            {
                if (match(dataGridView.Rows[r].Tag))
                {
                    SelectEntireRow(r, true);
                    if (dataGridView.Rows[r].Cells.Count > 1)
                        dataGridView.CurrentCell = dataGridView[1, r];
                    break;
                }
            }
        }

        public void UpdateTable(string name, IList<string> columns, IList<IList<object>> values, IList<object> rowTags = null)
        {
            if (columns == null) throw new ArgumentNullException("columns");
            if (values == null) throw new ArgumentNullException("values");
            var series = new InfluxDbSeries(name ?? string.Empty, columns, new Dictionary<string, string>(), values);
            UpdateResults(series, true, rowTags);
        }

        public void ClearResults()
        {
            resultsCount = 0;
            tagsTextBox.Text = null;
            anchorColumnIndex = -1;
            anchorRowIndex = -1;
            dataGridView.Rows.Clear();
            dataGridView.Columns.Clear();
        }

        public int UpdateResults(InfluxDbSeries result, bool clear = false)
        {
            return UpdateResults(result, clear, null);
        }

        public int UpdateResults(InfluxDbSeries result, bool clear, IList<object> rowTags)
        {
            if (result == null) throw new ArgumentNullException("result");

            lastResult = result;
            if (clear) ClearResults();

            if (result.Tags != null && result.Tags.Count > 0)
            {
                splitContainer.Panel1Collapsed = false;
                var tagCount = result.Tags.Count;
                var tagCounter = 0;
                var sb = new StringBuilder();
                foreach (var tag in result.Tags)
                    sb.AppendFormat("{0} = {1}{2}", tag.Key, tag.Value, ++tagCounter < tagCount ? ", " : null);
                tagsTextBox.Text = sb.ToString();
            }
            else
            {
                splitContainer.Panel1Collapsed = true;
            }

            dataGridView.SuspendLayout();

            if (dataGridView.Columns.Count == 0)
            {
                var rowNum = CreateTextColumn("_rowNum", Localization.T("#"), 54);
                dataGridView.Columns.Add(rowNum);

                foreach (var c in result.Columns)
                    dataGridView.Columns.Add(CreateTextColumn(c, c, 96));
            }

            var columnWidth = Math.Max(96, (Width - 12) / Math.Max(1, dataGridView.Columns.Count));
            foreach (DataGridViewColumn col in dataGridView.Columns)
            {
                if (col.Name == "_rowNum") continue;
                col.Width = columnWidth;
            }

            for (var i = 0; i < result.Values.Count; i++)
            {
                var raw = result.Values[i];
                var values = new object[dataGridView.Columns.Count];
                values[0] = (++resultsCount).ToString(CultureInfo.InvariantCulture);

                for (var x = 0; x < raw.Count && x + 1 < values.Length; x++)
                    values[x + 1] = AppForm.Settings.FormatValue(raw[x]);

                var index = dataGridView.Rows.Add(values);
                if (rowTags != null && i < rowTags.Count)
                    dataGridView.Rows[index].Tag = rowTags[i];
                else
                    dataGridView.Rows[index].Tag = raw;
            }

            dataGridView.ResumeLayout();
            return resultsCount;
        }

        static DataGridViewTextBoxColumn CreateTextColumn(string name, string header, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.True
            };
        }

        void SelectEntireColumn(int columnIndex, bool selected)
        {
            if (columnIndex < 0 || columnIndex >= dataGridView.Columns.Count) return;
            for (var r = 0; r < dataGridView.Rows.Count; r++)
                dataGridView[columnIndex, r].Selected = selected;
        }

        void SelectEntireRow(int rowIndex, bool selected)
        {
            if (rowIndex < 0 || rowIndex >= dataGridView.Rows.Count) return;
            for (var c = 0; c < dataGridView.Columns.Count; c++)
                dataGridView[c, rowIndex].Selected = selected;
        }

        List<int> GetSelectedRowIndexes()
        {
            var set = new SortedSet<int>();
            foreach (DataGridViewCell cell in dataGridView.SelectedCells)
            {
                if (cell.RowIndex >= 0) set.Add(cell.RowIndex);
            }
            return new List<int>(set);
        }

        List<int> GetSelectedDataColumnIndexes()
        {
            var set = new SortedSet<int>();
            foreach (DataGridViewCell cell in dataGridView.SelectedCells)
            {
                if (cell.ColumnIndex > 0) set.Add(cell.ColumnIndex);
            }
            return new List<int>(set);
        }

        async Task ExportToCsv(bool onlySelected = false)
        {
            try
            {
                saveFileDialog.FileName = string.Format("{0}.csv", GetExportBaseName());
                saveFileDialog.Filter = Localization.T("CSV files|*.csv|All files|*.*");
                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

                var selectedRows = onlySelected ? new HashSet<int>(GetSelectedRowIndexes()) : null;

                using (var sw = new StreamWriter(saveFileDialog.FileName))
                {
                    var sb = new StringBuilder();
                    for (var i = 1; i < dataGridView.Columns.Count; i++)
                    {
                        if (i > 1) sb.Append(',');
                        sb.Append(dataGridView.Columns[i].HeaderText);
                    }
                    await sw.WriteLineAsync(sb.ToString());

                    for (var r = 0; r < dataGridView.Rows.Count; r++)
                    {
                        if (selectedRows != null && !selectedRows.Contains(r)) continue;
                        sb.Clear();
                        for (var c = 1; c < dataGridView.Columns.Count; c++)
                        {
                            if (c > 1) sb.Append(',');
                            var val = dataGridView[c, r].Value;
                            sb.Append(val == null ? string.Empty : val.ToString());
                        }
                        await sw.WriteLineAsync(sb.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        void ExportToExcel(bool onlySelected = false)
        {
            try
            {
                saveFileDialog.FileName = string.Format("{0}.xlsx", GetExportBaseName());
                saveFileDialog.Filter = Localization.T("Excel files|*.xlsx|All files|*.*");
                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

                var headers = new List<string>();
                for (var i = 1; i < dataGridView.Columns.Count; i++)
                    headers.Add(dataGridView.Columns[i].HeaderText);

                var selectedRows = onlySelected ? new HashSet<int>(GetSelectedRowIndexes()) : null;
                var rows = new List<IList<string>>();
                for (var r = 0; r < dataGridView.Rows.Count; r++)
                {
                    if (selectedRows != null && !selectedRows.Contains(r)) continue;
                    var row = new List<string>();
                    for (var c = 1; c < dataGridView.Columns.Count; c++)
                    {
                        var val = dataGridView[c, r].Value;
                        row.Add(val == null ? string.Empty : val.ToString());
                    }
                    rows.Add(row);
                }

                ExcelExport.WriteXlsx(saveFileDialog.FileName, Localization.T("Results"), headers, rows);
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        void ExportToJson(bool onlySelected = false)
        {
            try
            {
                saveFileDialog.FileName = string.Format("{0}.json", GetExportBaseName());
                saveFileDialog.Filter = Localization.T("JSON files|*.json|All files|*.*");
                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

                var array = BuildJsonFromRows(onlySelected ? GetSelectedRowIndexes() : null);
                File.WriteAllText(saveFileDialog.FileName, JsonConvert.SerializeObject(array, Formatting.Indented));
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        async Task ExportToLineProtocol(bool onlySelected = false)
        {
            try
            {
                if (lastResult == null) return;
                if (!allowLineProtocolExport) return;

                if (string.IsNullOrWhiteSpace(lastResult.Name))
                {
                    AppForm.DisplayError(Localization.T(
                        "Cannot export Line Protocol: the result has no measurement name. Use a SELECT query against a measurement."));
                    return;
                }

                saveFileDialog.FileName = string.Format("{0}.lp", GetExportBaseName());
                saveFileDialog.Filter = Localization.T("Line Protocol files|*.lp|All files|*.*");
                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

                ICollection<string> tagKeys = null;
                ICollection<string> fieldKeys = null;
                try
                {
                    if (InfluxDbClient != null && !string.IsNullOrWhiteSpace(Database))
                    {
                        var tags = await InfluxDbClient.GetTagKeysAsync(Database, lastResult.Name);
                        var fields = await InfluxDbClient.GetFieldKeysAsync(Database, lastResult.Name);
                        if (tags != null) tagKeys = new List<string>(tags);
                        if (fields != null)
                        {
                            fieldKeys = new List<string>();
                            foreach (var fk in fields)
                            {
                                if (fk != null && !string.IsNullOrEmpty(fk.Name))
                                    fieldKeys.Add(fk.Name);
                            }
                        }
                    }
                }
                catch
                {
                }

                ICollection<int> selectedIndexes = onlySelected ? GetSelectedRowIndexes() : null;
                var body = LineProtocolExport.FormatSeries(lastResult, tagKeys, fieldKeys, selectedIndexes);
                if (string.IsNullOrEmpty(body))
                {
                    AppForm.DisplayError(Localization.T(
                        "No Line Protocol lines were generated (need at least one field per point)."));
                    return;
                }

                File.WriteAllText(saveFileDialog.FileName, body, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        /// <summary>
        /// Excel-style clipboard: selected cells as TSV (rows/cols ordered).
        /// </summary>
        void CopySelectionAsTsv()
        {
            try
            {
                if (dataGridView.GetCellCount(DataGridViewElementStates.Selected) == 0) return;

                var rows = new SortedSet<int>();
                var cols = new SortedSet<int>();
                var selected = new HashSet<string>();

                foreach (DataGridViewCell cell in dataGridView.SelectedCells)
                {
                    rows.Add(cell.RowIndex);
                    cols.Add(cell.ColumnIndex);
                    selected.Add(cell.RowIndex + "," + cell.ColumnIndex);
                }

                var sb = new StringBuilder();
                var firstRow = true;
                foreach (var r in rows)
                {
                    if (!firstRow) sb.AppendLine();
                    firstRow = false;

                    var firstCol = true;
                    foreach (var c in cols)
                    {
                        if (!firstCol) sb.Append('\t');
                        firstCol = false;

                        if (selected.Contains(r + "," + c))
                        {
                            var val = dataGridView[c, r].Value;
                            sb.Append(val == null ? string.Empty : val.ToString());
                        }
                    }
                }

                SetClipboardText(sb.ToString());
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        void CopySelectedRowsAsJson()
        {
            try
            {
                var indexes = GetSelectedRowIndexes();
                if (indexes.Count == 0) return;
                var array = BuildJsonFromRows(indexes);
                SetClipboardText(JsonConvert.SerializeObject(array, Formatting.Indented));
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        void CopySelectedRowsAsKeyValue()
        {
            try
            {
                var rows = GetSelectedRawRows();
                if (rows.Count == 0) return;

                var sb = new StringBuilder();
                for (var r = 0; r < rows.Count; r++)
                {
                    if (r > 0) sb.AppendLine();
                    AppendKeyValueLine(sb, rows[r]);
                }
                SetClipboardText(sb.ToString());
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        void CopySelectedHeaders()
        {
            try
            {
                var cols = GetSelectedDataColumnIndexes();
                if (cols.Count == 0) return;

                var sb = new StringBuilder();
                var first = true;
                foreach (var c in cols)
                {
                    if (!first) sb.Append('\t');
                    first = false;
                    sb.Append(dataGridView.Columns[c].HeaderText);
                }
                SetClipboardText(sb.ToString());
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        void CopyAllHeaders()
        {
            try
            {
                if (dataGridView.Columns.Count <= 1) return;
                var sb = new StringBuilder();
                for (var c = 1; c < dataGridView.Columns.Count; c++)
                {
                    if (c > 1) sb.Append('\t');
                    sb.Append(dataGridView.Columns[c].HeaderText);
                }
                SetClipboardText(sb.ToString());
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        List<object> BuildJsonFromRows(IList<int> rowIndexes)
        {
            var array = new List<object>();
            if (lastResult == null) return array;

            if (rowIndexes == null)
            {
                for (var i = 0; i < lastResult.Values.Count; i++)
                    array.Add(RowToDictionary(lastResult.Values[i]));
            }
            else
            {
                foreach (var i in rowIndexes)
                {
                    var raw = GetRawRowAt(i);
                    if (raw != null) array.Add(RowToDictionary(raw));
                }
            }
            return array;
        }

        Dictionary<string, object> RowToDictionary(IList<object> row)
        {
            var d = new Dictionary<string, object>();
            for (var i = 0; i < row.Count && i < lastResult.Columns.Count; i++)
                d[lastResult.Columns[i]] = AppForm.Settings.ToLocalExportValue(row[i]);
            return d;
        }

        List<IList<object>> GetSelectedRawRows()
        {
            var rows = new List<IList<object>>();
            foreach (var index in GetSelectedRowIndexes())
            {
                var raw = GetRawRowAt(index);
                if (raw != null) rows.Add(raw);
            }
            return rows;
        }

        IList<object> GetRawRowAt(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dataGridView.Rows.Count) return null;
            var tagged = dataGridView.Rows[rowIndex].Tag as IList<object>;
            if (tagged != null) return tagged;
            if (lastResult != null && rowIndex < lastResult.Values.Count)
                return lastResult.Values[rowIndex];
            return null;
        }

        void AppendKeyValueLine(StringBuilder sb, IList<object> row)
        {
            var first = true;
            for (var i = 0; i < row.Count && i < lastResult.Columns.Count; i++)
            {
                var value = row[i];
                if (value == null) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(lastResult.Columns[i]);
                sb.Append('=');
                AppendKeyValueLiteral(sb, value);
            }
        }

        void AppendKeyValueLiteral(StringBuilder sb, object value)
        {
            if (value is bool)
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is sbyte || value is byte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            DateTime dt;
            if (AppSettings.TryGetDateTime(value, out dt))
            {
                var display = AppForm.Settings.FormatDateTime(dt) ?? string.Empty;
                sb.Append('"');
                sb.Append(display.Replace("\\", "\\\\").Replace("\"", "\\\""));
                sb.Append('"');
                return;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            sb.Append('"');
            sb.Append(text.Replace("\\", "\\\\").Replace("\"", "\\\""));
            sb.Append('"');
        }

        static void SetClipboardText(string text)
        {
            if (text == null) text = string.Empty;
            try
            {
                if (text.Length == 0)
                    Clipboard.Clear();
                else
                    Clipboard.SetText(text);
            }
            catch
            {
                Clipboard.SetDataObject(text, true);
            }
        }

        string GetExportBaseName()
        {
            if (!string.IsNullOrWhiteSpace(ExportFileNameStem))
                return ExportFileNameStem;

            var conn = InfluxDbClient != null && InfluxDbClient.Connection != null
                ? InfluxDbClient.Connection.Name
                : "export";
            var db = string.IsNullOrWhiteSpace(Database) ? "results" : Database;
            return conn + "_" + db;
        }

        #endregion Methods
    }
}
