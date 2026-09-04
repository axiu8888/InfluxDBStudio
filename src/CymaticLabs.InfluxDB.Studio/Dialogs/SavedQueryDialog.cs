using System;
using System.Drawing;
using System.Windows.Forms;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Dialog used to add or edit a favorite / history query.
    /// </summary>
    public class SavedQueryDialog : Form
    {
        TextBox nameTextBox;
        TextBox sqlTextBox;
        Label nameLabel;
        Button okButton;

        public string QueryName
        {
            get { return nameTextBox.Text == null ? string.Empty : nameTextBox.Text.Trim(); }
            set { nameTextBox.Text = value; }
        }

        public string Sql
        {
            get { return sqlTextBox.Text == null ? string.Empty : sqlTextBox.Text.Trim(); }
            set { sqlTextBox.Text = value; }
        }

        public bool ShowName
        {
            get { return nameTextBox.Visible; }
            set
            {
                nameLabel.Visible = value;
                nameTextBox.Visible = value;
            }
        }

        public SavedQueryDialog()
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            MinimumSize = new Size(480, 280);
            ClientSize = new Size(560, 340);
            Text = Localization.T("Add to Favorites");

            nameLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 15),
                Text = Localization.T("Name (optional):")
            };

            nameTextBox = new TextBox
            {
                Location = new Point(15, 34),
                Size = new Size(530, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var sqlLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 68),
                Text = Localization.T("SQL:")
            };

            sqlTextBox = new TextBox
            {
                Location = new Point(15, 87),
                Size = new Size(530, 200),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                AcceptsReturn = true,
                WordWrap = false,
                Font = new Font("Consolas", 9.75f),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(389, 300),
                Size = new Size(75, 23),
                Text = Localization.T("OK"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            var cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(470, 300),
                Size = new Size(75, 23),
                Text = Localization.T("Cancel"),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.Add(nameLabel);
            Controls.Add(nameTextBox);
            Controls.Add(sqlLabel);
            Controls.Add(sqlTextBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            FormClosing += SavedQueryDialog_FormClosing;
        }

        void SavedQueryDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK) return;
            if (!string.IsNullOrWhiteSpace(Sql)) return;

            AppForm.DisplayError("SQL cannot be blank.");
            e.Cancel = true;
        }
    }
}
