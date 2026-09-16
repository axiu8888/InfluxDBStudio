using System;
using System.Drawing;
using System.Windows.Forms;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Browse, search, edit, and delete query history.
    /// </summary>
    public class QueryHistoryDialog : Form
    {
        TextBox searchTextBox;
        ListView listView;
        TextBox previewTextBox;
        Button selectButton;
        Button editButton;
        Button deleteButton;
        Button clearButton;

        public SavedQuery SelectedQuery { get; private set; }

        public QueryHistoryDialog()
        {
            Text = Localization.T("Query History");
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            MinimumSize = new Size(640, 420);
            ClientSize = new Size(720, 520);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var searchPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var searchLabel = new Label
            {
                Text = Localization.T("Search:"),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 0, 0)
            };

            searchTextBox = new TextBox { Dock = DockStyle.Fill };
            searchTextBox.TextChanged += (s, e) => RefreshList();

            searchPanel.Controls.Add(searchLabel, 0, 0);
            searchPanel.Controls.Add(searchTextBox, 1, 0);

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            listView.Columns.Add(Localization.T("Time"), 160);
            listView.Columns.Add(Localization.T("Query"), 500);
            listView.SelectedIndexChanged += ListView_SelectedIndexChanged;
            listView.DoubleClick += (s, e) => SelectAndClose();
            listView.KeyDown += ListView_KeyDown;
            listView.Resize += (s, e) =>
            {
                if (listView.Columns.Count >= 2)
                    listView.Columns[1].Width = Math.Max(120, listView.ClientSize.Width - listView.Columns[0].Width - 4);
            };

            var previewPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var previewLabel = new Label
            {
                Text = Localization.T("SQL:"),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0)
            };

            previewTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9.75f)
            };

            previewPanel.Controls.Add(previewLabel, 0, 0);
            previewPanel.Controls.Add(previewTextBox, 0, 1);

            var buttonPanel = new Panel { Dock = DockStyle.Fill };

            clearButton = new Button
            {
                Text = Localization.T("Clear All"),
                Size = new Size(90, 23),
                Location = new Point(0, 8),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            clearButton.Click += (s, e) => ClearAll();

            selectButton = new Button
            {
                Text = Localization.T("Select"),
                Size = new Size(75, 23),
                Location = new Point(430, 8),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Enabled = false
            };
            selectButton.Click += (s, e) => SelectAndClose();

            editButton = new Button
            {
                Text = Localization.T("Edit"),
                Size = new Size(75, 23),
                Location = new Point(511, 8),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Enabled = false
            };
            editButton.Click += (s, e) => EditSelected();

            deleteButton = new Button
            {
                Text = Localization.T("Delete"),
                Size = new Size(75, 23),
                Location = new Point(592, 8),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Enabled = false
            };
            deleteButton.Click += (s, e) => DeleteSelected();

            var closeButton = new Button
            {
                Text = Localization.T("Close"),
                DialogResult = DialogResult.Cancel,
                Size = new Size(75, 23),
                Location = new Point(673, 8),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };

            buttonPanel.Controls.Add(clearButton);
            buttonPanel.Controls.Add(selectButton);
            buttonPanel.Controls.Add(editButton);
            buttonPanel.Controls.Add(deleteButton);
            buttonPanel.Controls.Add(closeButton);

            // Reposition right-side buttons relative to panel width on resize.
            buttonPanel.Resize += (s, e) =>
            {
                var right = buttonPanel.ClientSize.Width;
                closeButton.Left = right - closeButton.Width;
                deleteButton.Left = closeButton.Left - deleteButton.Width - 6;
                editButton.Left = deleteButton.Left - editButton.Width - 6;
                selectButton.Left = editButton.Left - selectButton.Width - 6;
            };

            root.Controls.Add(searchPanel, 0, 0);
            root.Controls.Add(listView, 0, 1);
            root.Controls.Add(previewPanel, 0, 2);
            root.Controls.Add(buttonPanel, 0, 3);

            Controls.Add(root);

            AcceptButton = selectButton;
            CancelButton = closeButton;

            Shown += (s, e) =>
            {
                RefreshList();
                searchTextBox.Focus();
            };
        }

        void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                SelectAndClose();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                DeleteSelected();
            }
        }

        void ListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = GetSelectedItem();
            var has = item != null;
            selectButton.Enabled = has;
            editButton.Enabled = has;
            deleteButton.Enabled = has;
            previewTextBox.Text = has ? (item.Sql ?? string.Empty) : string.Empty;
        }

        SavedQuery GetSelectedItem()
        {
            if (listView.SelectedItems.Count == 0) return null;
            return listView.SelectedItems[0].Tag as SavedQuery;
        }

        void RefreshList()
        {
            var filter = searchTextBox.Text == null ? string.Empty : searchTextBox.Text.Trim();
            var selected = GetSelectedItem();
            var selectedId = selected != null ? selected.Id : Guid.Empty;

            listView.BeginUpdate();
            listView.Items.Clear();

            var history = QueryLibrary.History;
            if (history != null)
            {
                foreach (var entry in history)
                {
                    if (entry == null) continue;
                    if (!MatchesFilter(entry, filter)) continue;

                    var timeText = AppForm.Settings.FormatDateTime(entry.Timestamp);
                    var preview = QueryLibrary.GetDisplayText(entry, false);
                    var li = new ListViewItem(new[] { timeText, preview })
                    {
                        Tag = entry
                    };
                    listView.Items.Add(li);

                    if (entry.Id == selectedId)
                        li.Selected = true;
                }
            }

            if (listView.Items.Count == 0)
            {
                var empty = string.IsNullOrEmpty(filter)
                    ? Localization.T("(none)")
                    : Localization.T("(no matching history)");
                listView.Items.Add(new ListViewItem(new[] { string.Empty, empty }));
            }

            listView.EndUpdate();

            if (listView.SelectedItems.Count == 0 && listView.Items.Count > 0
                && listView.Items[0].Tag is SavedQuery)
            {
                listView.Items[0].Selected = true;
            }

            ListView_SelectedIndexChanged(listView, EventArgs.Empty);
            clearButton.Enabled = history != null && history.Count > 0;
        }

        static bool MatchesFilter(SavedQuery entry, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;

            if (!string.IsNullOrEmpty(entry.Sql)
                && entry.Sql.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var timeText = AppForm.Settings.FormatDateTime(entry.Timestamp);
            if (!string.IsNullOrEmpty(timeText)
                && timeText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var label = QueryLibrary.GetDisplayText(entry, false);
            return !string.IsNullOrEmpty(label)
                && label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void SelectAndClose()
        {
            var item = GetSelectedItem();
            if (item == null) return;
            SelectedQuery = item;
            DialogResult = DialogResult.OK;
            Close();
        }

        void EditSelected()
        {
            var item = GetSelectedItem();
            if (item == null) return;

            var dialog = new SavedQueryDialog();
            dialog.Text = Localization.T("Edit History Query");
            dialog.ShowName = false;
            dialog.QueryName = item.Name;
            dialog.Sql = item.Sql;

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            item.Sql = dialog.Sql;
            QueryLibrary.UpdateHistory(item);
            RefreshList();
        }

        void DeleteSelected()
        {
            var item = GetSelectedItem();
            if (item == null) return;

            if (MessageBox.Show(this,
                    Localization.T("Delete this query from history?"),
                    Localization.T("Confirm Delete"),
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            QueryLibrary.RemoveHistory(item.Id);
            RefreshList();
        }

        void ClearAll()
        {
            var history = QueryLibrary.History;
            if (history == null || history.Count == 0) return;

            if (MessageBox.Show(this,
                    Localization.T("Clear all query history?"),
                    Localization.T("Confirm Delete"),
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            QueryLibrary.ClearHistory();
            searchTextBox.Clear();
            RefreshList();
        }
    }
}
