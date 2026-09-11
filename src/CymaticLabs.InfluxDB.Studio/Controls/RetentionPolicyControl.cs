using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio.Dialogs;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Control for working with Retention Policies.
    /// </summary>
    public partial class RetentionPolicyControl : RequestControl
    {
        #region Fields

        // The last fetched retention policy results
        IEnumerable<InfluxDbRetentionPolicy> policies;

        // Used for creating new retention policies
        RetentionPolicyDialog policyDialog;

        QueryResultsControl resultsGrid;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the currently selected database.
        /// </summary>
        public string SelectedDatabase { get; private set; }

        /// <summary>
        /// Gets the currently selected retention policy (if any).
        /// </summary>
        public InfluxDbRetentionPolicy SelectedRetentionPolicy { get; private set; }

        #endregion Properties

        #region Constructors

        public RetentionPolicyControl()
        {
            InitializeComponent();
            resultsGrid = QueryResultsControl.Replace(listView, false);
            resultsGrid.SelectedRowChanged += resultsGrid_SelectedRowChanged;
            resultsGrid.PrependContextItems(createPolicyToolStripMenuItem, alterPolicyToolStripMenuItem, dropPolicyToolStripMenuItem);
            Localization.Apply(this);
            policyDialog = new RetentionPolicyDialog();
        }

        #endregion Constructors

        #region Event Handlers

        // Handles changes to the selected database
        private async void databaseComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (databaseComboBox.Items.Count == 0 || databaseComboBox.SelectedItem == null) return;
            SelectedDatabase = databaseComboBox.SelectedItem.ToString();
            await BindSelectedPolicy();
        }

        // Handle changes to the selected policy
        private void resultsGrid_SelectedRowChanged(object sender, EventArgs e)
        {
            SelectedRetentionPolicy = resultsGrid.SelectedRowTag as InfluxDbRetentionPolicy;
            UpdateUIState();
        }

        // Handle create policy
        private async void createButton_Click(object sender, EventArgs e)
        {
            await ShowCreatePolicy();
        }

        // Handle create policy
        private async void createPolicyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowCreatePolicy();
        }

        // Handle alter policy
        private async void alterButton_Click(object sender, EventArgs e)
        {
            await ShowAlterPolicy();
        }

        // Handle alter policy
        private async void alterPolicyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowAlterPolicy();
        }

        // Handle drop policy
        private async void dropButton_Click(object sender, EventArgs e)
        {
            await ShowDropPolicy();
        }

        // Handle drop policy
        private async void dropPolicyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowDropPolicy();
        }

        #endregion Event Handlers

        #region Methods

        #region Commands

        // Shows the create policy dialog
        async Task ShowCreatePolicy()
        {
            try
            {
                // Clear out the dialog values
                policyDialog.ResetPolicyValues();

                if (policyDialog.ShowDialog() == DialogResult.OK)
                {
                    var name = policyDialog.PolicyName;
                    var duration = policyDialog.PolicyDuration;
                    var replication = policyDialog.PolicyReplication;
                    var isDefault = policyDialog.PolicyDefault;

                    var response = await InfluxDbClient.CreateRetentionPolicyAsync(SelectedDatabase, name, duration, replication, isDefault);

                    if (!response.Success)
                    {
                        AppForm.DisplayError(response.Body);
                    }

                    // Reload latest data
                    await ExecuteRequestAsync();
                }
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        // Shows the alter policy dialog
        async Task ShowAlterPolicy()
        {
            try
            {
                var policy = SelectedRetentionPolicy;
                if (policy == null || string.IsNullOrWhiteSpace(SelectedDatabase)) return;

                var prevIsDefault = policy.Default;

                // Clear out the dialog values
                policyDialog.ResetPolicyValues();

                // Bind to current policy
                policyDialog.BindToPolicy(SelectedRetentionPolicy);
                
                if (policyDialog.ShowDialog() == DialogResult.OK)
                {
                    var name = policyDialog.PolicyName;
                    var duration = policyDialog.PolicyDuration;
                    var replication = policyDialog.PolicyReplication;
                    var isDefault = policyDialog.PolicyDefault;

                    var response = await InfluxDbClient.AlterRetentionPolicyAsync(SelectedDatabase, name, duration, replication, isDefault);

                    if (!response.Success)
                    {
                        AppForm.DisplayError(response.Body);
                    }
                    else
                    {
                        // If default status chagned, attempt to update another policy as default
                        if (prevIsDefault != isDefault && !isDefault)
                        {
                            policies = await InfluxDbClient.GetRetentionPoliciesAsync(SelectedDatabase);

                            if (policies.Count() > 0)
                            {
                                // Try to find the autogen/default
                                var rp = (from r in policies where r.Name.ToLower() == "autogen" select r).FirstOrDefault();

                                // Otherwise try to find another policy with a different name
                                if (rp == null) rp = (from r in policies where r.Name != policy.Name select r).FirstOrDefault();

                                if (rp != null)
                                {
                                    // If a new retention policy was found, make it the default
                                    response = await InfluxDbClient.AlterRetentionPolicyAsync(SelectedDatabase, rp.Name, rp.Duration, rp.ReplicationCopies, true);
                                }
                            }
                        }
                    }

                    // Reload latest data
                    await ExecuteRequestAsync();
                }
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        // Drops the selected policy
        async Task ShowDropPolicy()
        {
            try
            {
                var policy = SelectedRetentionPolicy;
                if (policy == null || string.IsNullOrWhiteSpace(SelectedDatabase)) return;
                var wasDefault = policy.Default;

                // Confirm the drop
                if (DialogResult.OK == MessageBox.Show(Localization.Format("Drop retention policy: {0}?", policy.Name), Localization.T("Confirm Drop"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning))
                {
                    var response = await InfluxDbClient.DropRetentionPolicyAsync(SelectedDatabase, policy.Name);

                    if (!response.Success)
                    {
                        AppForm.DisplayError(response.Body);
                    }

                    // If this was the default policy and was dropped
                    if (wasDefault)
                    {
                        policies = await InfluxDbClient.GetRetentionPoliciesAsync(SelectedDatabase);

                        if (policies.Count() > 0)
                        {
                            // Try to find the autogen/default
                            var rp = (from r in policies where r.Name.ToLower() == "autogen" select r).FirstOrDefault();

                            // Otherwise try to find another policy with a different name
                            if (rp == null) rp = (from r in policies where r.Name != policy.Name select r).FirstOrDefault();

                            if (rp != null)
                            {
                                // If a new retention policy was found, make it the default
                                response = await InfluxDbClient.AlterRetentionPolicyAsync(SelectedDatabase, rp.Name, rp.Duration, rp.ReplicationCopies, true);
                            }
                        }
                    }

                    // Reload latest data
                    await ExecuteRequestAsync();
                }
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        #endregion Commands

        #region User Interface

        // Updates the UI state based on current selections
        void UpdateUIState()
        {
            alterButton.Enabled = alterPolicyToolStripMenuItem.Enabled = resultsGrid.SelectedRowCount > 0;
            dropButton.Enabled = dropPolicyToolStripMenuItem.Enabled = resultsGrid.RowCount > 1 && resultsGrid.SelectedRowCount > 0;
        }

        #endregion User Interface

        #region Data

        /// <summary>
        /// Runs the current query against the configured connection and database.
        /// </summary>
        public override async Task ExecuteRequestAsync()
        {
            if (InfluxDbClient == null) throw new Exception("No InfluxDB client available.");

            resultsGrid.InfluxDbClient = InfluxDbClient;
            resultsGrid.Database = Database;
            resultsGrid.AllowLineProtocolExport = false;
            databaseComboBox.Visible = false;
            toolStripSeparator1.Visible = false;

            SelectedDatabase = Database;
            if (string.IsNullOrWhiteSpace(SelectedDatabase)) return;

            selectDbLabel.Text = Localization.T("Database:") + " " + SelectedDatabase;
            await BindSelectedPolicy();
        }

        // Binds retention policies for the current database
        async Task BindSelectedPolicy()
        {
            if (string.IsNullOrWhiteSpace(SelectedDatabase)) return;

            resultsGrid.ExportFileNameStem = SelectedDatabase + "_policies";
            policies = await InfluxDbClient.GetRetentionPoliciesAsync(SelectedDatabase);

            var columns = new[]
            {
                Localization.T("Policy Name"),
                Localization.T("Duration"),
                Localization.T("Shard Group Duration"),
                Localization.T("Replica N"),
                Localization.T("Default")
            };

            var values = new List<IList<object>>();
            var tags = new List<object>();
            foreach (var rp in policies)
            {
                values.Add(new object[]
                {
                    rp.Name,
                    rp.Duration,
                    rp.ShardGroupDuration,
                    rp.ReplicationCopies.ToString(),
                    rp.Default ? CheckMark : null
                });
                tags.Add(rp);
            }

            resultsGrid.UpdateTable("policies", columns, values, tags);

            if (SelectedRetentionPolicy != null)
            {
                var selectedName = SelectedRetentionPolicy.Name;
                resultsGrid.SelectFirstRowWhere(tag =>
                {
                    var policy = tag as InfluxDbRetentionPolicy;
                    return policy != null && policy.Name == selectedName;
                });
            }

            UpdateUIState();
        }

        #endregion Data

        #endregion Methods
    }
}
