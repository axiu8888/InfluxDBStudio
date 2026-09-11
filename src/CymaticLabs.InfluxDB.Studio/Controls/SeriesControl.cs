using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    public partial class SeriesControl : MeasurementControl
    {
        public override string ExportFileNameStem { get { return "series"; } }

        public SeriesControl()
        {
            InitializeComponent();
            Localization.Apply(this);
        }

        protected override async Task OnExecuteQuery()
        {
            var seriesSetNames = await InfluxDbClient.GetSeriesNamesAsync(Database, Measurement);
            if (seriesSetNames.Count() == 0) return;

            var columnNameToIndex = new Dictionary<string, int>();
            var columnList = new List<string>();
            var seriesValues = new List<Dictionary<string, string>>();

            foreach (var seriesName in seriesSetNames)
            {
                var parsedSeries = seriesName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var values = new Dictionary<string, string>();
                seriesValues.Add(values);

                foreach (var pair in parsedSeries)
                {
                    var parsedPair = pair.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    var name = parsedPair[0];
                    if (name == Measurement || parsedPair.Length == 1) continue;
                    var value = parsedPair[1];

                    if (!columnNameToIndex.ContainsKey(name))
                    {
                        columnNameToIndex.Add(name, columnList.Count);
                        columnList.Add(name);
                    }

                    values.Add(name, value);
                }
            }

            var rows = new List<IList<object>>();
            var tags = new List<object>();
            for (var i = 0; i < seriesValues.Count; i++)
            {
                var values = seriesValues[i];
                var row = new object[columnList.Count];
                for (var x = 0; x < columnList.Count; x++)
                {
                    var name = columnList[x];
                    row[x] = values.ContainsKey(name) ? values[name] : null;
                }
                rows.Add(row);
                tags.Add(values);
            }

            resultsGrid.UpdateTable("series", columnList, rows, tags);
        }
    }
}
