using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Data;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Dialog used to pick a retention policy to delete.
    /// </summary>
    public class SelectRetentionPolicyDialog : Form
    {
        ComboBox policyComboBox;
        TextBox detailsTextBox;
        Button okButton;
        Button cancelButton;

        public InfluxDbRetentionPolicy SelectedPolicy
        {
            get { return policyComboBox.SelectedItem as InfluxDbRetentionPolicy; }
        }

        public SelectRetentionPolicyDialog()
        {
            Text = Localization.T("Drop Retention Policy");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 220);

            var selectLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 15),
                Text = Localization.T("Select a retention policy to delete:")
            };

            policyComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(15, 38),
                Size = new Size(490, 21),
                DisplayMember = "Name",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            policyComboBox.SelectedIndexChanged += PolicyComboBox_SelectedIndexChanged;

            var detailsLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 72),
                Text = Localization.T("Details")
            };

            detailsTextBox = new TextBox
            {
                Location = new Point(15, 90),
                Size = new Size(490, 80),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(349, 182),
                Size = new Size(75, 23),
                Text = Localization.T("OK"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(430, 182),
                Size = new Size(75, 23),
                Text = Localization.T("Cancel"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(selectLabel);
            Controls.Add(policyComboBox);
            Controls.Add(detailsLabel);
            Controls.Add(detailsTextBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
        }

        public void Bind(IEnumerable<InfluxDbRetentionPolicy> policies)
        {
            policyComboBox.Items.Clear();
            detailsTextBox.Text = null;

            if (policies != null)
            {
                foreach (var policy in policies)
                    policyComboBox.Items.Add(policy);
            }

            if (policyComboBox.Items.Count > 0)
                policyComboBox.SelectedIndex = 0;

            okButton.Enabled = policyComboBox.SelectedItem != null;
        }

        void PolicyComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var policy = SelectedPolicy;
            detailsTextBox.Text = policy == null ? null : FormatDetails(policy);
            okButton.Enabled = policy != null;
        }

        static string FormatDetails(InfluxDbRetentionPolicy policy)
        {
            return string.Format("{0} {1}\r\n{2} {3}\r\n{4} {5}\r\n{6} {7}",
                Localization.T("Duration:"), policy.Duration,
                Localization.T("Shard Group Duration"), policy.ShardGroupDuration,
                Localization.T("Replica N"), policy.ReplicationCopies,
                Localization.T("Default:"), policy.Default ? Localization.T("Yes") : Localization.T("No"));
        }
    }
}
