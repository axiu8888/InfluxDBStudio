using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScintillaNET;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio;
using CymaticLabs.InfluxDB.Studio.Dialogs;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// A control that executes an InfluxDB query and displays the results.
    /// </summary>
    public partial class QueryControl : RequestControl
    {
        #region Fields

        // Used for timing operations.
        System.Diagnostics.Stopwatch stopWatch;

        // Used to give resulting rows an ID number
        int resultsCount = 0;

        readonly List<string> measurements = new List<string>();
        readonly Dictionary<string, MeasurementSchema> schemaByMeasurement =
            new Dictionary<string, MeasurementSchema>(StringComparer.OrdinalIgnoreCase);
        readonly List<Bitmap> autocompleteIcons = new List<Bitmap>();

        Task loadMeasurementsTask;
        Task loadColumnsTask;
        string columnsLoadedFor;

        ToolStripMenuItem addFavoriteMenuItem;
        ToolStripMenuItem favoritesMenuItem;
        ToolStripMenuItem historyMenuItem;
        ToolStripMenuItem runSqlMenuItem;
        ToolStripMenuItem runSelectedSqlMenuItem;
        ToolTip savedQueryToolTip;
        ToolStripDropDown savedQueryToolTipOwner;

        class MeasurementSchema
        {
            public List<string> Fields = new List<string>();
            public List<string> Tags = new List<string>();
        }

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets or sets the query editor's text.
        /// </summary>
        public string EditorText
        {
            get { return queryEditor.Text; }
            set { queryEditor.Text = value; }
        }

        #endregion Properties

        #region Constructors

        public QueryControl()
        {
            stopWatch = new System.Diagnostics.Stopwatch();

            InitializeComponent();
            ConfigureQueryEditorMenu();
            Localization.Apply(this);

            // Clear query results text
            resultsLabel.Text = null;

            // Set query editor styles, InfluxDB is SQL like, so use those
            queryEditor.Styles[Style.Sql.Identifier].ForeColor = Color.Blue;
            queryEditor.Styles[Style.Sql.String].ForeColor = Color.Red;
            queryEditor.Styles[Style.Sql.Number].ForeColor = Color.Magenta;
            queryEditor.Styles[Style.Sql.QuotedIdentifier].ForeColor = Color.Red;

            ConfigureAutocomplete();
            Load += QueryControl_Load;
        }

        #endregion Constructors

        #region Event Handlers

        async void QueryControl_Load(object sender, EventArgs e)
        {
            await EnsureMeasurementsAsync();
            await EnsureColumnsForFromAsync();
        }

        void queryEditor_CharAdded(object sender, CharAddedEventArgs e)
        {
            var ch = (char)e.Char;
            if (ch == '\r' || ch == '\n' || ch == ';' || ch == ')') return;
            BeginShowAutocomplete(false);
        }

        void queryEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                BeginShowAutocomplete(true);
                return;
            }

            if (e.KeyCode == Keys.F5 && !e.Control && !e.Alt && !e.Shift)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                BeginExecuteQuery(false);
                return;
            }

            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                BeginExecuteQuery(true);
            }
        }

        void queryEditor_AutoCCompleted(object sender, AutoCSelectionEventArgs e)
        {
            var insert = InfluxQlAutoComplete.GetInsertText(e.Text);
            if (string.IsNullOrEmpty(insert) || insert == e.Text) return;

            var start = e.Position;
            var end = queryEditor.CurrentPosition;
            if (end <= start) end = start + e.Text.Length;
            if (end > queryEditor.TextLength) end = queryEditor.TextLength;

            var current = queryEditor.GetTextRange(start, Math.Max(0, end - start));
            if (current == insert) return;

            queryEditor.BeginUndoAction();
            try
            {
                queryEditor.TargetStart = start;
                queryEditor.TargetEnd = end;
                queryEditor.ReplaceTarget(insert);
                queryEditor.GotoPosition(start + insert.Length);
            }
            finally
            {
                queryEditor.EndUndoAction();
            }
        }

        void formatPrettyMenuItem_Click(object sender, EventArgs e)
        {
            ApplyFormattedSql(InfluxQlFormatter.FormatPretty);
        }

        void formatCompactMenuItem_Click(object sender, EventArgs e)
        {
            ApplyFormattedSql(InfluxQlFormatter.FormatCompact);
        }

        void runSqlMenuItem_Click(object sender, EventArgs e)
        {
            BeginExecuteQuery(false);
        }

        void runSelectedSqlMenuItem_Click(object sender, EventArgs e)
        {
            BeginExecuteQuery(true);
        }

        #endregion Event Handlers

        #region Methods

        void ConfigureQueryEditorMenu()
        {
            queryEditor.UsePopup(false);

            var menu = new ContextMenuStrip();

            runSqlMenuItem = new ToolStripMenuItem("Run SQL") { ShortcutKeys = Keys.F5 };
            runSqlMenuItem.Click += runSqlMenuItem_Click;
            runSelectedSqlMenuItem = new ToolStripMenuItem("Run Selected SQL") { ShortcutKeys = Keys.Control | Keys.Enter };
            runSelectedSqlMenuItem.Click += runSelectedSqlMenuItem_Click;

            var beautify = new ToolStripMenuItem("Beautify SQL (multiple lines)");
            beautify.Click += formatPrettyMenuItem_Click;
            var compact = new ToolStripMenuItem("Compact SQL (one line)");
            compact.Click += formatCompactMenuItem_Click;

            addFavoriteMenuItem = new ToolStripMenuItem("Add to Favorites...");
            addFavoriteMenuItem.Click += addFavoriteMenuItem_Click;
            favoritesMenuItem = new ToolStripMenuItem("Favorites");
            historyMenuItem = new ToolStripMenuItem("Query History");
            EnsureSavedQueryToolTip();
            favoritesMenuItem.DropDown.Opening += (s, e) => HideSavedQueryToolTip();
            favoritesMenuItem.DropDown.Closed += (s, e) => HideSavedQueryToolTip();
            historyMenuItem.DropDown.Opening += (s, e) => HideSavedQueryToolTip();
            historyMenuItem.DropDown.Closed += (s, e) => HideSavedQueryToolTip();

            var cut = new ToolStripMenuItem("Cut", null, (s, e) => queryEditor.Cut()) { ShortcutKeys = Keys.Control | Keys.X };
            var copy = new ToolStripMenuItem("Copy", null, (s, e) => queryEditor.Copy()) { ShortcutKeys = Keys.Control | Keys.C };
            var paste = new ToolStripMenuItem("Paste", null, (s, e) => queryEditor.Paste()) { ShortcutKeys = Keys.Control | Keys.V };
            var selectAll = new ToolStripMenuItem("Select All", null, (s, e) => queryEditor.SelectAll()) { ShortcutKeys = Keys.Control | Keys.A };

            menu.Items.Add(runSqlMenuItem);
            menu.Items.Add(runSelectedSqlMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(beautify);
            menu.Items.Add(compact);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(addFavoriteMenuItem);
            menu.Items.Add(favoritesMenuItem);
            menu.Items.Add(historyMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(cut);
            menu.Items.Add(copy);
            menu.Items.Add(paste);
            menu.Items.Add(selectAll);

            menu.ShowItemToolTips = true;
            menu.Opening += (s, e) =>
            {
                HideSavedQueryToolTip();
                var hasText = queryEditor.TextLength > 0;
                var hasSelection = queryEditor.SelectionStart != queryEditor.SelectionEnd;
                var hasSelectedSql = !string.IsNullOrWhiteSpace(queryEditor.SelectedText);
                runSqlMenuItem.Enabled = hasText;
                runSelectedSqlMenuItem.Enabled = hasSelectedSql;
                beautify.Enabled = hasText;
                compact.Enabled = hasText;
                cut.Enabled = hasSelection;
                copy.Enabled = hasSelection;
                addFavoriteMenuItem.Enabled = !string.IsNullOrWhiteSpace(queryEditor.Text);
                RebuildSavedQueryMenus();
            };
            menu.Closed += (s, e) => HideSavedQueryToolTip();

            queryEditor.ContextMenuStrip = menu;
        }

        void EnsureSavedQueryToolTip()
        {
            if (savedQueryToolTip != null) return;

            savedQueryToolTip = new ToolTip
            {
                AutoPopDelay = 60000,
                InitialDelay = 400,
                ReshowDelay = 100,
                ShowAlways = true,
                UseFading = false,
                UseAnimation = false
            };
        }

        void ShowSavedQueryToolTip(ToolStripItem item, string sql)
        {
            if (item == null || string.IsNullOrWhiteSpace(sql)) return;

            var dropDown = item.Owner as ToolStripDropDown;
            if (dropDown == null || dropDown.IsDisposed) return;

            EnsureSavedQueryToolTip();
            HideSavedQueryToolTip();

            savedQueryToolTipOwner = dropDown;

            // Prefer below the item; if not enough screen space, show above.
            var lineCount = 1;
            for (var i = 0; i < sql.Length; i++)
            {
                if (sql[i] == '\n') lineCount++;
            }
            var estimatedHeight = Math.Min(420, 24 + lineCount * 16);
            var x = item.Bounds.Left;
            var belowY = item.Bounds.Bottom + 4;
            var screenBottom = dropDown.PointToScreen(new Point(x, belowY + estimatedHeight));
            var workingArea = Screen.FromControl(dropDown).WorkingArea;

            var y = screenBottom.Y <= workingArea.Bottom
                ? belowY
                : item.Bounds.Top - estimatedHeight - 4;

            savedQueryToolTip.Show(sql, dropDown, x, y);
        }

        void HideSavedQueryToolTip()
        {
            if (savedQueryToolTip == null) return;

            if (savedQueryToolTipOwner != null && !savedQueryToolTipOwner.IsDisposed)
                savedQueryToolTip.Hide(savedQueryToolTipOwner);
            else
                savedQueryToolTip.RemoveAll();

            savedQueryToolTipOwner = null;
        }

        void RebuildSavedQueryMenus()
        {
            RebuildSavedQueryMenu(favoritesMenuItem, QueryLibrary.Favorites, false);
            RebuildSavedQueryMenu(historyMenuItem, QueryLibrary.History, true);
        }

        void RebuildSavedQueryMenu(ToolStripMenuItem parent, IList<SavedQuery> items, bool history)
        {
            HideSavedQueryToolTip();
            parent.DropDownItems.Clear();

            if (items == null || items.Count == 0)
            {
                parent.DropDownItems.Add(new ToolStripMenuItem(Localization.T("(none)")) { Enabled = false });
                return;
            }

            foreach (var item in items)
            {
                var entry = item;
                var sql = entry.Sql ?? string.Empty;
                var root = new ToolStripMenuItem(QueryLibrary.GetDisplayText(entry, history));
                root.Click += (s, e) => ApplySavedQuery(entry);
                root.MouseEnter += (s, e) => ShowSavedQueryToolTip(root, sql);
                root.MouseLeave += (s, e) => HideSavedQueryToolTip();
                root.DropDownOpening += (s, e) => HideSavedQueryToolTip();

                var selectItem = new ToolStripMenuItem(Localization.T("Select"));
                selectItem.Click += (s, e) => ApplySavedQuery(entry);

                var editItem = new ToolStripMenuItem(Localization.T("Edit"));
                editItem.Click += (s, e) => EditSavedQuery(entry, history);

                var deleteItem = new ToolStripMenuItem(Localization.T("Delete"));
                deleteItem.Click += (s, e) => DeleteSavedQuery(entry, history);

                root.DropDownItems.Add(selectItem);
                root.DropDownItems.Add(editItem);
                root.DropDownItems.Add(deleteItem);
                parent.DropDownItems.Add(root);
            }
        }

        void addFavoriteMenuItem_Click(object sender, EventArgs e)
        {
            var sql = queryEditor.Text;
            if (string.IsNullOrWhiteSpace(sql)) return;

            var dialog = new SavedQueryDialog();
            dialog.Text = Localization.T("Add to Favorites");
            dialog.ShowName = true;
            dialog.Sql = sql.Trim();

            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
            QueryLibrary.AddFavorite(dialog.QueryName, dialog.Sql);
        }

        void ApplySavedQuery(SavedQuery item)
        {
            if (item == null || string.IsNullOrEmpty(item.Sql)) return;
            queryEditor.Text = item.Sql;
            queryEditor.Focus();
        }

        void EditSavedQuery(SavedQuery item, bool history)
        {
            if (item == null) return;

            var dialog = new SavedQueryDialog();
            dialog.Text = Localization.T(history ? "Edit History Query" : "Edit Favorite");
            dialog.ShowName = !history;
            dialog.QueryName = item.Name;
            dialog.Sql = item.Sql;

            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

            item.Sql = dialog.Sql;
            if (!history) item.Name = string.IsNullOrWhiteSpace(dialog.QueryName) ? null : dialog.QueryName;

            if (history) QueryLibrary.UpdateHistory(item);
            else QueryLibrary.UpdateFavorite(item);
        }

        void DeleteSavedQuery(SavedQuery item, bool history)
        {
            if (item == null) return;

            var message = history
                ? Localization.T("Delete this query from history?")
                : Localization.Format("Delete favorite '{0}'?", QueryLibrary.GetDisplayText(item, false));

            if (MessageBox.Show(message, Localization.T("Confirm Delete"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
                != DialogResult.OK)
            {
                return;
            }

            if (history) QueryLibrary.RemoveHistory(item.Id);
            else QueryLibrary.RemoveFavorite(item.Id);
        }

        void ApplyFormattedSql(Func<string, string> formatter)
        {
            var hasSelection = queryEditor.SelectionStart != queryEditor.SelectionEnd;
            var source = hasSelection ? queryEditor.SelectedText : queryEditor.Text;
            if (string.IsNullOrWhiteSpace(source)) return;

            var formatted = formatter(source);
            if (formatted == source) return;

            queryEditor.BeginUndoAction();
            try
            {
                if (hasSelection)
                {
                    queryEditor.ReplaceSelection(formatted);
                }
                else
                {
                    queryEditor.TargetStart = 0;
                    queryEditor.TargetEnd = queryEditor.TextLength;
                    queryEditor.ReplaceTarget(formatted);
                }
            }
            finally
            {
                queryEditor.EndUndoAction();
            }
        }

        void ConfigureAutocomplete()
        {
            queryEditor.WordChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.";
            queryEditor.AutoCIgnoreCase = true;
            queryEditor.AutoCAutoHide = true;
            queryEditor.AutoCCancelAtStart = false;
            queryEditor.AutoCChooseSingle = false;
            queryEditor.AutoCMaxHeight = 16;
            queryEditor.AutoCMaxWidth = 0;
            queryEditor.AutoCOrder = Order.Custom;
            queryEditor.AutoCSeparator = InfluxQlAutoComplete.ListSeparator;
            queryEditor.AutoCTypeSeparator = InfluxQlAutoComplete.TypeSeparator;
            queryEditor.AutoCSetFillUps("");
            RegisterAutocompleteIcons();
            queryEditor.CharAdded += queryEditor_CharAdded;
            queryEditor.KeyDown += queryEditor_KeyDown;
            queryEditor.AutoCCompleted += queryEditor_AutoCCompleted;
        }

        void RegisterAutocompleteIcons()
        {
            queryEditor.ClearRegisteredImages();
            foreach (var icon in autocompleteIcons) icon.Dispose();
            autocompleteIcons.Clear();

            RegisterAutocompleteIcon((int)InfluxQlAutoComplete.Kind.Field, Color.FromArgb(46, 139, 87), "F");
            RegisterAutocompleteIcon((int)InfluxQlAutoComplete.Kind.Tag, Color.FromArgb(30, 122, 184), "T");
            RegisterAutocompleteIcon((int)InfluxQlAutoComplete.Kind.Function, Color.FromArgb(128, 57, 186), "f");
            RegisterAutocompleteIcon((int)InfluxQlAutoComplete.Kind.Measurement, Color.FromArgb(214, 122, 28), "M");
            RegisterAutocompleteIcon((int)InfluxQlAutoComplete.Kind.Keyword, Color.FromArgb(90, 90, 90), "K");
        }

        void RegisterAutocompleteIcon(int type, Color color, string letter)
        {
            var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(color))
                    g.FillEllipse(brush, 1, 1, 14, 14);

                using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point))
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    TextRenderer.DrawText(
                        g,
                        letter,
                        font,
                        new Rectangle(0, 0, 16, 16),
                        Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
            }

            autocompleteIcons.Add(bmp);
            queryEditor.RegisterRgbaImage(type, bmp);
        }

        async void BeginShowAutocomplete(bool force)
        {
            try
            {
                var position = queryEditor.CurrentPosition;
                await EnsureMeasurementsAsync();
                await EnsureColumnsForFromAsync();
                if (IsDisposed || queryEditor.IsDisposed) return;

                if (queryEditor.CurrentPosition != position && !force) return;

                if (InvokeRequired)
                    BeginInvoke(new Action<bool>(ShowAutocomplete), force);
                else
                    ShowAutocomplete(force);
            }
            catch
            {
                // Autocomplete should never interrupt typing
            }
        }

        void ShowAutocomplete(bool force)
        {
            if (IsDisposed || queryEditor.IsDisposed) return;

            var pos = queryEditor.CurrentPosition;
            var wordStart = queryEditor.WordStartPosition(pos, true);
            if (wordStart > 0)
            {
                var before = queryEditor.GetTextRange(wordStart - 1, 1);
                if (before == "\"") wordStart--;
            }

            var lengthEntered = Math.Max(0, pos - wordStart);
            var currentWord = lengthEntered > 0 ? queryEditor.GetTextRange(wordStart, lengthEntered) : string.Empty;
            var textBeforeWord = queryEditor.GetTextRange(0, wordStart);

            if (InfluxQlAutoComplete.IsInStringOrComment(textBeforeWord + currentWord) && !force)
            {
                if (queryEditor.AutoCActive) queryEditor.AutoCCancel();
                return;
            }

            var prefix = currentWord.Trim('"');
            if (!force && prefix.Length == 0 && !ShouldSuggestAfterToken(textBeforeWord))
            {
                if (queryEditor.AutoCActive) queryEditor.AutoCCancel();
                return;
            }

            var context = InfluxQlAutoComplete.GetContext(textBeforeWord);
            var fromMeasurement = InfluxQlAutoComplete.GetFromMeasurement(queryEditor.Text);
            IEnumerable<string> fields = null;
            IEnumerable<string> tags = null;
            MeasurementSchema schema;
            if (!string.IsNullOrEmpty(fromMeasurement) &&
                schemaByMeasurement.TryGetValue(fromMeasurement, out schema))
            {
                fields = schema.Fields;
                tags = schema.Tags;
            }

            var items = InfluxQlAutoComplete.BuildItems(context, prefix, measurements, fields, tags);
            if (items.Count == 0)
            {
                if (queryEditor.AutoCActive) queryEditor.AutoCCancel();
                return;
            }

            if (items.Count == 1 && string.Equals(items[0].InsertText.Trim('"'), prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (queryEditor.AutoCActive) queryEditor.AutoCCancel();
                return;
            }

            var list = string.Join(InfluxQlAutoComplete.ListSeparator.ToString(), items.Select(i => i.ToListEntry()));
            queryEditor.AutoCShow(lengthEntered, list);
        }

        static bool ShouldSuggestAfterToken(string textBeforeWord)
        {
            return InfluxQlAutoComplete.GetContext(textBeforeWord) != InfluxQlAutoComplete.Context.General;
        }

        async Task EnsureMeasurementsAsync()
        {
            if (measurements.Count > 0) return;
            if (InfluxDbClient == null || string.IsNullOrWhiteSpace(Database)) return;
            if (loadMeasurementsTask != null)
            {
                await loadMeasurementsTask;
                return;
            }

            loadMeasurementsTask = LoadMeasurementsCoreAsync();
            try
            {
                await loadMeasurementsTask;
            }
            finally
            {
                if (measurements.Count == 0) loadMeasurementsTask = null;
            }
        }

        async Task LoadMeasurementsCoreAsync()
        {
            try
            {
                var names = await InfluxDbClient.GetMeasurementNamesAsync(Database);
                measurements.Clear();
                if (names == null) return;
                measurements.AddRange(names.Where(n => !string.IsNullOrWhiteSpace(n)));
            }
            catch
            {
                measurements.Clear();
            }
        }

        async Task EnsureColumnsForFromAsync()
        {
            if (InfluxDbClient == null || string.IsNullOrWhiteSpace(Database)) return;

            var measurement = InfluxQlAutoComplete.GetFromMeasurement(queryEditor.Text);
            if (string.IsNullOrEmpty(measurement)) return;
            if (schemaByMeasurement.ContainsKey(measurement)) return;
            if (loadColumnsTask != null && string.Equals(columnsLoadedFor, measurement, StringComparison.OrdinalIgnoreCase))
            {
                await loadColumnsTask;
                return;
            }

            columnsLoadedFor = measurement;
            loadColumnsTask = LoadColumnsCoreAsync(measurement);
            try
            {
                await loadColumnsTask;
            }
            finally
            {
                if (!schemaByMeasurement.ContainsKey(measurement))
                {
                    loadColumnsTask = null;
                    columnsLoadedFor = null;
                }
            }
        }

        async Task LoadColumnsCoreAsync(string measurement)
        {
            try
            {
                var schema = new MeasurementSchema();
                var fields = await InfluxDbClient.GetFieldKeysAsync(Database, measurement);
                if (fields != null)
                {
                    foreach (var field in fields)
                    {
                        if (field != null && !string.IsNullOrWhiteSpace(field.Name))
                            schema.Fields.Add(field.Name);
                    }
                }

                var tags = await InfluxDbClient.GetTagKeysAsync(Database, measurement);
                if (tags != null) schema.Tags.AddRange(tags.Where(t => !string.IsNullOrWhiteSpace(t)));

                schemaByMeasurement[measurement] = schema;
            }
            catch
            {
                // Ignore schema lookup failures
            }
        }

        async void BeginExecuteQuery(bool selectedOnly)
        {
            await ExecuteQuerySafeAsync(selectedOnly);
        }

        async Task ExecuteQuerySafeAsync(bool selectedOnly)
        {
            try
            {
                await ExecuteQueryAsync(selectedOnly);
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        /// <summary>
        /// Runs the current query against the configured connection and database.
        /// </summary>
        public override async Task ExecuteRequestAsync()
        {
            await ExecuteQueryAsync(false);
        }

        /// <summary>
        /// Runs either the whole editor SQL or only the current selection.
        /// </summary>
        public async Task ExecuteQueryAsync(bool selectedOnly)
        {
            if (InfluxDbClient == null) throw new Exception("No InfluxDB client available.");

            // Reset the results count
            resultsCount = 0;

            var query = selectedOnly ? queryEditor.SelectedText : EditorText;
            if (string.IsNullOrWhiteSpace(query)) return;
            query = query.Trim();

            bool addedLimit, addedOffset;
            var autoPaged = InfluxDbHelper.TryEnsureSelectQueryPaging(query, out query, out addedLimit, out addedOffset);
            if (autoPaged && !selectedOnly) EditorText = query;

            QueryLibrary.AddHistory(query);

            bool isAggregate = query.ToLower().Contains("group by");

            // Clear the current results
            tabControl.Controls.Clear();

            // Start timing...
            stopWatch.Restart();

            // Execute the query
            var results = await InfluxDbClient.QueryAsync(Database, query);

            // Stop timing...
            stopWatch.Stop();

            // If there are results
            if (results != null && results.Count() > 0)
            {
                var tabCount = 0;
                var tabLabel = isAggregate ? Localization.T("Group") : Localization.T("Results");

                foreach (var result in results)
                {
                    // Create a new tab page to hold the query results control
                    var tab = new TabPage(string.Format("{0} {1}", tabLabel, ++tabCount));

                    // Create a new query results control
                    var queryResultsControl = new QueryResultsControl();
                    queryResultsControl.InfluxDbClient = InfluxDbClient;
                    queryResultsControl.Database = Database;
                    queryResultsControl.Dock = DockStyle.Fill;
                    tab.Controls.Add(queryResultsControl);

                    // Add the tab to the control
                    tabControl.TabPages.Add(tab);

                    // Render the results and increment the global total
                    resultsCount += queryResultsControl.UpdateResults(result);
                }
            }

            // Show stat results of query
            var status = Localization.Format("results: {0}, response time: {1:0} ms", resultsCount, stopWatch.Elapsed.TotalMilliseconds);
            if (addedLimit && addedOffset)
                status += Localization.Format("  (LIMIT {0} OFFSET {1} added automatically)", InfluxDbHelper.DefaultQueryLimit, InfluxDbHelper.DefaultQueryOffset);
            else if (addedLimit)
                status += Localization.Format("  (LIMIT {0} added automatically)", InfluxDbHelper.DefaultQueryLimit);
            else if (addedOffset)
                status += Localization.Format("  (OFFSET {0} added automatically)", InfluxDbHelper.DefaultQueryOffset);
            resultsLabel.Text = status;
        }

        #endregion Methods
    }
}
