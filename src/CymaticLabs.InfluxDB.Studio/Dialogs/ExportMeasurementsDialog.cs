using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio.Controls;
using CymaticLabs.InfluxDB.Studio.Properties;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Selects measurements and exports them by WHERE time range (and optional filters) to files named after each measurement.
    /// </summary>
    public class ExportMeasurementsDialog : Form
    {
        readonly InfluxDbClient client;
        readonly string database;

        readonly CheckedListBox measurementsList;
        readonly ComboBox retentionPolicyComboBox;
        readonly DateTimePicker fromPicker;
        readonly DateTimePicker toPicker;
        readonly TextBox extraWhereTextBox;
        readonly TextBox limitTextBox;
        readonly CheckBox csvCheckBox;
        readonly CheckBox excelCheckBox;
        readonly CheckBox jsonCheckBox;
        readonly CheckBox lineProtocolCheckBox;
        readonly TextBox outputDirTextBox;
        readonly Label progressLabel;
        readonly Button selectAllButton;
        readonly Button selectNoneButton;
        readonly Button browseButton;
        readonly Button exportButton;
        readonly Button cancelButton;

        CancellationTokenSource exportCts;
        bool exportRunning;

        public ExportMeasurementsDialog(
            InfluxDbClient client,
            string databaseName,
            IEnumerable<string> measurements,
            IEnumerable<InfluxDbRetentionPolicy> policies)
        {
            this.client = client;
            database = databaseName;

            Text = Localization.T("Export Measurements");
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            ControlBox = true;
            MinimumSize = new Size(560, 660);
            ClientSize = new Size(620, 640);

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
                Size = new Size(594, 180),
                CheckOnClick = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var rpLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 262),
                Text = Localization.T("Retention Policy:")
            };
            retentionPolicyComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 259),
                Size = new Size(486, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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
            // Default range: last 24 hours, second precision for display/input.
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
                Text = Localization.T("(optional filters only; multiple lines OK, e.g. host = 'server01')")
            };

            var limitLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 432),
                Text = Localization.T("LIMIT:")
            };
            limitTextBox = new TextBox
            {
                Location = new Point(120, 429),
                Size = new Size(100, 21)
            };
            var limitHint = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(230, 432),
                Text = Localization.T("(optional; leave blank for no LIMIT)")
            };

            var formatsLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 466),
                Text = Localization.T("Formats:")
            };
            lineProtocolCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(120, 465),
                Text = "Line Protocol",
                Checked = true
            };
            jsonCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(230, 465),
                Text = "JSON"
            };
            excelCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(295, 465),
                Text = "Excel"
            };
            csvCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(365, 465),
                Text = "CSV"
            };

            var outputLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 500),
                Text = Localization.T("Output folder:")
            };
            outputDirTextBox = new TextBox
            {
                Location = new Point(120, 497),
                Size = new Size(400, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            browseButton = new Button
            {
                Location = new Point(526, 495),
                Size = new Size(80, 23),
                Text = Localization.T("Browse..."),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            browseButton.Click += BrowseButton_Click;

            progressLabel = new Label
            {
                AutoSize = false,
                Location = new Point(12, 536),
                Size = new Size(594, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = string.Empty
            };

            exportButton = new Button
            {
                Location = new Point(430, 592),
                Size = new Size(90, 28),
                Text = Localization.T("Export"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            exportButton.Click += async (s, e) => await ExportButton_Click();

            cancelButton = new Button
            {
                Location = new Point(526, 592),
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
            Controls.Add(fromLabel);
            Controls.Add(fromPicker);
            Controls.Add(toLabel);
            Controls.Add(toPicker);
            Controls.Add(extraLabel);
            Controls.Add(extraWhereTextBox);
            Controls.Add(extraHint);
            Controls.Add(limitLabel);
            Controls.Add(limitTextBox);
            Controls.Add(limitHint);
            Controls.Add(formatsLabel);
            Controls.Add(lineProtocolCheckBox);
            Controls.Add(jsonCheckBox);
            Controls.Add(excelCheckBox);
            Controls.Add(csvCheckBox);
            Controls.Add(outputLabel);
            Controls.Add(outputDirTextBox);
            Controls.Add(browseButton);
            Controls.Add(progressLabel);
            Controls.Add(exportButton);
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

            // Follow global Settings date/time format (same as Backfill dialog).
            var pickerFormat = AppForm.Settings.DateTimePickerFormat;
            fromPicker.Format = DateTimePickerFormat.Custom;
            fromPicker.CustomFormat = pickerFormat;
            toPicker.Format = DateTimePickerFormat.Custom;
            toPicker.CustomFormat = pickerFormat;

            RestoreLastExportDirectory();
        }

        void RestoreLastExportDirectory()
        {
            var last = Settings.Default.LastMeasurementExportDirectory;
            if (!string.IsNullOrWhiteSpace(last) && Directory.Exists(last))
                outputDirTextBox.Text = last;
        }

        static void RememberExportDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.Trim();
            if (!Directory.Exists(path)) return;

            if (string.Equals(Settings.Default.LastMeasurementExportDirectory, path, StringComparison.OrdinalIgnoreCase))
                return;

            Settings.Default.LastMeasurementExportDirectory = path;
            Settings.Default.Save();
        }

        void LoadRetentionPolicies(IEnumerable<InfluxDbRetentionPolicy> policies)
        {
            retentionPolicyComboBox.Items.Clear();

            var defaultOption = new RetentionPolicyItem
            {
                IsDefaultOption = true,
                Name = Localization.T("(Database default)")
            };
            retentionPolicyComboBox.Items.Add(defaultOption);

            InfluxDbRetentionPolicy defaultPolicy = null;
            if (policies != null)
            {
                foreach (var policy in policies.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (policy == null || string.IsNullOrWhiteSpace(policy.Name)) continue;
                    retentionPolicyComboBox.Items.Add(new RetentionPolicyItem
                    {
                        Name = policy.Name,
                        IsDefault = policy.Default
                    });
                    if (policy.Default) defaultPolicy = policy;
                }
            }

            if (defaultPolicy != null)
            {
                for (var i = 0; i < retentionPolicyComboBox.Items.Count; i++)
                {
                    var item = retentionPolicyComboBox.Items[i] as RetentionPolicyItem;
                    if (item != null && !item.IsDefaultOption && item.Name == defaultPolicy.Name)
                    {
                        retentionPolicyComboBox.SelectedIndex = i;
                        return;
                    }
                }
            }

            retentionPolicyComboBox.SelectedIndex = 0;
        }

        void SetAllChecked(bool value)
        {
            for (var i = 0; i < measurementsList.Items.Count; i++)
                measurementsList.SetItemChecked(i, value);
        }

        void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = Localization.T("Select output folder");
                if (!string.IsNullOrWhiteSpace(outputDirTextBox.Text) && Directory.Exists(outputDirTextBox.Text))
                    dialog.SelectedPath = outputDirTextBox.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    outputDirTextBox.Text = dialog.SelectedPath;
                    RememberExportDirectory(dialog.SelectedPath);
                }
            }
        }

        void CancelButton_Click(object sender, EventArgs e)
        {
            if (exportRunning && exportCts != null)
            {
                exportCts.Cancel();
                progressLabel.Text = Localization.T("Cancelling...");
                return;
            }

            DialogResult = DialogResult.Cancel;
            Close();
        }

        async Task ExportButton_Click()
        {
            if (exportRunning) return;

            MeasurementBatchExport.Options options;
            string error;
            if (!TryBuildOptions(out options, out error))
            {
                MessageBox.Show(this, error, Localization.T("Export Measurements"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RememberExportDirectory(options.OutputDirectory);

            exportRunning = true;
            exportCts = new CancellationTokenSource();
            SetUiEnabled(false);
            cancelButton.Text = Localization.T("Cancel");
            progressLabel.Text = Localization.T("Starting...");

            try
            {
                var progress = new Progress<MeasurementBatchExport.ProgressUpdate>(u =>
                {
                    progressLabel.Text = Localization.Format(
                        "[{0}/{1}] {2}",
                        u.Index,
                        u.Total,
                        u.Message ?? u.Measurement ?? string.Empty);
                });

                var result = await MeasurementBatchExport.RunAsync(client, options, progress, exportCts.Token)
                    .ConfigureAwait(true);

                var summary = Localization.Format(
                    "Done. Succeeded: {0}, empty skipped: {1}, failed: {2}.",
                    result.Succeeded,
                    result.SkippedEmpty,
                    result.Failed);

                if (result.Errors != null && result.Errors.Count > 0)
                {
                    var detail = string.Join(Environment.NewLine, result.Errors.Take(10));
                    if (result.Errors.Count > 10)
                        detail += Environment.NewLine + "...";
                    summary += Environment.NewLine + Environment.NewLine + detail;
                }

                progressLabel.Text = summary;
                MessageBox.Show(this, summary, Localization.T("Export Measurements"),
                    MessageBoxButtons.OK,
                    result.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                progressLabel.Text = Localization.T("Export cancelled.");
            }
            catch (Exception ex)
            {
                progressLabel.Text = Localization.T("Export failed.");
                AppForm.DisplayException(ex);
            }
            finally
            {
                exportRunning = false;
                if (exportCts != null)
                {
                    exportCts.Dispose();
                    exportCts = null;
                }
                SetUiEnabled(true);
                cancelButton.Text = Localization.T("Close");
            }
        }

        bool TryBuildOptions(out MeasurementBatchExport.Options options, out string error)
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

            var formats = MeasurementBatchExport.Formats.None;
            if (csvCheckBox.Checked) formats |= MeasurementBatchExport.Formats.Csv;
            if (excelCheckBox.Checked) formats |= MeasurementBatchExport.Formats.Excel;
            if (jsonCheckBox.Checked) formats |= MeasurementBatchExport.Formats.Json;
            if (lineProtocolCheckBox.Checked) formats |= MeasurementBatchExport.Formats.LineProtocol;

            if (formats == MeasurementBatchExport.Formats.None)
            {
                error = Localization.T("Please select at least one export format.");
                return false;
            }

            var outputDir = outputDirTextBox.Text != null ? outputDirTextBox.Text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                error = Localization.T("Please select an output folder.");
                return false;
            }

            int? limit = null;
            var limitText = limitTextBox.Text != null ? limitTextBox.Text.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(limitText))
            {
                int parsed;
                if (!int.TryParse(limitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed <= 0)
                {
                    error = Localization.T("LIMIT must be a positive integer, or left blank.");
                    return false;
                }
                limit = parsed;
            }

            string rp = null;
            var rpItem = retentionPolicyComboBox.SelectedItem as RetentionPolicyItem;
            if (rpItem != null && !rpItem.IsDefaultOption)
                rp = rpItem.Name;

            options = new MeasurementBatchExport.Options
            {
                Database = database,
                Measurements = selected,
                RetentionPolicyName = rp,
                FromLocal = fromPicker.Value,
                ToLocal = toPicker.Value,
                ExtraWhere = extraWhereTextBox.Text,
                Limit = limit,
                Formats = formats,
                OutputDirectory = outputDir
            };
            return true;
        }

        void SetUiEnabled(bool enabled)
        {
            measurementsList.Enabled = enabled;
            retentionPolicyComboBox.Enabled = enabled;
            fromPicker.Enabled = enabled;
            toPicker.Enabled = enabled;
            extraWhereTextBox.Enabled = enabled;
            limitTextBox.Enabled = enabled;
            csvCheckBox.Enabled = enabled;
            excelCheckBox.Enabled = enabled;
            jsonCheckBox.Enabled = enabled;
            lineProtocolCheckBox.Enabled = enabled;
            outputDirTextBox.Enabled = enabled;
            browseButton.Enabled = enabled;
            selectAllButton.Enabled = enabled;
            selectNoneButton.Enabled = enabled;
            exportButton.Enabled = enabled;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (exportRunning)
            {
                if (exportCts != null) exportCts.Cancel();
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
