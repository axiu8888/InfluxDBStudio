using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScintillaNET;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    public partial class RunningQueriesControl : RequestControl
    {
        #region Fields

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the currently selected running query, if any.
        /// </summary>
        public InfluxDbRunningQuery SelectedQuery { get; private set; }

        QueryResultsControl resultsGrid;

        #endregion Properties

        #region Constructors

        public RunningQueriesControl()
        {
            InitializeComponent();
            resultsGrid = QueryResultsControl.Replace(listView, false);
            resultsGrid.SelectedRowChanged += resultsGrid_SelectedRowChanged;
            resultsGrid.PrependContextItems(killQueryToolStripMenuItem);
            Localization.Apply(this);

            // Set query editor styles, InfluxDB is SQL like, so use those
            queryEditor.Styles[Style.Sql.Identifier].ForeColor = Color.Blue;
            queryEditor.Styles[Style.Sql.String].ForeColor = Color.Red;
            queryEditor.Styles[Style.Sql.Number].ForeColor = Color.Magenta;
            queryEditor.Styles[Style.Sql.QuotedIdentifier].ForeColor = Color.Red;
        }

        #endregion Constructors

        #region Event Handlers

        // Handle query selection change
        private void resultsGrid_SelectedRowChanged(object sender, EventArgs e)
        {
            SelectedQuery = resultsGrid.SelectedRowTag as InfluxDbRunningQuery;
            queryEditor.ReadOnly = false;
            queryEditor.Text = SelectedQuery != null ? SelectedQuery.Query : null;
            queryEditor.ReadOnly = true;
            UpdateUIState();
        }

        // Handle kill query
        private async void killQueryButton_Click(object sender, EventArgs e)
        {
            await KillQuery();
        }

        private async void killQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await KillQuery();
        }

        #endregion Event Handlers

        #region Methods

        #region Commands

        // Kills the selected query
        async Task KillQuery()
        {
            try
            {
                if (SelectedQuery == null) return;
                var pid = SelectedQuery.PID;
                var response = await InfluxDbClient.KillQueryAsync(pid);

                if (!response.Success && !response.Body.Contains("query interrupted"))
                {
                    AppForm.DisplayError(response.Body);
                }
                else
                {
                    await ExecuteRequestAsync();
                }

                UpdateUIState();
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        #endregion Commands

        #region User Interface

        void UpdateUIState()
        {
            killQueryButton.Enabled = killQueryToolStripMenuItem.Enabled = SelectedQuery != null;
        }

        #endregion User Interface

        public async override Task ExecuteRequestAsync()
        {
            if (InfluxDbClient == null) throw new Exception("No InfluxDB client available.");

            resultsGrid.InfluxDbClient = InfluxDbClient;
            resultsGrid.ExportFileNameStem = "running_queries";
            resultsGrid.AllowLineProtocolExport = false;

            var queries = await InfluxDbClient.GetRunningQueriesAsync();
            var columns = new[]
            {
                Localization.T("PID"),
                Localization.T("Duration"),
                Localization.T("Database"),
                Localization.T("Query")
            };

            var values = new List<IList<object>>();
            var tags = new List<object>();
            foreach (var q in queries)
            {
                values.Add(new object[]
                {
                    q.PID.ToString(),
                    q.Duration,
                    q.Database,
                    q.Query
                });
                tags.Add(q);
            }

            resultsGrid.UpdateTable("queries", columns, values, tags);

            if (SelectedQuery != null)
            {
                var pid = SelectedQuery.PID;
                resultsGrid.SelectFirstRowWhere(tag =>
                {
                    var query = tag as InfluxDbRunningQuery;
                    return query != null && query.PID == pid;
                });
            }

            UpdateUIState();
        }

        #endregion Methods
    }
}
