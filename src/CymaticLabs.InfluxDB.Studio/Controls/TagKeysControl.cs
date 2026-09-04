using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Renders the tag keys for a given measurement.
    /// </summary>
    public partial class TagKeysControl : MeasurementControl
    {
        #region Fields

        #endregion Fields

        #region Properties

        public override string ExportFileNameStem { get { return "tag_keys"; } }

        #endregion Properties

        #region Constructors

        public TagKeysControl()
        {
            InitializeComponent();
            Localization.Apply(this);
        }

        #endregion Constructors

        #region Methods

        protected async override Task OnExecuteQuery()
        {
            // Get tag keys from the server
            var tagKeys = await InfluxDbClient.GetTagKeysAsync(Database, Measurement);
            if (tagKeys == null || tagKeys.Count() == 0) return;

            // Add default row count column
            listView.Columns.Add(new ColumnHeader() { Text = Localization.T("#") });

            // Add tag key column
            listView.Columns.Add(new ColumnHeader() { Text = Localization.T("tagKey") });

            // Add values
            var rowCount = 0;

            foreach (var tagKey in tagKeys)
            {
                listView.Items.Add(new ListViewItem(new string[] { (++rowCount).ToString(), tagKey }) { Tag = tagKey });
            }
        }

        #endregion Methods
    }
}
