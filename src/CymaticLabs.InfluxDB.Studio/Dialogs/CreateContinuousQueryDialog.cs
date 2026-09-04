using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScintillaNET;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio;

namespace CymaticLabs.InfluxDB.Studio.Dialogs
{
    /// <summary>
    /// Dialog used to create a new Continuous Query.
    /// </summary>
    public partial class CreateContinuousQueryDialog : Form
    {
        #region Fields

        // Place holder text for the query editing portion of a new down sampler query
        const string QueryEditorPlaceholderText = "#edit your query / downsamplers here";

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets or sets the <see cref="InfluxDB.InfluxDbClient">InfluxDB connection</see> associated
        /// with the dialog.
        /// </summary>
        public InfluxDbClient InfluxDbClient { get; set; }

        /// <summary>
        /// Gets or sets the name of the database associated with the dialog.
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Gets the resulting Continuous Query parameters from the dialog.
        /// </summary>
        public InfluxDbCqParams CqResult { get; private set; }

        #endregion Properties

        #region Constructors

        public CreateContinuousQueryDialog()
        {
            InitializeComponent();
            Localization.Apply(this);

            // Populate fill type drop down
            foreach (var fillType in Enum.GetValues(typeof(InfluxDbFillTypes)))
            {
                fillTypeComboBox.Items.Add(fillType);
            }

            fillTypeComboBox.SelectedIndex = 0;

            // Set query editor styles, InfluxDB is SQL like, so use those
            queryEditor.Styles[Style.Sql.Identifier].ForeColor = Color.Blue;
            queryEditor.Styles[Style.Sql.String].ForeColor = Color.Red;
            queryEditor.Styles[Style.Sql.Number].ForeColor = Color.Magenta;
            queryEditor.Styles[Style.Sql.QuotedIdentifier].ForeColor = Color.Red;
        }

        #endregion Constructors

        #region Event Handlers

        private void CreateContinuousQueryDialog_Load(object sender, EventArgs e)
        {
            // Setup help/info tool tips
            destinationToolTip.SetToolTip(destinationInfo, Localization.T(Properties.Resources.CQ_Destination_Info));
            sourceToolTip.SetToolTip(sourceInfo, Localization.T(Properties.Resources.CQ_Source_Info));
            intervalToolTip.SetToolTip(intervalInfo, Localization.T(Properties.Resources.CQ_Interval_Info));
            fillTypeToolTip.SetToolTip(fillTypeInfo, Localization.T(Properties.Resources.CQ_FillType_Info));
            tagsToolTip.SetToolTip(tagsInfo, Localization.T(Properties.Resources.CQ_Tags_Info));
            resampleToolTip.SetToolTip(resampleInfo, Localization.T(Properties.Resources.CQ_Resample_Info));
            resampleEveryToolTip.SetToolTip(resampleEveryInfo, Localization.T(Properties.Resources.CQ_ResampleEvery_Info));
            resampleForToolTip.SetToolTip(resampleForInfo, Localization.T(Properties.Resources.CQ_ResampleFor_Info));
        }

        // Handle add new subquery button
        private void addQueryButton_Click(object sender, EventArgs e)
        {
            NewQuery();
        }

        // Handle changes to the reample enable/disable check box
        private void resampleCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            resampleEveryLabel.Enabled = resampleEveryTextBox.Enabled = resampleCheckBox.Checked;
            resampleForLabel.Enabled = resampleForTextBox.Enabled = resampleCheckBox.Checked;
        }

        // Handles closing of the form
        private void CreateContinuousQueryDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // If the user is closing/canceling the form, nothing to do
                if (DialogResult != DialogResult.OK) return;

                // Create the CQ result from form values
                CqResult = CreateCqParamsFromValues();

                // If a valid result wasn't returned, cancel
                if (CqResult == null) e.Cancel = true;
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        #endregion Event Handlers

        #region Methods

        #region Commands

        // Adds a new query tab
        public void NewQuery()
        {
            queryEditor.Text = Localization.T(QueryEditorPlaceholderText);
        }

        #endregion Commands

        #region User Interface

        /// <summary>
        /// Resets the dialog form to its initial state.
        /// </summary>
        public void ResetCqForm()
        {
            // Reset form values
            nameTextBox.Text = null;
            databaseValue.Text = null;
            destinationComboBox.SelectedItem = null;
            destinationComboBox.Text = null;
            destinationComboBox.Items.Clear();
            sourceComboBox.SelectedItem = null;
            sourceComboBox.Text = null;
            sourceComboBox.Items.Clear();
            intervalTextBox.Text = null;
            fillTypeComboBox.SelectedIndex = 0;
            tagsTextBox.Text = null;
            resampleCheckBox.Checked = false;
            resampleEveryTextBox.Text = null;
            resampleForTextBox.Text = null;

            // Reset result
            CqResult = null;

            // Clear query
            NewQuery();

            ConfigureAsCreate();
        }

        /// <summary>
        /// Switches the dialog to create mode.
        /// </summary>
        public void ConfigureAsCreate()
        {
            Text = Localization.T("Create Continuous Query");
            createButton.Text = Localization.T("Create");
        }

        /// <summary>
        /// Switches the dialog to edit mode.
        /// </summary>
        public void ConfigureAsEdit()
        {
            Text = Localization.T("Edit Continuous Query");
            createButton.Text = Localization.T("Save");
        }

        /// <summary>
        /// Populates the form from an existing continuous query definition.
        /// </summary>
        public bool LoadFromQuery(string cqName, string query)
        {
            InfluxDbCqParams parsed;
            if (!TryParseContinuousQuery(cqName, query, out parsed))
                return false;

            nameTextBox.Text = parsed.Name;
            SetComboValue(destinationComboBox, parsed.Destination);
            SetComboValue(sourceComboBox, parsed.Source);
            intervalTextBox.Text = parsed.Interval;

            string subquery = null;
            if (parsed.SubQueries != null)
            {
                foreach (var item in parsed.SubQueries)
                {
                    subquery = item;
                    break;
                }
            }
            queryEditor.Text = subquery;

            if (parsed.Tags != null)
            {
                var tagList = new List<string>(parsed.Tags);
                tagsTextBox.Text = string.Join(", ", tagList.ToArray());
            }

            fillTypeComboBox.SelectedItem = parsed.FillType;

            var hasResample = !string.IsNullOrWhiteSpace(parsed.ResampleEveryInterval)
                || !string.IsNullOrWhiteSpace(parsed.ResampleForInterval);
            resampleCheckBox.Checked = hasResample;
            resampleEveryTextBox.Text = parsed.ResampleEveryInterval;
            resampleForTextBox.Text = parsed.ResampleForInterval;
            return true;
        }

        static void SetComboValue(ComboBox combo, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                combo.SelectedItem = null;
                combo.Text = null;
                return;
            }

            foreach (var item in combo.Items)
            {
                if (string.Equals(item as string, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            combo.SelectedItem = null;
            combo.Text = value;
        }

        static bool TryParseContinuousQuery(string cqName, string query, out InfluxDbCqParams parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(query)) return false;

            var normalized = Regex.Replace(query.Trim().TrimEnd(';'), @"\s+", " ");
            var headerMatch = Regex.Match(normalized,
                @"^CREATE\s+CONTINUOUS\s+QUERY\s+(?<name>\S+)\s+ON\s+(?<db>\S+)(?:\s+RESAMPLE(?<resample>.*?))?\s+BEGIN\s+(?<body>.*)\s+END\s*$",
                RegexOptions.IgnoreCase);

            if (!headerMatch.Success) return false;

            var body = headerMatch.Groups["body"].Value.Trim();
            var intoIndex = IndexOfKeyword(body, "INTO", 0);
            var fromIndex = intoIndex >= 0 ? IndexOfKeyword(body, "FROM", intoIndex) : -1;
            var groupIndex = fromIndex >= 0 ? IndexOfKeyword(body, "GROUP", fromIndex) : -1;

            if (intoIndex < 0 || fromIndex < 0 || groupIndex < 0) return false;
            if (!body.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) return false;

            var select = body.Substring(6, intoIndex - 6).Trim();
            var destination = UnquoteIdent(body.Substring(intoIndex + 4, fromIndex - (intoIndex + 4)).Trim());
            var fromAndMaybeWhere = body.Substring(fromIndex + 4, groupIndex - (fromIndex + 4)).Trim();

            var whereIndex = IndexOfKeyword(fromAndMaybeWhere, "WHERE", 0);
            var source = UnquoteIdent(whereIndex >= 0 ? fromAndMaybeWhere.Substring(0, whereIndex).Trim() : fromAndMaybeWhere);

            var groupBy = body.Substring(groupIndex).Trim();
            var timeMatch = Regex.Match(groupBy, @"GROUP\s+BY\s+time\((?<interval>[^)]+)\)(?<rest>.*)$", RegexOptions.IgnoreCase);
            if (!timeMatch.Success) return false;

            var interval = timeMatch.Groups["interval"].Value.Trim();
            var rest = timeMatch.Groups["rest"].Value.Trim();
            var fillType = InfluxDbFillTypes.Null;
            var tags = new List<string>();

            var fillMatch = Regex.Match(rest, @"\bfill\((?<fill>[^)]+)\)\s*$", RegexOptions.IgnoreCase);
            if (fillMatch.Success)
            {
                var fillName = fillMatch.Groups["fill"].Value.Trim();
                InfluxDbFillTypes parsedFill;
                if (Enum.TryParse(fillName, true, out parsedFill))
                    fillType = parsedFill;
                rest = rest.Substring(0, fillMatch.Index).Trim().Trim(',');
            }
            else
            {
                rest = rest.Trim().Trim(',');
            }

            if (!string.IsNullOrWhiteSpace(rest))
            {
                foreach (var tag in rest.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cleaned = UnquoteIdent(tag);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                        tags.Add(cleaned);
                }
            }

            string resampleEvery = null;
            string resampleFor = null;
            var resample = headerMatch.Groups["resample"].Value;
            if (!string.IsNullOrWhiteSpace(resample))
            {
                var everyMatch = Regex.Match(resample, @"EVERY\s+(\S+)", RegexOptions.IgnoreCase);
                if (everyMatch.Success) resampleEvery = everyMatch.Groups[1].Value.Trim();

                var forMatch = Regex.Match(resample, @"FOR\s+(\S+)", RegexOptions.IgnoreCase);
                if (forMatch.Success) resampleFor = forMatch.Groups[1].Value.Trim();
            }

            parsed = new InfluxDbCqParams
            {
                Name = string.IsNullOrWhiteSpace(cqName) ? UnquoteIdent(headerMatch.Groups["name"].Value) : cqName,
                Database = UnquoteIdent(headerMatch.Groups["db"].Value),
                SubQueries = new List<string> { select },
                Destination = destination,
                Source = source,
                Interval = interval,
                FillType = fillType,
                Tags = tags.Count > 0 ? tags : null,
                ResampleEveryInterval = resampleEvery,
                ResampleForInterval = resampleFor
            };

            return !string.IsNullOrWhiteSpace(parsed.Name)
                && !string.IsNullOrWhiteSpace(parsed.Destination)
                && !string.IsNullOrWhiteSpace(parsed.Source)
                && !string.IsNullOrWhiteSpace(parsed.Interval)
                && !string.IsNullOrWhiteSpace(select);
        }

        static int IndexOfKeyword(string text, string keyword, int start)
        {
            if (string.IsNullOrEmpty(text) || start < 0 || start >= text.Length) return -1;
            var match = Regex.Match(text.Substring(start), @"\b" + Regex.Escape(keyword) + @"\b", RegexOptions.IgnoreCase);
            return match.Success ? start + match.Index : -1;
        }

        static string UnquoteIdent(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            value = value.Trim();
            var parts = value.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (part.Length >= 2 &&
                    ((part[0] == '"' && part[part.Length - 1] == '"') ||
                     (part[0] == '\'' && part[part.Length - 1] == '\'')))
                {
                    part = part.Substring(1, part.Length - 2);
                }
                parts[i] = part;
            }
            return string.Join(".", parts);
        }

        #endregion User Interface

        #region Data

        /// <summary>
        /// Binds the form's database drop down to the list of available databases for the 
        /// current connection.
        /// </summary>
        public async Task BindInfluxDataSources()
        {
            try
            {
                // No database assigned
                if (string.IsNullOrWhiteSpace(Database)) return;

                // Display database name in UI
                databaseValue.Text = Database;

                // Clear the current list of measurements and reload
                destinationComboBox.Items.Clear();
                sourceComboBox.Items.Clear();
                var measurementNames = await InfluxDbClient.GetMeasurementNamesAsync(Database);

                foreach (var measurement in measurementNames)
                {
                    destinationComboBox.Items.Add(measurement);
                    sourceComboBox.Items.Add(measurement);
                }

                destinationComboBox.SelectedItem = null;
                sourceComboBox.SelectedItem = null;
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
            }
        }

        // Validates the current form values
        bool ValidateCqValues()
        {
            try
            {
                // Validate values
                var cqName = nameTextBox.Text;

                // CQ Name
                if (string.IsNullOrWhiteSpace(cqName))
                {
                    AppForm.DisplayError("Continuous Query name cannot be blank.");
                    return false;
                }

                // Source & Destination
                var destination = destinationComboBox.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(destination)) destination = destinationComboBox.Text;
                var source = sourceComboBox.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(source)) source = sourceComboBox.Text;

                if (string.IsNullOrWhiteSpace(destination))
                {
                    AppForm.DisplayError("Destination cannot be blank.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(source))
                {
                    AppForm.DisplayError("Source cannot be blank.");
                    return false;
                }

                if (destination == source)
                {
                    if (MessageBox.Show(Localization.T("Source is the same as the Destination. These are typically different values for a Continous Query. Are you sure that you want to have duplicate values?"), Localization.T("Confirm"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    {
                        return false;
                    }
                }

                // Interval
                var interval = intervalTextBox.Text;

                if (string.IsNullOrWhiteSpace(interval))
                {
                    AppForm.DisplayError("Interval cannot be blank. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.");
                    return false;
                }

                interval = interval.Trim();

                if (!InfluxDbHelper.IsTimeIntervalValid(interval))
                {
                    AppForm.DisplayError("Interval value is invalid. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.");
                    return false;
                }

                // Resample Every
                if (resampleCheckBox.Checked)
                {
                    var resampleEvery = resampleEveryTextBox.Text;

                    if (!string.IsNullOrWhiteSpace(resampleEvery))
                    {
                        resampleEvery = resampleEvery.Trim();

                        if (!InfluxDbHelper.IsTimeIntervalValid(resampleEvery))
                        {
                            AppForm.DisplayError("Resample Every interval value is invalid. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.");
                            return false;
                        }
                    }
                }

                // Resample For
                if (resampleCheckBox.Checked)
                {
                    var resampleFor = resampleForTextBox.Text;

                    if (!string.IsNullOrWhiteSpace(resampleFor))
                    {
                        resampleFor = resampleFor.Trim();

                        if (!InfluxDbHelper.IsTimeIntervalValid(resampleFor))
                        {
                            AppForm.DisplayError("Resample For interval value is invalid. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.");
                            return false;
                        }
                    }
                }

                // Subquery
                if (queryEditor.Text == null || queryEditor.Text.Length == 0 || queryEditor.Text == Localization.T(QueryEditorPlaceholderText))
                {
                    AppForm.DisplayError("SubQuery cannot be blank.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
                return false;
            }
        }

        /// <summary>
        /// Creates a new <see cref="InfluxDbCqParams">Continuous Query</see> parameters object
        /// from the current dialog values.
        /// </summary>
        /// <returns>A CQ params object if current values are valid, otherwise NULL.</returns>
        public InfluxDbCqParams CreateCqParamsFromValues()
        {
            if (!ValidateCqValues()) return null;

            try
            {
                // Collect form values
                var destination = destinationComboBox.SelectedItem != null ? destinationComboBox.SelectedItem as string : destinationComboBox.Text;
                var source = sourceComboBox.SelectedItem != null ? sourceComboBox.SelectedItem as string : sourceComboBox.Text;

                // Subqueries
                var subQueries = new List<string>();
                subQueries.Add(queryEditor.Text.Trim());

                // Tags
                List<string> tags = new List<string>();

                if (!string.IsNullOrWhiteSpace(tagsTextBox.Text))
                {
                    var parsedTags = tagsTextBox.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tag in parsedTags) tags.Add(tag.Trim());
                }

                // Create the CQ parameters
                var cqParams = new InfluxDbCqParams()
                {
                    Name = nameTextBox.Text,
                    Database = databaseValue.Text,
                    Destination = destination,
                    Source = source,
                    SubQueries = subQueries,
                    Interval = intervalTextBox.Text,
                    FillType = (InfluxDbFillTypes)fillTypeComboBox.SelectedItem,
                    Tags = tags.Count > 0 ? tags : null,
                    ResampleEveryInterval = resampleEveryTextBox.Text,
                    ResampleForInterval = resampleForTextBox.Text
                };

                return cqParams;
            }
            catch (Exception ex)
            {
                AppForm.DisplayException(ex);
                return null;
            }
        }

        #endregion Data

        #endregion Methods
    }
}
