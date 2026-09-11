using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    public partial class TagKeysControl : MeasurementControl
    {
        public override string ExportFileNameStem { get { return "tag_keys"; } }

        public TagKeysControl()
        {
            InitializeComponent();
            Localization.Apply(this);
        }

        protected async override Task OnExecuteQuery()
        {
            var tagKeys = await InfluxDbClient.GetTagKeysAsync(Database, Measurement);
            if (tagKeys == null || tagKeys.Count() == 0) return;

            var columns = new[] { Localization.T("tagKey") };
            var values = new List<IList<object>>();
            var tags = new List<object>();
            foreach (var tagKey in tagKeys)
            {
                values.Add(new object[] { tagKey });
                tags.Add(tagKey);
            }

            resultsGrid.UpdateTable("tag_keys", columns, values, tags);
        }
    }
}
