using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    public partial class FieldKeysControl : MeasurementControl
    {
        public override string ExportFileNameStem { get { return "field_keys"; } }

        public FieldKeysControl()
        {
            InitializeComponent();
            Localization.Apply(this);
        }

        protected async override Task OnExecuteQuery()
        {
            var fieldKeys = await InfluxDbClient.GetFieldKeysAsync(Database, Measurement);
            if (fieldKeys == null || fieldKeys.Count() == 0) return;

            var columns = new[] { Localization.T("fieldKey"), Localization.T("fieldType") };
            var values = new List<IList<object>>();
            var tags = new List<object>();
            foreach (var fk in fieldKeys)
            {
                values.Add(new object[] { fk.Name, fk.Type });
                tags.Add(fk);
            }

            resultsGrid.UpdateTable("field_keys", columns, values, tags);
        }
    }
}
