using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Data;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Dialog for importing a Line Protocol file into a database.
    /// </summary>
    public class ImportLineProtocolDialog : Form
    {
        readonly Label databaseValue;
        readonly TextBox fileTextBox;
        readonly ComboBox retentionPolicyComboBox;
        readonly ComboBox consistencyComboBox;
        readonly Button browseButton;
        readonly Button importButton;
        readonly Button cancelButton;

        /// <summary>Selected .lp file path.</summary>
        public string FilePath
        {
            get { return fileTextBox.Text != null ? fileTextBox.Text.Trim() : string.Empty; }
        }

        /// <summary>
        /// Retention policy name to write to, or null/empty for the database default.
        /// </summary>
        public string RetentionPolicyName
        {
            get
            {
                var item = retentionPolicyComboBox.SelectedItem as RetentionPolicyItem;
                if (item == null || item.IsDefaultOption) return null;
                return item.Name;
            }
        }

        /// <summary>Write consistency: any, one, quorum, or all.</summary>
        public string Consistency
        {
            get
            {
                var value = consistencyComboBox.SelectedItem as string;
                return string.IsNullOrWhiteSpace(value) ? "all" : value;
            }
        }

        public ImportLineProtocolDialog(string databaseName, IEnumerable<InfluxDbRetentionPolicy> policies)
        {
            Text = Localization.T("Import Line Protocol");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ControlBox = true;
            ClientSize = new Size(520, 200);

            var databaseLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 18),
                Text = Localization.T("Database:")
            };

            databaseValue = new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Location = new Point(120, 18),
                Text = databaseName ?? "-"
            };

            var fileLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 52),
                Text = Localization.T("File:")
            };

            fileTextBox = new TextBox
            {
                Location = new Point(120, 49),
                Size = new Size(300, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            browseButton = new Button
            {
                Location = new Point(426, 47),
                Size = new Size(75, 23),
                Text = Localization.T("Browse..."),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            browseButton.Click += BrowseButton_Click;

            var rpLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 86),
                Text = Localization.T("Retention Policy:")
            };

            retentionPolicyComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 83),
                Size = new Size(381, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var consistencyLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 120),
                Text = Localization.T("Consistency:")
            };

            consistencyComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 117),
                Size = new Size(150, 21)
            };
            consistencyComboBox.Items.AddRange(new object[] { "all", "one", "any", "quorum" });
            consistencyComboBox.SelectedItem = "all";

            importButton = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(345, 160),
                Size = new Size(75, 23),
                Text = Localization.T("Import"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            importButton.Click += ImportButton_Click;

            cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(426, 160),
                Size = new Size(75, 23),
                Text = Localization.T("Cancel"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            AcceptButton = importButton;
            CancelButton = cancelButton;

            Controls.Add(databaseLabel);
            Controls.Add(databaseValue);
            Controls.Add(fileLabel);
            Controls.Add(fileTextBox);
            Controls.Add(browseButton);
            Controls.Add(rpLabel);
            Controls.Add(retentionPolicyComboBox);
            Controls.Add(consistencyLabel);
            Controls.Add(consistencyComboBox);
            Controls.Add(importButton);
            Controls.Add(cancelButton);

            LoadRetentionPolicies(policies);
            Localization.Apply(this);
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
                        IsDefault = policy.Default,
                        Duration = policy.Duration,
                        ReplicationCopies = policy.ReplicationCopies
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

        void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = Localization.T("Line Protocol files|*.lp|All files|*.*");
                dialog.Title = Localization.T("Import Line Protocol...");
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (!string.IsNullOrWhiteSpace(fileTextBox.Text) && File.Exists(fileTextBox.Text))
                    dialog.FileName = fileTextBox.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    fileTextBox.Text = dialog.FileName;
            }
        }

        void ImportButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                MessageBox.Show(this,
                    Localization.T("Please select a Line Protocol file."),
                    Localization.T("Import Line Protocol"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            if (!File.Exists(FilePath))
            {
                MessageBox.Show(this,
                    Localization.T("The selected file does not exist."),
                    Localization.T("Import Line Protocol"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        sealed class RetentionPolicyItem
        {
            public string Name { get; set; }
            public bool IsDefaultOption { get; set; }
            public bool IsDefault { get; set; }
            public string Duration { get; set; }
            public int ReplicationCopies { get; set; }

            public override string ToString()
            {
                if (IsDefaultOption) return Name;

                var suffix = IsDefault
                    ? Localization.T(" (default)")
                    : string.Empty;
                return string.Format("{0}{1}", Name, suffix);
            }
        }
    }
}
