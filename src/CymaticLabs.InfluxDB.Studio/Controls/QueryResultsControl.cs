using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
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

        // Used to give resulting rows an ID number
        int resultsCount = 0;

        // A cache of the last results received.
        InfluxDbSeries lastResult;

        // Column/item under the mouse when opening the context menu or copying a cell/column.
        int contextColumnIndex = -1;
        ListViewItem contextItem;

        // Column selected by clicking the header (Excel-like whole-column selection).
        int selectedColumnIndex = -1;

        static readonly Color ColumnSelectionBackColor = Color.FromArgb(204, 232, 255);
        static readonly Color ColumnSelectionHeaderBackColor = Color.FromArgb(160, 200, 240);

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets or sets the <see cref="InfluxDB.InfluxDbClient">InfluxDB connection</see> associated
        /// with the control.
        /// </summary>
        public InfluxDbClient InfluxDbClient { get; set; }

        /// <summary>
        /// Gets or sets the name of the database associated with the control.
        /// </summary>
        public string Database { get; set; }

        #endregion Properties

        #region Constructors

        public QueryResultsControl()
        {
            InitializeComponent();
            Localization.Apply(this);

            listView.OwnerDraw = true;
            listView.DrawColumnHeader += listView_DrawColumnHeader;
            listView.DrawItem += listView_DrawItem;
            listView.DrawSubItem += listView_DrawSubItem;
            listView.ColumnClick += listView_ColumnClick;
        }

        #endregion Constructors

        #region Event Handlers

        // Export All -> CSV
        private async void exportAllCsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToCsv();
        }

        // Export All -> Excel
        private void exportAllExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportToExcel();
        }

        // Export All -> JSON
        private void jSONToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportToJson();
        }

        // Export All -> Line Protocol
        private async void exportAllLineProtocolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToLineProtocol();
        }

        // Export Selected -> Excel
        private void exportSelectedExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportToExcel(true);
        }

        // Export Selected -> CSV
        private async void exportSelectedCsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToCsv(true);
        }

        // Export Selected -> JSON
        private void jSONToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ExportToJson(true);
        }

        // Export Selected -> Line Protocol
        private async void exportSelectedLineProtocolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExportToLineProtocol(true);
        }

        void listView_MouseDown(object sender, MouseEventArgs e)
        {
            var hit = listView.HitTest(e.Location);
            contextItem = hit.Item;
            contextColumnIndex = -1;

            if (hit.Item != null && hit.SubItem != null)
            {
                contextColumnIndex = hit.Item.SubItems.IndexOf(hit.SubItem);

                // Left-click a cell leaves column-header selection mode.
                if (e.Button == MouseButtons.Left && selectedColumnIndex >= 0)
                {
                    selectedColumnIndex = -1;
                    listView.Invalidate();
                }

                if (e.Button == MouseButtons.Right && !hit.Item.Selected && selectedColumnIndex < 0)
                {
                    // Right-click on an unselected row focuses that row for copy actions.
                    listView.SelectedItems.Clear();
                    hit.Item.Selected = true;
                    hit.Item.Focused = true;
                }
            }
            else if (e.Button == MouseButtons.Right && selectedColumnIndex >= 0)
            {
                contextColumnIndex = selectedColumnIndex;
            }
        }

        void listView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Click header to select the whole column (like Excel).
            if (e.Column < 0 || e.Column >= listView.Columns.Count) return;

            selectedColumnIndex = e.Column;
            contextColumnIndex = e.Column;
            contextItem = null;
            listView.SelectedItems.Clear();
            listView.Invalidate();
            listView.Focus();
        }

        void listView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAllRows();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                if (selectedColumnIndex >= 0)
                    CopyColumn();
                else
                    CopySelectedRowsAsJson();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && selectedColumnIndex >= 0)
            {
                selectedColumnIndex = -1;
                listView.Invalidate();
                e.Handled = true;
            }
        }

        void SelectAllRows()
        {
            if (listView.Items.Count == 0) return;

            selectedColumnIndex = -1;
            listView.BeginUpdate();
            foreach (ListViewItem item in listView.Items)
                item.Selected = true;
            listView.EndUpdate();
            listView.Invalidate();
        }

        void contextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var hasSelection = listView.SelectedItems.Count > 0;
            var hasCell = contextItem != null && contextColumnIndex >= 0
                && contextColumnIndex < contextItem.SubItems.Count;
            var hasColumnTarget = selectedColumnIndex >= 0
                || (contextColumnIndex >= 0 && (hasSelection || listView.Items.Count > 0));

            copyCellToolStripMenuItem.Enabled = hasCell;
            copyRowsToolStripMenuItem.Enabled = hasSelection;
            copyColumnToolStripMenuItem.Enabled = hasColumnTarget && listView.Items.Count > 0;
        }

        void copyCellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyCell();
        }

        void copyRowsJsonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelectedRowsAsJson();
        }

        void copyRowsKeyValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopySelectedRowsAsKeyValue();
        }

        void copyColumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyColumn();
        }

        void listView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            var selected = e.ColumnIndex == selectedColumnIndex;
            var backColor = selected ? ColumnSelectionHeaderBackColor : SystemColors.Control;
            using (var back = new SolidBrush(backColor))
                e.Graphics.FillRectangle(back, e.Bounds);

            ControlPaint.DrawBorder(e.Graphics, e.Bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);

            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                e.Font,
                e.Bounds,
                e.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        void listView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            // Drawing is handled in DrawSubItem so every column can be centered,
            // including the first column which ListView cannot center by default.
        }

        void listView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            var rowSelected = e.Item.Selected;
            var columnSelected = e.ColumnIndex == selectedColumnIndex;
            Color backColor;
            Color foreColor;

            if (rowSelected)
            {
                backColor = listView.Focused ? SystemColors.Highlight : SystemColors.ControlLight;
                foreColor = listView.Focused ? SystemColors.HighlightText : e.Item.ForeColor;
            }
            else if (columnSelected)
            {
                backColor = ColumnSelectionBackColor;
                foreColor = e.Item.ForeColor;
            }
            else
            {
                backColor = e.Item.BackColor;
                foreColor = e.Item.ForeColor;
            }

            using (var back = new SolidBrush(backColor))
                e.Graphics.FillRectangle(back, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                e.SubItem.Font ?? e.Item.Font,
                e.Bounds,
                foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (listView.GridLines)
            {
                using (var pen = new Pen(SystemColors.ControlLight))
                    e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
        }

        #endregion Event Handlers

        #region Methods

        /// <summary>
        /// Clears the current query results from the UI.
        /// </summary>
        public void ClearResults()
        {
            // Clear out current items
            resultsCount = 0;
            tagsTextBox.Text = null;
            selectedColumnIndex = -1;
            contextColumnIndex = -1;
            contextItem = null;
            listView.BeginUpdate();
            listView.Columns.Clear();
            listView.Items.Clear();
            listView.EndUpdate();
        }

        /// <summary>
        /// Updates the query control's query results UI with the supplied result.
        /// </summary>
        /// <param name="result">The query result to render.</param>
        /// <returns>The total number of results found.</returns>
        public int UpdateResults(InfluxDbSeries result, bool clear = false)
        {
            if (result == null) throw new ArgumentNullException("result");

            // Cache
            lastResult = result;

            // Clear as needed
            if (clear) ClearResults();

            // Add tag values to to results
            if (result.Tags.Count > 0)
            {
                splitContainer.Panel1Collapsed = false;
                var tagCount = result.Tags.Count;
                var tagCounter = 0;
                var sb = new StringBuilder();

                foreach (var tag in result.Tags)
                {
                    sb.AppendFormat("{0} = {1}{2}", tag.Key, tag.Value, ++tagCounter < tagCount ? ", " : null);
                }

                tagsTextBox.Text = sb.ToString();
            }
            // Hide tag area if there are no tag values
            else
            {
                splitContainer.Panel1Collapsed = true;
            }

            // Start to update the list view with the new results
            listView.BeginUpdate();

            // Build the first column
            var colRecordNum = new ColumnHeader() { Text = Localization.T("#"), TextAlign = HorizontalAlignment.Center };
            listView.Columns.Add(colRecordNum);

            // Build the dynamic columns
            foreach (var c in result.Columns)
            {
                var col = new ColumnHeader();
                col.Text = c;
                col.TextAlign = HorizontalAlignment.Center;
                listView.Columns.Add(col);
            }

            // Build the rows
            for (var i = 0; i < result.Values.Count; i++)
            {
                // Create the top level row item and give it the record number as a label
                ListViewItem li = new ListViewItem((++resultsCount).ToString());
                listView.Items.Add(li);

                // Get the columns/values for the row
                var r = result.Values[i];

                for (var x = 0; x < r.Count; x++)
                {
                    // Get the value
                    var v = r[x];

                    // Attach the column values as subitems
                    var li2 = new ListViewItem.ListViewSubItem(li, AppForm.Settings.FormatValue(v));
                    li2.Tag = r;
                    li.SubItems.Add(li2);
                }
            }

            // Resize each column
            if (listView.Columns.Count > 0)
            {
                var columnWidth = (Width - 12) / listView.Columns.Count;
                if (columnWidth < 96) columnWidth = 96;
                foreach (ColumnHeader col in listView.Columns) col.Width = columnWidth;
            }

            listView.EndUpdate();

            return resultsCount;
        }

        // Exports series data to CSV
        async Task ExportToCsv(bool onlySelected = false)
        {
            try
            {
                // Configure save dialog and open
                saveFileDialog.FileName = string.Format("{0}.csv", InfluxDbClient.Connection.Name + "_" + Database);
                saveFileDialog.Filter = Localization.T("CSV files|*.csv|All files|*.*");

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();

                    // Create a stream writer to write the CSV file
                    using (var sw = new StreamWriter(saveFileDialog.FileName))
                    {
                        sb.Clear();

                        // Write the CSV column names (skip first column which is just row # label)
                        for (var i = 1; i < listView.Columns.Count; i++)
                        {
                            sb.Append(listView.Columns[i].Text);
                            if (i < listView.Columns.Count - 1) sb.Append(",");
                        }

                        await sw.WriteLineAsync(sb.ToString());

                        // Now write each series row
                        foreach (ListViewItem li in listView.Items)
                        {
                            if (onlySelected && !li.Selected) continue;

                            sb.Clear();

                            // (skip first column which is just row # label)
                            for (var i = 1; i < li.SubItems.Count; i++)
                            {
                                var sli = li.SubItems[i];
                                sb.Append(sli.Text);
                                if (i < li.SubItems.Count - 1) sb.Append(",");
                            }

                            await sw.WriteLineAsync(sb.ToString());
                        }
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
                saveFileDialog.FileName = string.Format("{0}.xlsx", InfluxDbClient.Connection.Name + "_" + Database);
                saveFileDialog.Filter = Localization.T("Excel files|*.xlsx|All files|*.*");

                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

                var headers = new List<string>();
                for (var i = 1; i < listView.Columns.Count; i++)
                    headers.Add(listView.Columns[i].Text);

                var rows = new List<IList<string>>();
                foreach (ListViewItem li in listView.Items)
                {
                    if (onlySelected && !li.Selected) continue;

                    var row = new List<string>();
                    for (var i = 1; i < li.SubItems.Count; i++)
                        row.Add(li.SubItems[i].Text);
                    rows.Add(row);
                }

                ExcelExport.WriteXlsx(saveFileDialog.FileName, Localization.T("Results"), headers, rows);
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        // Exports series data to a JSON array
        void ExportToJson(bool onlySelected = false)
        {
            try
            {
                // Configure save dialog and open
                saveFileDialog.FileName = string.Format("{0}.json", InfluxDbClient.Connection.Name + "_" + Database);
                saveFileDialog.Filter = Localization.T("JSON files|*.json|All files|*.*");

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Collect the list of points
                    var array = new List<object>();

                    if (lastResult != null)
                    {
                        // Build name lookup
                        var indexToName = new Dictionary<int, string>();

                        foreach (var colName in lastResult.Columns)
                        {
                            if (!indexToName.ContainsKey(indexToName.Count))
                                indexToName.Add(indexToName.Count, colName);
                        }

                        // Build selected states from UI state
                        var selectedByRowId = new Dictionary<int, bool>();

                        for (var i = 0; i < listView.Items.Count; i++)
                        {
                            var li = listView.Items[i];
                            selectedByRowId.Add(i, li.Selected);
                        }

                        // Convert results to JSON for export
                        for (var i = 0; i < lastResult.Values.Count; i++)
                        {
                            var r = lastResult.Values[i];

                            if (onlySelected && !selectedByRowId[i]) continue;

                            // Convert to outgoing dictionary
                            var d = new Dictionary<string, object>();

                            for (var x = 0; x < r.Count; x++)
                            {
                                var key = indexToName[x];
                                var value = AppForm.Settings.ToLocalExportValue(r[x]);

                                if (d.ContainsKey(key)) d[key] = value;
                                else d.Add(key, value);
                            }

                            // Add to outgoing json structure
                            array.Add(d);
                        }
                    }

                    // Serialize to json
                    var json = JsonConvert.SerializeObject(array, Formatting.Indented);

                    // Write to disk
                    File.WriteAllText(saveFileDialog.FileName, json);
                }
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
                if (lastResult == null)
                    return;

                if (string.IsNullOrWhiteSpace(lastResult.Name))
                {
                    AppForm.DisplayError(Localization.T(
                        "Cannot export Line Protocol: the result has no measurement name. Use a SELECT query against a measurement."));
                    return;
                }

                saveFileDialog.FileName = string.Format("{0}.lp", InfluxDbClient.Connection.Name + "_" + Database);
                saveFileDialog.Filter = Localization.T("Line Protocol files|*.lp|All files|*.*");

                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    return;

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
                    // Schema lookup is best-effort; fall back to series.Tags + columns-as-fields.
                }

                List<int> selectedIndexes = null;
                if (onlySelected)
                {
                    selectedIndexes = new List<int>();
                    for (var i = 0; i < listView.Items.Count; i++)
                    {
                        if (listView.Items[i].Selected)
                            selectedIndexes.Add(i);
                    }
                }

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

        void CopyCell()
        {
            try
            {
                if (contextItem == null || contextColumnIndex < 0
                    || contextColumnIndex >= contextItem.SubItems.Count)
                    return;

                SetClipboardText(contextItem.SubItems[contextColumnIndex].Text ?? string.Empty);
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
                var rows = GetSelectedRawRows();
                if (rows.Count == 0) return;

                var array = new List<Dictionary<string, object>>();
                foreach (var row in rows)
                {
                    var d = new Dictionary<string, object>();
                    for (var i = 0; i < row.Count && i < lastResult.Columns.Count; i++)
                    {
                        var key = lastResult.Columns[i];
                        d[key] = AppForm.Settings.ToLocalExportValue(row[i]);
                    }
                    array.Add(d);
                }

                var json = JsonConvert.SerializeObject(array, Formatting.Indented);
                SetClipboardText(json);
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
                sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            DateTime dt;
            if (AppSettings.TryGetDateTime(value, out dt))
            {
                // Match the query results list display (local time + user date/time format).
                var display = AppForm.Settings.FormatDateTime(dt) ?? string.Empty;
                sb.Append('"');
                sb.Append(display.Replace("\\", "\\\\").Replace("\"", "\\\""));
                sb.Append('"');
                return;
            }

            // Other string-like values: quoted
            var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            sb.Append('"');
            sb.Append(text.Replace("\\", "\\\\").Replace("\"", "\\\""));
            sb.Append('"');
        }

        List<IList<object>> GetSelectedRawRows()
        {
            var rows = new List<IList<object>>();
            if (lastResult == null || lastResult.Values == null) return rows;

            foreach (ListViewItem item in listView.Items)
            {
                if (!item.Selected) continue;

                IList<object> raw = null;
                if (item.SubItems.Count > 1)
                    raw = item.SubItems[1].Tag as IList<object>;

                if (raw == null)
                {
                    var index = item.Index;
                    if (index >= 0 && index < lastResult.Values.Count)
                        raw = lastResult.Values[index];
                }

                if (raw != null) rows.Add(raw);
            }

            return rows;
        }

        void CopyColumn()
        {
            try
            {
                var columnIndex = selectedColumnIndex >= 0 ? selectedColumnIndex : contextColumnIndex;
                if (columnIndex < 0 || columnIndex >= listView.Columns.Count) return;
                if (listView.Items.Count == 0) return;

                var wholeColumn = selectedColumnIndex >= 0;
                var sb = new StringBuilder();
                var first = true;

                foreach (ListViewItem item in listView.Items)
                {
                    if (!wholeColumn && !item.Selected) continue;
                    if (!first) sb.AppendLine();
                    first = false;
                    if (columnIndex < item.SubItems.Count)
                        sb.Append(item.SubItems[columnIndex].Text);
                }

                SetClipboardText(sb.ToString());
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        static void SetClipboardText(string text)
        {
            if (text == null) text = string.Empty;

            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                Clipboard.SetDataObject(text, true);
            }
        }

        #endregion Methods
    }
}
