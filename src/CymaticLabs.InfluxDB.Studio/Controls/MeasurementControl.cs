using System;
using System.Threading.Tasks;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Base class that renders information relating to measurements using the query results grid.
    /// </summary>
    public partial class MeasurementControl : RequestControl
    {
        protected QueryResultsControl resultsGrid;

        public string Measurement { get; set; }

        public virtual string ExportFileNameStem { get; }

        public MeasurementControl()
        {
            InitializeComponent();
            resultsGrid = QueryResultsControl.Replace(listView, false);
            Localization.Apply(this);
        }

        public async override Task ExecuteRequestAsync()
        {
            if (InfluxDbClient == null) throw new Exception("No InfluxDB client available.");

            resultsGrid.InfluxDbClient = InfluxDbClient;
            resultsGrid.Database = Database;
            resultsGrid.AllowLineProtocolExport = false;
            resultsGrid.ExportFileNameStem = string.Format("{0}_{1}", Measurement, ExportFileNameStem);
            resultsGrid.ClearResults();

            await OnExecuteQuery();
        }

        protected async virtual Task OnExecuteQuery()
        {
        }
    }
}
