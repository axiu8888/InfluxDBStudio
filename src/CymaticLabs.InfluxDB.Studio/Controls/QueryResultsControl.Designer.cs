namespace CymaticLabs.InfluxDB.Studio.Controls
{
    partial class QueryResultsControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copySelectionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyRowsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyRowsJsonToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyRowsKeyValueToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyHeaderToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyAllHeadersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copySeparatorToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.exportAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAllCsvToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAllExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.jSONToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportAllLineProtocolToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedCsvToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.jSONToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedLineProtocolToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.tagsTextBox = new System.Windows.Forms.TextBox();
            this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.contextMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridView.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.Disable;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.ContextMenuStrip = this.contextMenuStrip;
            this.dataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView.EnableHeadersVisualStyles = true;
            this.dataGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.Single;
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.MultiSelect = true;
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.RowHeadersWidth = 54;
            this.dataGridView.RowTemplate.Height = 23;
            this.dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridView.ShowCellToolTips = true;
            this.dataGridView.Size = new System.Drawing.Size(688, 429);
            this.dataGridView.TabIndex = 0;
            this.dataGridView.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_ColumnHeaderMouseClick);
            this.dataGridView.RowHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_RowHeaderMouseClick);
            this.dataGridView.KeyDown += new System.Windows.Forms.KeyEventHandler(this.dataGridView_KeyDown);
            this.dataGridView.MouseDown += new System.Windows.Forms.MouseEventHandler(this.dataGridView_MouseDown);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copySelectionToolStripMenuItem,
            this.copyRowsToolStripMenuItem,
            this.copyHeaderToolStripMenuItem,
            this.copyAllHeadersToolStripMenuItem,
            this.copySeparatorToolStripMenuItem,
            this.exportAllToolStripMenuItem,
            this.exportSelectedToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(180, 164);
            this.contextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip_Opening);
            // 
            // copySelectionToolStripMenuItem
            // 
            this.copySelectionToolStripMenuItem.Name = "copySelectionToolStripMenuItem";
            this.copySelectionToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.copySelectionToolStripMenuItem.Text = "Copy";
            this.copySelectionToolStripMenuItem.Click += new System.EventHandler(this.copySelectionToolStripMenuItem_Click);
            // 
            // copyRowsToolStripMenuItem
            // 
            this.copyRowsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyRowsJsonToolStripMenuItem,
            this.copyRowsKeyValueToolStripMenuItem});
            this.copyRowsToolStripMenuItem.Name = "copyRowsToolStripMenuItem";
            this.copyRowsToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.copyRowsToolStripMenuItem.Text = "Copy Row(s)";
            // 
            // copyRowsJsonToolStripMenuItem
            // 
            this.copyRowsJsonToolStripMenuItem.Name = "copyRowsJsonToolStripMenuItem";
            this.copyRowsJsonToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.copyRowsJsonToolStripMenuItem.Text = "Copy as JSON";
            this.copyRowsJsonToolStripMenuItem.Click += new System.EventHandler(this.copyRowsJsonToolStripMenuItem_Click);
            // 
            // copyRowsKeyValueToolStripMenuItem
            // 
            this.copyRowsKeyValueToolStripMenuItem.Name = "copyRowsKeyValueToolStripMenuItem";
            this.copyRowsKeyValueToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.copyRowsKeyValueToolStripMenuItem.Text = "Copy as Key=Value";
            this.copyRowsKeyValueToolStripMenuItem.Click += new System.EventHandler(this.copyRowsKeyValueToolStripMenuItem_Click);
            // 
            // copyHeaderToolStripMenuItem
            // 
            this.copyHeaderToolStripMenuItem.Name = "copyHeaderToolStripMenuItem";
            this.copyHeaderToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.copyHeaderToolStripMenuItem.Text = "Copy Header";
            this.copyHeaderToolStripMenuItem.Click += new System.EventHandler(this.copyHeaderToolStripMenuItem_Click);
            // 
            // copyAllHeadersToolStripMenuItem
            // 
            this.copyAllHeadersToolStripMenuItem.Name = "copyAllHeadersToolStripMenuItem";
            this.copyAllHeadersToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.copyAllHeadersToolStripMenuItem.Text = "Copy All Headers";
            this.copyAllHeadersToolStripMenuItem.Click += new System.EventHandler(this.copyAllHeadersToolStripMenuItem_Click);
            // 
            // copySeparatorToolStripMenuItem
            // 
            this.copySeparatorToolStripMenuItem.Name = "copySeparatorToolStripMenuItem";
            this.copySeparatorToolStripMenuItem.Size = new System.Drawing.Size(176, 6);
            // 
            // exportAllToolStripMenuItem
            // 
            this.exportAllToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportAllCsvToolStripMenuItem,
            this.exportAllExcelToolStripMenuItem,
            this.jSONToolStripMenuItem,
            this.exportAllLineProtocolToolStripMenuItem});
            this.exportAllToolStripMenuItem.Name = "exportAllToolStripMenuItem";
            this.exportAllToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.exportAllToolStripMenuItem.Text = "Export All";
            // 
            // exportAllCsvToolStripMenuItem
            // 
            this.exportAllCsvToolStripMenuItem.Name = "exportAllCsvToolStripMenuItem";
            this.exportAllCsvToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.exportAllCsvToolStripMenuItem.Text = "CSV";
            this.exportAllCsvToolStripMenuItem.Click += new System.EventHandler(this.exportAllCsvToolStripMenuItem_Click);
            // 
            // exportAllExcelToolStripMenuItem
            // 
            this.exportAllExcelToolStripMenuItem.Name = "exportAllExcelToolStripMenuItem";
            this.exportAllExcelToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.exportAllExcelToolStripMenuItem.Text = "Excel";
            this.exportAllExcelToolStripMenuItem.Click += new System.EventHandler(this.exportAllExcelToolStripMenuItem_Click);
            // 
            // jSONToolStripMenuItem
            // 
            this.jSONToolStripMenuItem.Name = "jSONToolStripMenuItem";
            this.jSONToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.jSONToolStripMenuItem.Text = "JSON";
            this.jSONToolStripMenuItem.Click += new System.EventHandler(this.jSONToolStripMenuItem_Click);
            // 
            // exportAllLineProtocolToolStripMenuItem
            // 
            this.exportAllLineProtocolToolStripMenuItem.Name = "exportAllLineProtocolToolStripMenuItem";
            this.exportAllLineProtocolToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.exportAllLineProtocolToolStripMenuItem.Text = "Line Protocol";
            this.exportAllLineProtocolToolStripMenuItem.Click += new System.EventHandler(this.exportAllLineProtocolToolStripMenuItem_Click);
            // 
            // exportSelectedToolStripMenuItem
            // 
            this.exportSelectedToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportSelectedCsvToolStripMenuItem,
            this.exportSelectedExcelToolStripMenuItem,
            this.jSONToolStripMenuItem1,
            this.exportSelectedLineProtocolToolStripMenuItem});
            this.exportSelectedToolStripMenuItem.Name = "exportSelectedToolStripMenuItem";
            this.exportSelectedToolStripMenuItem.Size = new System.Drawing.Size(179, 22);
            this.exportSelectedToolStripMenuItem.Text = "Export Selected";
            // 
            // exportSelectedCsvToolStripMenuItem
            // 
            this.exportSelectedCsvToolStripMenuItem.Name = "exportSelectedCsvToolStripMenuItem";
            this.exportSelectedCsvToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.exportSelectedCsvToolStripMenuItem.Text = "CSV";
            this.exportSelectedCsvToolStripMenuItem.Click += new System.EventHandler(this.exportSelectedCsvToolStripMenuItem_Click);
            // 
            // exportSelectedExcelToolStripMenuItem
            // 
            this.exportSelectedExcelToolStripMenuItem.Name = "exportSelectedExcelToolStripMenuItem";
            this.exportSelectedExcelToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.exportSelectedExcelToolStripMenuItem.Text = "Excel";
            this.exportSelectedExcelToolStripMenuItem.Click += new System.EventHandler(this.exportSelectedExcelToolStripMenuItem_Click);
            // 
            // jSONToolStripMenuItem1
            // 
            this.jSONToolStripMenuItem1.Name = "jSONToolStripMenuItem1";
            this.jSONToolStripMenuItem1.Size = new System.Drawing.Size(152, 22);
            this.jSONToolStripMenuItem1.Text = "JSON";
            this.jSONToolStripMenuItem1.Click += new System.EventHandler(this.jSONToolStripMenuItem1_Click);
            // 
            // exportSelectedLineProtocolToolStripMenuItem
            // 
            this.exportSelectedLineProtocolToolStripMenuItem.Name = "exportSelectedLineProtocolToolStripMenuItem";
            this.exportSelectedLineProtocolToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.exportSelectedLineProtocolToolStripMenuItem.Text = "Line Protocol";
            this.exportSelectedLineProtocolToolStripMenuItem.Click += new System.EventHandler(this.exportSelectedLineProtocolToolStripMenuItem_Click);
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitContainer.Panel1.Controls.Add(this.tagsTextBox);
            this.splitContainer.Panel2.Controls.Add(this.dataGridView);
            this.splitContainer.Size = new System.Drawing.Size(688, 458);
            this.splitContainer.SplitterDistance = 25;
            this.splitContainer.TabIndex = 4;
            // 
            // tagsTextBox
            // 
            this.tagsTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tagsTextBox.Location = new System.Drawing.Point(0, 0);
            this.tagsTextBox.Multiline = true;
            this.tagsTextBox.Name = "tagsTextBox";
            this.tagsTextBox.ReadOnly = true;
            this.tagsTextBox.Size = new System.Drawing.Size(688, 25);
            this.tagsTextBox.TabIndex = 1;
            // 
            // QueryResultsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.Name = "QueryResultsControl";
            this.Size = new System.Drawing.Size(688, 458);
            this.contextMenuStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TextBox tagsTextBox;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem copySelectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyRowsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyRowsJsonToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyRowsKeyValueToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyHeaderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyAllHeadersToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator copySeparatorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAllCsvToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAllExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportAllLineProtocolToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedCsvToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedLineProtocolToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem jSONToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem jSONToolStripMenuItem1;
    }
}
