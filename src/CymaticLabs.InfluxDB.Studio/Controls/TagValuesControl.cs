using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    public partial class TagValuesControl : MeasurementControl
    {
        public override string ExportFileNameStem
        {
            get { return string.Format("tag_values_{0}", tagKeysComboBox.SelectedItem as string); }
        }

        public TagValuesControl()
        {
            InitializeComponent();
            Localization.Apply(this);
        }

        private async void tagKeysComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            await ExecuteRequestAsync();
        }

        protected async override Task OnExecuteQuery()
        {
            if (tagKeysComboBox.Items.Count == 0)
            {
                var tagKeys = await InfluxDbClient.GetTagKeysAsync(Database, Measurement);

                if (tagKeys != null && tagKeys.Count() > 0)
                {
                    foreach (var tag in tagKeys) tagKeysComboBox.Items.Add(tag);
                    tagKeysComboBox.SelectedItem = tagKeys.First();
                }
            }
            else
            {
                await BindTagValues();
            }
        }

        async Task BindTagValues()
        {
            resultsGrid.ClearResults();
            resultsGrid.ExportFileNameStem = string.Format("{0}_{1}", Measurement, ExportFileNameStem);

            var selectedTag = tagKeysComboBox.SelectedItem as string;
            if (selectedTag == null) return;

            var tagValues = await InfluxDbClient.GetTagValuesAsync(Database, Measurement, selectedTag);
            var columns = new[] { Localization.T("tagValue") };
            var values = new List<IList<object>>();
            var tags = new List<object>();
            foreach (var tv in tagValues)
            {
                values.Add(new object[] { tv.Value });
                tags.Add(tv);
            }

            resultsGrid.UpdateTable("tag_values", columns, values, tags);
        }
    }
}
