using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Data;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Dialog used to pick a continuous query to delete.
    /// </summary>
    public class SelectContinuousQueryDialog : Form
    {
        ComboBox cqComboBox;
        TextBox queryTextBox;
        Button okButton;
        Button cancelButton;

        public InfluxDbContinuousQuery SelectedCq
        {
            get { return cqComboBox.SelectedItem as InfluxDbContinuousQuery; }
        }

        public SelectContinuousQueryDialog()
        {
            Text = Localization.T("Drop Continuous Query");
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
                Text = Localization.T("Select a continuous query to delete:")
            };

            cqComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(15, 38),
                Size = new Size(490, 21),
                DisplayMember = "Name",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cqComboBox.SelectedIndexChanged += CqComboBox_SelectedIndexChanged;

            var queryLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 72),
                Text = Localization.T("Query:")
            };

            queryTextBox = new TextBox
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
            Controls.Add(cqComboBox);
            Controls.Add(queryLabel);
            Controls.Add(queryTextBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
        }

        public void Bind(IEnumerable<InfluxDbContinuousQuery> queries)
        {
            cqComboBox.Items.Clear();
            queryTextBox.Text = null;

            if (queries != null)
            {
                foreach (var cq in queries)
                    cqComboBox.Items.Add(cq);
            }

            if (cqComboBox.Items.Count > 0)
                cqComboBox.SelectedIndex = 0;

            okButton.Enabled = cqComboBox.SelectedItem != null;
        }

        void CqComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var cq = SelectedCq;
            queryTextBox.Text = cq != null ? cq.Query : null;
            okButton.Enabled = cq != null;
        }
    }
}
