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

        void listView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawBackground();
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
            var selected = e.Item.Selected;
            Color backColor;
            Color foreColor;
            if (selected)
            {
                backColor = listView.Focused ? SystemColors.Highlight : SystemColors.ControlLight;
                foreColor = listView.Focused ? SystemColors.HighlightText : e.Item.ForeColor;
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

        #endregion Methods
    }
}
