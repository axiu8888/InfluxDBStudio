using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio.Controls;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Selects measurements and deletes points by WHERE time range (and optional tag filters).
    /// </summary>
    public class DeleteMeasurementsDialog : Form
    {
        readonly InfluxDbClient client;
        readonly string database;

        readonly CheckedListBox measurementsList;
        readonly ComboBox retentionPolicyComboBox;
        readonly DateTimePicker fromPicker;
        readonly DateTimePicker toPicker;
        readonly TextBox extraWhereTextBox;
        readonly Label progressLabel;
        readonly Button selectAllButton;
        readonly Button selectNoneButton;
        readonly Button previewSqlButton;
        readonly Button deleteButton;
        readonly Button cancelButton;

        CancellationTokenSource deleteCts;
        bool deleteRunning;

        public DeleteMeasurementsDialog(
            InfluxDbClient client,
            string databaseName,
            IEnumerable<string> measurements,
            IEnumerable<InfluxDbRetentionPolicy> policies)
        {
            this.client = client;
            database = databaseName;

            Text = Localization.T("Delete Measurements");
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            ControlBox = true;
            MinimumSize = new Size(560, 580);
            ClientSize = new Size(620, 560);

            var databaseLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 14),
                Text = Localization.T("Database:")
            };
            var databaseValue = new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(120, 14),
                Text = databaseName ?? "-"
            };

            var measurementsLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 42),
                Text = Localization.T("Measurements:")
            };

            selectAllButton = new Button
            {
                Location = new Point(420, 38),
                Size = new Size(90, 23),
                Text = Localization.T("Select All"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            selectAllButton.Click += (s, e) => SetAllChecked(true);

            selectNoneButton = new Button
            {
                Location = new Point(516, 38),
                Size = new Size(90, 23),
                Text = Localization.T("Select None"),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            selectNoneButton.Click += (s, e) => SetAllChecked(false);

            measurementsList = new CheckedListBox
            {
                Location = new Point(12, 68),
                Size = new Size(594, 160),
                CheckOnClick = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var rpLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 242),
                Text = Localization.T("Retention Policy:")
            };
            retentionPolicyComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 239),
                Size = new Size(486, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = false
            };
            var rpHint = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(120, 264),
                Text = Localization.T("(InfluxDB DELETE does not support retention policy; uses database default)")
            };

            var fromLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 296),
                Text = Localization.T("From:")
            };
            fromPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = AppForm.Settings.DateTimePickerFormat,
                Location = new Point(120, 292),
                Size = new Size(200, 21),
                ShowUpDown = true
            };

            var toLabel = new Label
            {
                AutoSize = true,
                Location = new Point(340, 296),
                Text = Localization.T("To:")
            };
            toPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = AppForm.Settings.DateTimePickerFormat,
                Location = new Point(380, 292),
                Size = new Size(200, 21),
                ShowUpDown = true
            };

            var now = DateTime.Now;
            toPicker.Value = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            fromPicker.Value = toPicker.Value.AddDays(-1);

            var extraLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 330),
                Text = Localization.T("Extra WHERE:")
            };
            extraWhereTextBox = new TextBox
            {
                Location = new Point(120, 327),
                Size = new Size(486, 72),
                Multiline = true,
                AcceptsReturn = true,
                AcceptsTab = false,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var extraHint = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(120, 404),
                Text = Localization.T("(optional tag filters only; fields are not supported by DELETE)")
            };

            var warnLabel = new Label
            {
                AutoSize = false,
                ForeColor = Color.DarkRed,
                Location = new Point(12, 430),
                Size = new Size(594, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = Localization.T("Warning: deleted points cannot be recovered. DROP MEASUREMENT is not used; only matching points are removed.")
            };

            progressLabel = new Label
            {
                AutoSize = false,
                Location = new Point(12, 472),
                Size = new Size(594, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text = string.Empty
            };

            previewSqlButton = new Button
            {
                Location = new Point(324, 518),
                Size = new Size(100, 28),
                Text = Localization.T("Preview SQL"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            previewSqlButton.Click += (s, e) => PreviewSqlButton_Click();

            deleteButton = new Button
            {
                Location = new Point(430, 518),
                Size = new Size(90, 28),
                Text = Localization.T("Delete"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            deleteButton.Click += async (s, e) => await DeleteButton_Click();

            cancelButton = new Button
            {
                Location = new Point(526, 518),
                Size = new Size(80, 28),
                Text = Localization.T("Cancel"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            cancelButton.Click += CancelButton_Click;

            CancelButton = cancelButton;

            Controls.Add(databaseLabel);
            Controls.Add(databaseValue);
            Controls.Add(measurementsLabel);
            Controls.Add(selectAllButton);
            Controls.Add(selectNoneButton);
            Controls.Add(measurementsList);
            Controls.Add(rpLabel);
            Controls.Add(retentionPolicyComboBox);
            Controls.Add(rpHint);
            Controls.Add(fromLabel);
            Controls.Add(fromPicker);
            Controls.Add(toLabel);
            Controls.Add(toPicker);
            Controls.Add(extraLabel);
            Controls.Add(extraWhereTextBox);
            Controls.Add(extraHint);
            Controls.Add(warnLabel);
            Controls.Add(progressLabel);
            Controls.Add(previewSqlButton);
            Controls.Add(deleteButton);
            Controls.Add(cancelButton);

            if (measurements != null)
            {
                foreach (var name in measurements.OrderBy(m => m, StringComparer.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        measurementsList.Items.Add(name);
                }
            }

            LoadRetentionPolicies(policies);
            Localization.Apply(this);

            var pickerFormat = AppForm.Settings.DateTimePickerFormat;
            fromPicker.Format = DateTimePickerFormat.Custom;
            fromPicker.CustomFormat = pickerFormat;
            toPicker.Format = DateTimePickerFormat.Custom;
            toPicker.CustomFormat = pickerFormat;
        }

        void LoadRetentionPolicies(IEnumerable<InfluxDbRetentionPolicy> policies)
        {
            retentionPolicyComboBox.Items.Clear();

            // DELETE cannot target a retention policy; keep a disabled placeholder.
            retentionPolicyComboBox.Items.Add(new RetentionPolicyItem
            {
                IsDefaultOption = true,
                Name = Localization.T("(Database default)")
            });
            retentionPolicyComboBox.SelectedIndex = 0;
        }

        void SetAllChecked(bool value)
        {
            for (var i = 0; i < measurementsList.Items.Count; i++)
                measurementsList.SetItemChecked(i, value);
        }

        void CancelButton_Click(object sender, EventArgs e)
        {
            if (deleteRunning && deleteCts != null)
            {
                deleteCts.Cancel();
                progressLabel.Text = Localization.T("Cancelling...");
                return;
            }

            DialogResult = DialogResult.Cancel;
            Close();
        }

        void PreviewSqlButton_Click()
        {
            MeasurementBatchDelete.Options options;
            string error;
            if (!TryBuildOptions(out options, out error))
            {
                MessageBox.Show(this, error, Localization.T("Preview SQL"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < options.Measurements.Count; i++)
            {
                if (i > 0) sb.AppendLine().AppendLine();
                sb.Append(MeasurementBatchDelete.BuildDeleteStatement(
                    options.Measurements[i],
                    options.RetentionPolicyName,
                    options.FromLocal,
                    options.ToLocal,
                    options.ExtraWhere));
                sb.Append(';');
            }

            using (var dialog = new Form())
            {
                dialog.Text = Localization.T("Preview SQL");
                dialog.FormBorderStyle = FormBorderStyle.Sizable;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = true;
                dialog.ShowInTaskbar = false;
                dialog.MinimumSize = new Size(560, 320);
                dialog.ClientSize = new Size(680, 420);

                var sqlBox = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap = false,
                    Font = new Font("Consolas", 9.75f),
                    Dock = DockStyle.Fill,
                    Text = sb.ToString()
                };

                var buttonPanel = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 44
                };

                var copyButton = new Button
                {
                    Text = Localization.T("Copy"),
                    Size = new Size(80, 28),
                    Location = new Point(480, 8),
                    Anchor = AnchorStyles.Right | AnchorStyles.Top
                };
                copyButton.Click += (s, e) =>
                {
                    try
                    {
                        Clipboard.SetText(sqlBox.Text ?? string.Empty);
                    }
                    catch
                    {
                        Clipboard.SetDataObject(sqlBox.Text ?? string.Empty, true);
                    }
                };

                var closeButton = new Button
                {
                    Text = Localization.T("Close"),
                    DialogResult = DialogResult.OK,
                    Size = new Size(80, 28),
                    Location = new Point(566, 8),
                    Anchor = AnchorStyles.Right | AnchorStyles.Top
                };

                buttonPanel.Resize += (s, e) =>
                {
                    closeButton.Left = buttonPanel.ClientSize.Width - closeButton.Width - 12;
                    copyButton.Left = closeButton.Left - copyButton.Width - 6;
                };

                buttonPanel.Controls.Add(copyButton);
                buttonPanel.Controls.Add(closeButton);
                dialog.Controls.Add(sqlBox);
                dialog.Controls.Add(buttonPanel);
                dialog.AcceptButton = closeButton;
                dialog.CancelButton = closeButton;

                dialog.ShowDialog(this);
            }
        }

        async Task DeleteButton_Click()
        {
            if (deleteRunning) return;

            MeasurementBatchDelete.Options options;
            string error;
            if (!TryBuildOptions(out options, out error))
            {
                MessageBox.Show(this, error, Localization.T("Delete Measurements"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var fromDisplay = options.FromLocal.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(options.FromLocal, DateTimeKind.Local)
                : options.FromLocal;
            var toDisplay = options.ToLocal.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(options.ToLocal, DateTimeKind.Local)
                : options.ToLocal;

            var confirm = Localization.Format(
                "Permanently delete matching points from {0} measurement(s) in database '{1}'?\n\nFrom: {2}\nTo: {3}\n\nThis cannot be undone.",
                options.Measurements.Count,
                database,
                AppForm.Settings.FormatDateTime(fromDisplay),
                AppForm.Settings.FormatDateTime(toDisplay));

            if (MessageBox.Show(this, confirm, Localization.T("Confirm Delete"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            deleteRunning = true;
            deleteCts = new CancellationTokenSource();
            SetUiEnabled(false);
            cancelButton.Text = Localization.T("Cancel");
            progressLabel.Text = Localization.T("Starting...");

            try
            {
                var progress = new Progress<MeasurementBatchDelete.ProgressUpdate>(u =>
                {
                    progressLabel.Text = Localization.Format(
                        "[{0}/{1}] {2}",
                        u.Index,
                        u.Total,
                        u.Message ?? u.Measurement ?? string.Empty);
                });

                var result = await MeasurementBatchDelete.RunAsync(client, options, progress, deleteCts.Token)
                    .ConfigureAwait(true);

                var summary = Localization.Format(
                    "Done. Succeeded: {0}, failed: {1}.",
                    result.Succeeded,
                    result.Failed);

                if (result.Errors != null && result.Errors.Count > 0)
                {
                    var detail = string.Join(Environment.NewLine, result.Errors.Take(10));
                    if (result.Errors.Count > 10)
                        detail += Environment.NewLine + "...";
                    summary += Environment.NewLine + Environment.NewLine + detail;
                }

                progressLabel.Text = summary;
                MessageBox.Show(this, summary, Localization.T("Delete Measurements"),
                    MessageBoxButtons.OK,
                    result.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                progressLabel.Text = Localization.T("Delete cancelled.");
            }
            catch (Exception ex)
            {
                progressLabel.Text = Localization.T("Delete failed.");
                AppForm.DisplayException(ex);
            }
            finally
            {
                deleteRunning = false;
                if (deleteCts != null)
                {
                    deleteCts.Dispose();
                    deleteCts = null;
                }
                SetUiEnabled(true);
                cancelButton.Text = Localization.T("Close");
            }
        }

        bool TryBuildOptions(out MeasurementBatchDelete.Options options, out string error)
        {
            options = null;
            error = null;

            var selected = new List<string>();
            foreach (var item in measurementsList.CheckedItems)
            {
                var name = item as string;
                if (!string.IsNullOrWhiteSpace(name)) selected.Add(name);
            }

            if (selected.Count == 0)
            {
                error = Localization.T("Please select at least one measurement.");
                return false;
            }

            if (fromPicker.Value >= toPicker.Value)
            {
                error = Localization.T("From time must be earlier than To time.");
                return false;
            }

            options = new MeasurementBatchDelete.Options
            {
                Database = database,
                Measurements = selected,
                // InfluxDB DELETE ignores / rejects retention policy in FROM.
                RetentionPolicyName = null,
                FromLocal = fromPicker.Value,
                ToLocal = toPicker.Value,
                ExtraWhere = extraWhereTextBox.Text
            };
            return true;
        }

        void SetUiEnabled(bool enabled)
        {
            measurementsList.Enabled = enabled;
            // Retention policy stays disabled: DELETE does not support it.
            fromPicker.Enabled = enabled;
            toPicker.Enabled = enabled;
            extraWhereTextBox.Enabled = enabled;
            selectAllButton.Enabled = enabled;
            selectNoneButton.Enabled = enabled;
            previewSqlButton.Enabled = enabled;
            deleteButton.Enabled = enabled;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (deleteRunning)
            {
                if (deleteCts != null) deleteCts.Cancel();
                e.Cancel = true;
                progressLabel.Text = Localization.T("Cancelling...");
                return;
            }
            base.OnFormClosing(e);
        }

        sealed class RetentionPolicyItem
        {
            public string Name { get; set; }
            public bool IsDefaultOption { get; set; }
            public bool IsDefault { get; set; }

            public override string ToString()
            {
                if (IsDefaultOption) return Name;
                var suffix = IsDefault ? Localization.T(" (default)") : string.Empty;
                return string.Format("{0}{1}", Name, suffix);
            }
        }
    }
}
