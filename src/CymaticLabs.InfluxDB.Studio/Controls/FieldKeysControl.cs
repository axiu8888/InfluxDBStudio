using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Renders the field keys for a given measurement.
    /// </summary>
    public partial class FieldKeysControl : MeasurementControl
    {
        #region Fields

        #endregion Fields

        #region Properties

        public override string ExportFileNameStem { get { return "field_keys"; } }

        #endregion Properties

        #region Constructors

        public FieldKeysControl()
        {
            InitializeComponent();
            Localization.Apply(this);
        }

        #endregion Constructors

        #region Methods

        protected async override Task OnExecuteQuery()
        {
            // Get field keys from the server
            var fieldKeys = await InfluxDbClient.GetFieldKeysAsync(Database, Measurement);
            if (fieldKeys == null || fieldKeys.Count() == 0) return;

            // Add default row count column
            listView.Columns.Add(new ColumnHeader() { Text = Localization.T("#") });

            // Add tag key column
            listView.Columns.Add(new ColumnHeader() { Text = Localization.T("fieldKey") });

            // Add tag key column
            listView.Columns.Add(new ColumnHeader() { Text = Localization.T("fieldType") });

            // Add values
            var rowCount = 0;

            foreach (var fk in fieldKeys)
            {
                listView.Items.Add(new ListViewItem(new string[] { (++rowCount).ToString(), fk.Name, fk.Type }) {Tag = fk });
            }
        }

        #endregion Methods
    }
}
