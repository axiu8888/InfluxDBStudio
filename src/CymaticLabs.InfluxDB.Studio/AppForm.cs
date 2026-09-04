using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using log4net;
using Newtonsoft.Json;
using CymaticLabs.InfluxDB.Data;
using CymaticLabs.InfluxDB.Studio.Controls;
using CymaticLabs.InfluxDB.Studio.Dialogs;

namespace CymaticLabs.InfluxDB.Studio
{
    /// <summary>
    /// The main application form.
    /// </summary>
    public partial class AppForm : Form
    {
        #region Enums
        
        /// <summary>
        /// Different InfluxDB types represented by tree view nodes.
        /// </summary>
        enum InfluxDbNodeTypes
        {
            /// <summary>
            /// Loading place holder used for lazy loading node data.
            /// </summary>
            LoadingPlacholder = 0,

            /// <summary>
            /// An InfluxDB server/connection.
            /// </summary>
            Connection = 1,

            /// <summary>
            /// An InfluxDB database.
            /// </summary>
            Database = 2,

            /// <summary>
            /// An InfluxDB database measurement (table).
            /// </summary>
            Measurement = 3,
        }

        #endregion Enums

        #region Fields

        // Singleton instance
        static AppForm instance;

        // The connections dialog used to manage connections
        ManageConnectionsDialog manageConnectionsDialog;

        // Used to create new databases
        CreateDatabaseDialog createDatabaseDialog;

        // Used to perform back fill queries
        private BackfillDialog backfillDialog;

        // Used to create new continuous queries from the database context menu
        CreateContinuousQueryDialog createCqDialog;

        // Used to pick a continuous query to drop from the database context menu
        SelectContinuousQueryDialog selectCqDialog;

        // Used to create retention policies from the database context menu
        RetentionPolicyDialog retentionPolicyDialog;

        // Used to pick a retention policy to drop from the database context menu
        SelectRetentionPolicyDialog selectRetentionPolicyDialog;

        // The application about dialog
        AboutDialog aboutDialog;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the application settings.
        /// </summary>
        public static AppSettings Settings { get; private set; }

        /// <summary>
        /// Gets the application's logger.
        /// </summary>
        public static ILog Log { get; private set; }

        /// <summary>
        /// Gets the list of currently active InfluxDB client connections.
        /// </summary>
        public static List<InfluxDbClient> ActiveClients { get; private set; }

        /// <summary>
        /// The global tab context menu used for closing tabs with extended options.
        /// </summary>
        public static ContextMenuStrip TabContextMenu { get; private set; }

        #endregion Properties

        #region Constructors

        public AppForm()
        {
            // Setup static properties
            instance = this;
            Settings = new AppSettings();

            // Enable logging
            Log = LogManager.GetLogger("AppLogger");

            // Setup container for active database connection clients
            ActiveClients = new List<InfluxDbClient>();
            InitializeComponent();
            Localization.Apply(this);

            // Create dialog windows
            aboutDialog = new AboutDialog();
            createDatabaseDialog = new CreateDatabaseDialog();
            backfillDialog = new BackfillDialog();
            createCqDialog = new CreateContinuousQueryDialog();
            selectCqDialog = new SelectContinuousQueryDialog();
            retentionPolicyDialog = new RetentionPolicyDialog();
            selectRetentionPolicyDialog = new SelectRetentionPolicyDialog();
            manageConnectionsDialog = new ManageConnectionsDialog();
            manageConnectionsDialog.ConnectionCreated += ManageConnectionsDialog_ConnectionCreated;
            manageConnectionsDialog.ConnectionUpdated += ManageConnectionsDialog_ConnectionUpdated;
            manageConnectionsDialog.ConnectionRemoved += ManageConnectionsDialog_ConnectionRemoved;
        }

        #endregion Constructors

        #region Event Handlers

        #region AppForm

        // Handle app load
        private async void AppForm_Load(object sender, EventArgs e)
        {
            // Assign the tab context menu
            TabContextMenu = tabContextMenuStrip;

            // Clear status
            statusLabel.Text = null;

            // Clear the current list of connections
            connectionsTreeView.Nodes.Clear();

            // Load current application settings
            Settings.LoadAll();

            saveFileDialog.Filter = Localization.T("JSON files|*.json|All files|*.*");
            openFileDialog.Filter = Localization.T("JSON files|*.json|All files|*.*");

            // Apply the settings to the application
            ApplySettings();

            // Set initial tool strip state
            UpdateUIState();

            // Wait a little bit for the main form to load and then show the connections dialog
            await Task.Delay(250);
            await ShowConnectionsDialog();
        }

        #endregion AppForm

        #region File Menu

        #region File

        // File -> Import -> Application Settings
        private async void importAppSettingsMenuItem_Click(object sender, EventArgs e)
        {
            await ImportSettings(true);
        }

        // File -> Export -> Appliction Settings
        private void exportAppSettingsMenuItem_Click(object sender, EventArgs e)
        {
            ExportSettings();
        }

        // File -> Exit
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        #endregion File

        #region Connections

        // Handles Connections -> Manage
        private async void manageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ShowConnectionsDialog();
        }

        #endregion Connections

        #region Query

        // Query -> Run
        private async void runQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ExecuteCurrentRequest();
        }
        
        // Query -> New Query
        private void newQueryToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            NewQuery(node);
        }

        // Query -> Show Queries
        private async void showQueriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowQueries(node);
        }

        #endregion Query

        #region Settings

        // Handles Settings -> Allow Untrusted SSL Certificates
        private void allowUntrustedSSLToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            var allowUntrustedSsl = allowUntrustedSSLToolStripMenuItem.Checked;
            SslIgnoreValidator.AllowUntrusted = allowUntrustedSsl;
            Settings.AllowUntrustedSsl = allowUntrustedSsl;
        }

        // Handles Settings -> Time Format -> change of time format
        private void timeFormatComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (timeFormatComboBox.SelectedIndex < 0) return;
            Settings.TimeFormat = timeFormatComboBox.SelectedItem as string;
        }

        // Handles Settings -> Date Format -> change of date format
        private void dateFormatComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (dateFormatComboBox.SelectedIndex < 0) return;
            Settings.DateFormat = dateFormatComboBox.SelectedItem as string;
        }

        private void englishToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Language = Localization.English;
            Localization.ChangeLanguage(Localization.English);
        }

        private void chineseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Language = Localization.Chinese;
            Localization.ChangeLanguage(Localization.Chinese);
        }

        #endregion Settings

        #region Help

        // Help -> About
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            aboutDialog.ShowDialog();
        }

        #endregion Help

        #endregion File Menu

        #region Tool Strip

        // Manage Connections
        private async void manageConnectionsButton_Click(object sender, EventArgs e)
        {
            await ShowConnectionsDialog();
        }

        // Disconnect
        private void disconnectButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;

            var type = GetNodeType(node);

            switch (type)
            {
                case InfluxDbNodeTypes.Connection:
                    Disconnect(node);
                    break;

                case InfluxDbNodeTypes.Database:
                    Disconnect(node.Parent);
                    break;

                case InfluxDbNodeTypes.Measurement:
                    Disconnect(node.Parent.Parent);
                    break;
            }
        }

        // Show Retention Policies
        private async void showPoliciesButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowRetentionPolicies(node);
        }

        // Show Users
        private async void showUsersButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowUsers(node);
        }

        // Show Statistics
        private async void showStatsButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowStatistics(node);
        }

        // Show Diagnostics
        private async void showDiagnosticsButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowDiagnostics(node);
        }

        // Refresh
        private async void refreshButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            var type = GetNodeType(node);

            switch (type)
            {
                case InfluxDbNodeTypes.Connection:
                    await RefreshConnection(node);
                    break;

                case InfluxDbNodeTypes.Database:
                    await RefreshDatabase(node);
                    break;
            }
        }

        // Run Query
        private async void runQueryButton_Click(object sender, EventArgs e)
        {
            if (CanRunQuery()) await ExecuteCurrentRequest();
        }

        // New Query
        private void newQueryButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            NewQuery(node);
        }

        // Query -> Show Queries
        private async void showQueriesButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowQueries(node);
        }

        // Create Database
        private async void createDatabaseButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await CreateDatabase(node);
        }

        // Show Continuous Queries
        private async void continuousQueryButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowContinuousQueries(node);
        }

        // Run Back Fill
        private async void backFillButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await RunBackFill(node);
        }

        // Drop Database
        private async void dropDatabaseButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropDatabase(node);
        }

        // Show Tag Keys
        private async void tagKeysButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowTagKeys(node);
        }

        // Show Tag Values
        private async void tagValuesButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowTagValues(node);
        }

        // Show Field Keys
        private async void fieldKeysButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowFieldKeys(node);
        }

        // Show Series
        private async void showSeriesButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowSeries(node);
        }

        // Drop Series
        private async void dropSeriesButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropSeries(node);
        }

        // Drop Measurement
        private async void dropMeasurementButton_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropMeasurement(node);
        }

        #endregion Tool Strip

        #region Connections Tree View

        #region Tree Nodes

        // Handle selection change
        private void connectionsTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Update the UI accordingly
            UpdateUIState();
        }

        // Handle connections clicks
        private void connectionsTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // If this is a right click, set the clicked node to the selected node for conext menus
            if (e.Button == MouseButtons.Right) connectionsTreeView.SelectedNode = e.Node;
        }

        // Handle node double click
        private void connectionsTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // If this is a measurement, launch a new query
            var node = e.Node;
            if (GetNodeType(node) != InfluxDbNodeTypes.Measurement) return;
            NewQuery(node);
        }

        // Handle tree node expansion
        private async void connectionsTreeView_AfterExpand(object sender, TreeViewEventArgs e)
        {
            await ExpandNodeChildren(e.Node);
        }

        #endregion Tree Nodes

        #region Connection Context Menu

        // Connection -> Refresh
        private async void connectionRefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await RefreshConnection(node);
        }

        // Connection -> Create Database
        private async void createDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await CreateDatabase(node);
        }

        // Connection -> Show Queries
        private async void showQueriesContextMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowQueries(node);
        }

        // Connection -> Show Users
        private async void showUsersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowUsers(node);
        }

        // Connection -> Show Statistics
        private async void showStatisticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowStatistics(node);
        }

        // Connection -> Show Diagnostics
        private async void diagnosticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowDiagnostics(node);
        }

        // Connection -> Disconnect
        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            Disconnect(node);
        }

        #endregion Connection Context Menu

        #region Database Context Menu

        // Database -> Refresh
        private async void databaseRefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await RefreshDatabase(node);
        }

        // Database -> Retention Policies -> View
        private async void viewRetentionPoliciesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowRetentionPolicies(node);
        }

        // Database -> Retention Policies -> New
        private async void createRetentionPolicyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await CreateRetentionPolicy(node);
        }

        // Database -> Retention Policies -> Delete
        private async void dropRetentionPolicyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropRetentionPolicy(node);
        }

        // Database -> Continuous Queries -> View
        private async void viewContinuousQueriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowContinuousQueries(node);
        }

        // Database -> Continuous Queries -> New
        private async void createContinuousQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await CreateContinuousQuery(node);
        }

        // Database -> Continuous Queries -> Delete
        private async void dropContinuousQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropContinuousQuery(node);
        }

        // Database -> Run Back Fill
        private async void backFillToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await RunBackFill(node);
        }

        // Database -> Drop Database
        private async void dropDatabaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropDatabase(node);
        }

        // Database|Measurement -> Import Line Protocol
        private async void importLineProtocolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ImportLineProtocol(node);
        }

        #endregion Database Context Menu

        #region Measurement Context Menu

        // Measurement -> Show Series
        private async void showSeriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowSeries(node);
        }

        // Measurement -> Tag Keys
        private async void tagKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowTagKeys(node);
        }

        // Measurement -> Tag Values
        private async void tagValuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowTagValues(node);
        }

        // Measurement -> Field Keys
        private async void fieldKeysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await ShowFieldKeys(node);
        }

        // Measurement -> Drop Measurement
        private async void dropMeasurementToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropMeasurement(node);
        }

        // Measurement => Drop Series
        private async void dropSeriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            await DropSeries(node);
        }

        #endregion Measurement Context Menu

        #region Context Menu Shared

        // Handle database/measurement -> New Query
        private void newQueryMenuItem_Click(object sender, EventArgs e)
        {
            var node = connectionsTreeView.SelectedNode;
            if (node == null) return;
            NewQuery(node);
        }

        #endregion Context Menu Shared

        #endregion Connections Tree View

        #region Tab Control

        // Handle user closing of tabs
        private void tabControl_TabClosed(object sender, TabPage e)
        {
            UpdateUIState();
        }

        #endregion Tab Control

        #region Manage Connections Dialog

        // Handles the creation of a connection
        private void ManageConnectionsDialog_ConnectionCreated(InfluxDbConnection connection)
        {
            try
            {
                // Ensure this is not a duplicate by name
                foreach (var c in Settings.Connections)
                {
                    if (c.Name == connection.Name)
                    {
                        DisplayError(Localization.Format("A connection already exists with name: {0}", c.Name), Localization.T("Connection Exists"));
                        return;
                    }
                }

                // Add to the global list
                Settings.Connections.Add(connection);

                // Save connection data
                Settings.SaveConnections();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }
            

        // Handles the update of a connection
        private async void ManageConnectionsDialog_ConnectionUpdated(InfluxDbConnection connection)
        {
            try
            {
                // Save connection data
                Settings.SaveConnections();

                // Go through active connection and update the matching connection in the UI if found
                foreach (TreeNode node in connectionsTreeView.Nodes)
                {
                    var c = node.Tag as InfluxDbConnection;

                    if (c.Id == connection.Id || c == connection)
                    {
                        await RenderConnectionDetails(node, connection, true);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Handles the removal of a connection
        private void ManageConnectionsDialog_ConnectionRemoved(InfluxDbConnection connection)
        {
            try
            {
                // Remove the connection from the global list
                foreach (var c in Settings.Connections)
                {
                    if (c.Id == connection.Id)
                    {
                        Settings.Connections.Remove(c);
                        break;
                    }
                }

                // Save connection data
                Settings.SaveConnections();

                // Remove from UI
                foreach (TreeNode node in connectionsTreeView.Nodes)
                {
                    if (node.Text == connection.Name)
                    {
                        Disconnect(node);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        #endregion Manage Connections Dialog

        #endregion Event Handlers

        #region Methods

        #region Commands

        #region Application

        // Import application settings
        async Task ImportSettings(bool showConnectionManage = false)
        {
            try
            {
                // Prompt to open
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Open the file
                    var json = File.ReadAllText(openFileDialog.FileName);

                    // Convert to app settings object
                    Settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    // Save to disk
                    Settings.SaveAll();

                    // Apply Settings
                    ApplySettings();

                    if (!string.IsNullOrWhiteSpace(Settings.Language) &&
                        !string.Equals(Settings.Language, Localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        Localization.ChangeLanguage(Settings.Language);
                        return;
                    }

                    // Show connections manager if requested
                    if (showConnectionManage) await ShowConnectionsDialog();
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Export application settings
        void ExportSettings()
        {
            try
            {
                // Prompt to save
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Serialize to JSON
                    var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);

                    // Write to file
                    File.WriteAllText(saveFileDialog.FileName, json);
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        #endregion Application

        #region Connection

        // Loads data under a tree node
        async Task ExpandNodeChildren(TreeNode node)
        {
            try
            {
                // Check to see if this a loading place holder node
                if (node.FirstNode == null || node.FirstNode.ImageIndex != (int)InfluxDbNodeTypes.LoadingPlacholder)
                    return;

                // Get the connection and client for this node
                var connection = GetConnection(node);
                var client = GetClient(connection);

                switch (GetNodeType(node))
                {
                    case InfluxDbNodeTypes.Connection:
                        await RenderDatabases(node, client);
                        break;

                    case InfluxDbNodeTypes.Database:
                        await RenderMeasurements(node, client);
                        break;
                    
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Connection -> Refresh
        async Task RefreshConnection(TreeNode node)
        {
            try
            {
                // Remove all child nodes
                node.Nodes.Clear();

                // Get the connection and client for the node
                var connection = GetConnection(node);
                var client = GetClient(connection);

                // Add a placeholder
                var placeholderNode = CreateTreeNode(Localization.T("loading..."), InfluxDbNodeTypes.LoadingPlacholder);
                node.Nodes.Add(placeholderNode);

                // Refresh and rerender
                await RenderDatabases(node, client);
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Connection -> Create Database
        async Task CreateDatabase(TreeNode node)
        {
            try
            {
                // Get the selected connection
                var connection = node.Tag as InfluxDbConnection;
                createDatabaseDialog.ConnectionName = connection.Name;
                createDatabaseDialog.DatabaseName = null;

                // Get the active client for this connection
                var client = GetClient(connection);

                // Show the create database dialog
                if (createDatabaseDialog.ShowDialog() == DialogResult.OK)
                {
                    // Get current list of database names
                    var currentDbNames = await client.GetDatabaseNamesAsync();

                    // Validate new database name
                    if (string.IsNullOrWhiteSpace(createDatabaseDialog.DatabaseName))
                    {
                        DisplayError(Localization.T("Database name cannot be blank."), Localization.T("Error Creating Database"));
                    }
                    // Ensure unique name
                    else if (currentDbNames.Contains(createDatabaseDialog.DatabaseName))
                    {
                        DisplayError(Localization.Format("A database named '{0}' already exists.", createDatabaseDialog.DatabaseName), Localization.T("Error Creating Database"));
                        return;
                    }
                    // Attempt to create the database
                    else
                    {
                        // Create the database and receive the response
                        var response = await client.CreateDatabaseAsync(createDatabaseDialog.DatabaseName);

                        if (!response.Success)
                        {
                            DisplayError(response.Body, Localization.T("Error Creating Database"));
                            return;
                        }

                        // If the create was successful, show the new database
                        var newDatabaseNode = CreateTreeNode(createDatabaseDialog.DatabaseName, InfluxDbNodeTypes.Database, createDatabaseDialog.DatabaseName);
                        node.Nodes.Add(newDatabaseNode);
                        newDatabaseNode.ContextMenuStrip = databaseContextMenu;

                        // Don't render measurement, instead include a loading place holder
                        var placeholderNode = CreateTreeNode(Localization.T("loading..."), InfluxDbNodeTypes.LoadingPlacholder);
                        newDatabaseNode.Nodes.Add(placeholderNode);

                        connectionsTreeView.SelectedNode = newDatabaseNode;
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database -> Retention Policies -> View
        async Task ShowRetentionPolicies(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);

                var policyControl = new RetentionPolicyControl();
                policyControl.InfluxDbClient = client;
                policyControl.Database = database;

                tabControl.AddTabWithControl(Localization.Format("{0}.{1} policies", connection.Name, database),
                    policyControl, Properties.Resources.RetentionPolicy);

                UpdateUIState();

                await policyControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database -> Retention Policies -> New
        async Task CreateRetentionPolicy(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);

                retentionPolicyDialog.ResetPolicyValues();

                if (retentionPolicyDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var response = await client.CreateRetentionPolicyAsync(
                        database,
                        retentionPolicyDialog.PolicyName,
                        retentionPolicyDialog.PolicyDuration,
                        retentionPolicyDialog.PolicyReplication,
                        retentionPolicyDialog.PolicyDefault);

                    if (response.Success)
                    {
                        await RefreshOpenRetentionPolicyTabs(client, database);
                    }
                    else
                    {
                        DisplayError(response.Body);
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database -> Retention Policies -> Delete
        async Task DropRetentionPolicy(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);

                var policyList = await client.GetRetentionPoliciesAsync(database);
                var policies = policyList != null ? new List<InfluxDbRetentionPolicy>(policyList) : new List<InfluxDbRetentionPolicy>();

                if (policies.Count == 0)
                {
                    MessageBox.Show(Localization.T("There are no retention policies to delete."),
                        Localization.T("Drop Retention Policy"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (policies.Count == 1)
                {
                    MessageBox.Show(Localization.T("A database must have at least one retention policy."),
                        Localization.T("Drop Retention Policy"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                selectRetentionPolicyDialog.Bind(policies);

                if (selectRetentionPolicyDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                var selectedPolicy = selectRetentionPolicyDialog.SelectedPolicy;
                if (selectedPolicy == null) return;

                if (MessageBox.Show(Localization.Format("Drop retention policy: {0}?", selectedPolicy.Name),
                    Localization.T("Confirm Drop"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                {
                    return;
                }

                var wasDefault = selectedPolicy.Default;
                var response = await client.DropRetentionPolicyAsync(database, selectedPolicy.Name);

                if (!response.Success)
                {
                    DisplayError(response.Body);
                    return;
                }

                if (wasDefault)
                {
                    var remaining = await client.GetRetentionPoliciesAsync(database);
                    var rp = remaining != null
                        ? remaining.FirstOrDefault(r => string.Equals(r.Name, "autogen", StringComparison.OrdinalIgnoreCase))
                          ?? remaining.FirstOrDefault(r => r.Name != selectedPolicy.Name)
                        : null;

                    if (rp != null)
                    {
                        await client.AlterRetentionPolicyAsync(database, rp.Name, rp.Duration, rp.ReplicationCopies, true);
                    }
                }

                await RefreshOpenRetentionPolicyTabs(client, database);
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Reloads any open retention policy tabs that match the given connection and database
        async Task RefreshOpenRetentionPolicyTabs(InfluxDbClient client, string database)
        {
            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Controls.Count == 0) continue;

                var policyControl = tab.Controls[0] as RetentionPolicyControl;
                if (policyControl == null) continue;
                if (policyControl.InfluxDbClient != client) continue;
                if (!string.Equals(policyControl.Database, database, StringComparison.Ordinal)) continue;

                await policyControl.ExecuteRequestAsync();
            }
        }

        // Connection -> Show Users
        async Task ShowUsers(TreeNode node)
        {
            try
            {
                // Get connection
                var connection = GetConnection(node);
                var client = GetClient(connection);

                // Create a new users control
                var usersControl = new InfluxDbUsersControl();
                usersControl.InfluxDbClient = client;

                // Add a tab with a query control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.users", connection.Name), usersControl, Properties.Resources.Users);

                // Update UI
                UpdateUIState();

                // Render
                await usersControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Connection -> Show Statistics
        async Task ShowStatistics(TreeNode node)
        {
            try
            {
                // Get connection
                var connection = GetConnection(node);
                var client = GetClient(connection);

                // Create a new diagnostics control
                var statsControl = new StatsControl();
                statsControl.InfluxDbClient = client;

                // Add a tab with a query control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.statistics", connection.Name), statsControl, Properties.Resources.Stats);

                // Update UI
                UpdateUIState();

                // Render
                await statsControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Connection -> Show Diagnostics
        async Task ShowDiagnostics(TreeNode node)
        {
            try
            {
                // Query diagnostics info
                var connection = GetConnection(node);
                var client = GetClient(connection);

                // Create a new diagnostics control
                var diagnosticsControl = new DiagnosticsControl();
                diagnosticsControl.InfluxDbClient = client;

                // Add a tab with a query control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.diagnostics", connection.Name), diagnosticsControl, Properties.Resources.Diagnostics);

                // Update UI
                UpdateUIState();

                // Render
                await diagnosticsControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Connection -> Disconnect
        void Disconnect(TreeNode node)
        {
            try
            {
                // Remove from active connections list
                var connection = GetConnection(node);
                var client = GetClient(connection);

                // Go through and remove all tabs associated with this connection
                var removedTabs = new List<TabPage>();

                foreach (TabPage tab in tabControl.TabPages)
                {
                    if (tab.Controls.Count > 0 && tab.Controls[0] is RequestControl)
                    {
                        var requestControl = tab.Controls[0] as RequestControl;
                        if (requestControl == null) continue;
                        if (requestControl.InfluxDbClient.Connection.Id == connection.Id)
                        {
                            removedTabs.Add(tab);
                        }
                    }
                }

                foreach (TabPage tab in removedTabs)
                {
                    tabControl.Controls.Remove(tab);
                }

                // Remove from active clients
                ActiveClients.Remove(client);

                // Really nothing to disconnect, just remove the node
                connectionsTreeView.Nodes.Remove(node);
                
                // Update UI
                UpdateUIState();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        #endregion Connection

        #region Database

        // Database -> Refresh
        async Task RefreshDatabase(TreeNode node)
        {
            try
            {
                // Remove all child nodes
                node.Nodes.Clear();

                // Get the connection and client for the node
                var connection = GetConnection(node);
                var client = GetClient(connection);

                // Add a placeholder
                var placeholderNode = new TreeNode(Localization.T("loading..."), 4, 4);
                node.Nodes.Add(placeholderNode);

                // Refresh and rerender
                await RenderMeasurements(node, client);
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database -> Continuous Queries -> View
        async Task ShowContinuousQueries(TreeNode node)
        {
            try
            {
                // Get the connection for this node
                var connection = GetConnection(node);

                // Get the active client for this connection
                var client = GetClient(connection);
                var database = GetDatabaseName(node);

                // Create a new contiuous query control
                var continousQueryControl = new ContinuousQueryControl();
                continousQueryControl.InfluxDbClient = client;
                continousQueryControl.Database = database;

                // Add a tab with a query control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.{1} CQs", connection.Name, database), 
                    continousQueryControl, Properties.Resources.ContinuousQuery);

                UpdateUIState();

                await continousQueryControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database -> Continuous Queries -> New
        async Task CreateContinuousQuery(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);

                createCqDialog.ResetCqForm();
                createCqDialog.InfluxDbClient = client;
                createCqDialog.Database = database;

                await createCqDialog.BindInfluxDataSources();

                if (createCqDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var cqParams = createCqDialog.CqResult;
                    var response = await client.CreateContinuousQueryAsync(cqParams);

                    if (response.Success)
                    {
                        await RefreshOpenContinuousQueryTabs(client, database);
                    }
                    else
                    {
                        DisplayError(response.Body);
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database -> Continuous Queries -> Delete
        async Task DropContinuousQuery(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);

                var cqList = await client.GetContinousQueriesAsync(database);
                var queries = cqList != null ? new List<InfluxDbContinuousQuery>(cqList) : new List<InfluxDbContinuousQuery>();

                if (queries.Count == 0)
                {
                    MessageBox.Show(Localization.T("There are no continuous queries to delete."),
                        Localization.T("Drop Continuous Query"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                selectCqDialog.Bind(queries);

                if (selectCqDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                var selectedCq = selectCqDialog.SelectedCq;
                if (selectedCq == null) return;

                if (MessageBox.Show(Localization.Format("Drop CQ: {0}?", selectedCq.Name), Localization.T("Confirm Drop"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                {
                    return;
                }

                var response = await client.DropContinuousQueryAsync(database, selectedCq.Name);

                if (response.Success)
                {
                    await RefreshOpenContinuousQueryTabs(client, database);
                }
                else
                {
                    DisplayError(response.Body);
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Reloads any open CQ tabs that match the given connection and database
        async Task RefreshOpenContinuousQueryTabs(InfluxDbClient client, string database)
        {
            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Controls.Count == 0) continue;

                var cqControl = tab.Controls[0] as ContinuousQueryControl;
                if (cqControl == null) continue;
                if (cqControl.InfluxDbClient != client) continue;
                if (!string.Equals(cqControl.Database, database, StringComparison.Ordinal)) continue;

                await cqControl.ExecuteRequestAsync();
            }
        }

        // Database -> Back Fill
        async Task RunBackFill(TreeNode node)
        {
            try
            {
                // Get the connection for this node
                var connection = GetConnection(node);

                // Get the active client for this connection
                var client = GetClient(connection);
                var database = GetDatabaseName(node);

                // Pass client connection down
                backfillDialog.ResetBackFillForm();
                backfillDialog.InfluxDbClient = client;
                backfillDialog.Database = database;

                // Bind dynamic data
                await backfillDialog.BindInfluxDataSources();

                if (backfillDialog.ShowDialog() == DialogResult.OK)
                {
                    // Get the resulting Backfill params
                    var backfillParams = backfillDialog.BackfillResult;

                    // Create the CQ and get the response
                    var response = await client.BackfillAsync(database, backfillParams);

                    if (!response.Success)
                    {
                        DisplayError(response.Body);
                    }

                    UpdateUIState();
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database -> Drop Database
        async Task DropDatabase(TreeNode node)
        {
            try
            {
                var name = GetDatabaseName(node);

                // Confirm delete
                if (MessageBox.Show(Localization.Format("Drop database: {0}?", name), Localization.T("Confirm Drop"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    // Get the connection
                    var connection = GetConnection(node);

                    if (connection == null)
                    {
                        DisplayError(Localization.Format("Connection not found: {0}", createDatabaseDialog.ConnectionName), Localization.T("Error Creating Database"));
                        return;
                    }

                    // Get the active client for the connection
                    var client = GetClient(connection);

                    // Create the database and receive the response
                    var response = await client.DropDatabaseAsync(name);

                    if (!response.Success)
                    {
                        DisplayError(response.Body, Localization.T("Error Dropping Database"));
                        return;
                    }

                    // Remove the database from the UI list
                    connectionsTreeView.BeginUpdate();
                    connectionsTreeView.Nodes.Remove(node);
                    connectionsTreeView.EndUpdate();
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Database|Measurement -> Import Line Protocol
        async Task ImportLineProtocol(TreeNode node)
        {
            try
            {
                var type = GetNodeType(node);
                if (type != InfluxDbNodeTypes.Database && type != InfluxDbNodeTypes.Measurement)
                    return;

                var database = GetDatabaseName(node);
                if (string.IsNullOrWhiteSpace(database))
                {
                    DisplayError(Localization.T("Database name cannot be blank."));
                    return;
                }

                var connection = GetConnection(node);
                if (connection == null)
                {
                    DisplayError(Localization.T("Connection not found."));
                    return;
                }

                var client = GetClient(connection);
                if (client == null)
                {
                    DisplayError(Localization.T("Connection not found."));
                    return;
                }

                IEnumerable<InfluxDbRetentionPolicy> policies = null;
                try
                {
                    policies = await client.GetRetentionPoliciesAsync(database);
                }
                catch
                {
                    // Dialog still works with database-default RP only.
                }

                using (var dialog = new ImportLineProtocolDialog(database, policies))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    var raw = File.ReadAllText(dialog.FilePath, System.Text.Encoding.UTF8);
                    var lineCount = CountLineProtocolLines(raw);
                    if (lineCount == 0)
                    {
                        DisplayError(Localization.T("The selected file has no Line Protocol data to import."));
                        return;
                    }

                    var response = await client.WriteLineProtocolAsync(
                        database,
                        raw,
                        "ns",
                        dialog.RetentionPolicyName,
                        dialog.Consistency);

                    if (!response.Success)
                    {
                        DisplayError(response.Body, Localization.T("Error Importing Line Protocol"));
                        return;
                    }

                    var dbNode = type == InfluxDbNodeTypes.Database ? node : node.Parent;
                    if (dbNode != null)
                        await RefreshDatabase(dbNode);

                    MessageBox.Show(
                        Localization.Format("Imported {0} line(s) into \"{1}\".", lineCount, database),
                        Localization.T("Import Line Protocol"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        static int CountLineProtocolLines(string lineProtocol)
        {
            if (string.IsNullOrEmpty(lineProtocol)) return 0;

            var count = 0;
            using (var reader = new StringReader(lineProtocol))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    count++;
                }
            }
            return count;
        }

        #endregion Database

        #region Measurement

        // Measurement -> Show Series
        async Task ShowSeries(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);
                var measurement = node.Text;

                
                // Create a new series control
                var seriesControl = new SeriesControl();
                seriesControl.InfluxDbClient = client;
                seriesControl.Database = database;
                seriesControl.Measurement = measurement;

                // Add a tab with a series control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.{1}.series", connection.Name, measurement), 
                    seriesControl, Properties.Resources.Series);

                // Update UI
                UpdateUIState();

                // Render
                await seriesControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Measurement -> Tag Keys
        async Task ShowTagKeys(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);
                var measurement = node.Text;

                // Create a new tag keys control
                var tagKeysControl = new TagKeysControl();
                tagKeysControl.InfluxDbClient = client;
                tagKeysControl.Database = database;
                tagKeysControl.Measurement = measurement;

                // Add a tab with a tag keys control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.{1}.tag_keys", connection.Name, measurement), 
                    tagKeysControl, Properties.Resources.TagKeys);

                // Update UI
                UpdateUIState();

                // Render
                await tagKeysControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Measurement -> Tag Values
        async Task ShowTagValues(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);
                var measurement = node.Text;

                // Create a new tag values control
                var tagValuesControl = new TagValuesControl();
                tagValuesControl.InfluxDbClient = client;
                tagValuesControl.Database = database;
                tagValuesControl.Measurement = measurement;

                // Add a tab with a tag values control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.{1}.tag_values", connection.Name, measurement), 
                    tagValuesControl, Properties.Resources.TagValues);

                // Update UI
                UpdateUIState();

                // Render
                await tagValuesControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Measurement - Field Keys
        async Task ShowFieldKeys(TreeNode node)
        {
            try
            {
                var connection = GetConnection(node);
                var client = GetClient(connection);
                var database = GetDatabaseName(node);
                var measurement = node.Text;

                // Create a new query control
                var fieldKeysControl = new FieldKeysControl();
                fieldKeysControl.InfluxDbClient = client;
                fieldKeysControl.Database = database;
                fieldKeysControl.Measurement = measurement;

                // Add a tab with a query control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.{1}.field_keys", connection.Name, measurement), 
                    fieldKeysControl, Properties.Resources.FieldKeys);

                // Update UI
                UpdateUIState();

                // Render
                await fieldKeysControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Measurement - Drop Measurement
        async Task DropMeasurement(TreeNode node)
        {
            try
            {
                var name = node.Text;
                var dbName = GetDatabaseName(node);

                // Confirm delete
                if (MessageBox.Show(Localization.Format("Drop measurement: {0}?", name), Localization.T("Confirm Drop"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    // Get the connection
                    var connection = GetConnection(node);

                    if (connection == null)
                    {
                        DisplayError(Localization.Format("Connection not found: {0}", createDatabaseDialog.ConnectionName), Localization.T("Error Creating Database"));
                        return;
                    }

                    // Get the active client for the connection
                    var client = GetClient(connection);

                    // Create the database and receive the response
                    var response = await client.DropMeasurementAsync(dbName, name);

                    if (!response.Success)
                    {
                        DisplayError(response.Body, Localization.T("Error Dropping Measurement"));
                        return;
                    }

                    // Remove the series from the UI list
                    connectionsTreeView.BeginUpdate();
                    connectionsTreeView.Nodes.Remove(node);
                    connectionsTreeView.EndUpdate();
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Measurement -> Drop Series
        async Task DropSeries(TreeNode node)
        {
            try
            {
                var name = node.Text;
                var dbName = GetDatabaseName(node);

                // Confirm delete
                if (MessageBox.Show(Localization.Format("Drop series for: {0}?", name), Localization.T("Confirm Drop"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    // Get the connection
                    var connection = GetConnection(node);

                    if (connection == null)
                    {
                        DisplayError(Localization.Format("Connection not found: {0}", createDatabaseDialog.ConnectionName), Localization.T("Error Creating Database"));
                        return;
                    }

                    // Get the active client for the connection
                    var client = GetClient(connection);

                    // Create the database and receive the response
                    var response = await client.DropSeriesAsync(dbName, name);

                    if (!response.Success)
                    {
                        DisplayError(response.Body, Localization.T("Error Dropping Series"));
                        return;
                    }

                    // Remove the series from the UI list
                    connectionsTreeView.BeginUpdate();
                    node.Nodes.Clear();
                    connectionsTreeView.EndUpdate();
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        #endregion Measurement

        #region Queries/Requests

        // Database|Measurement -> New Query
        void NewQuery(TreeNode node)
        {
            try
            {
                // Get the connection for this node
                var connection = GetConnection(node);

                // Get the active client for this connection
                var client = GetClient(connection);

                // Some dynamic values
                string database = null;
                string measurement = "measurement";

                // Figure out what type of node was clicked
                switch (GetNodeType(node))
                {
                    // Connection
                    case InfluxDbNodeTypes.Connection:
                        return;

                    // Database
                    case InfluxDbNodeTypes.Database:
                        database = GetDatabaseName(node);
                        break;

                    // Measurement
                    case InfluxDbNodeTypes.Measurement:
                        database = GetDatabaseName(node);
                        measurement = node.Text;
                        break;
                }

                // Create a new query control
                var queryControl = new QueryControl();
                queryControl.InfluxDbClient = client;
                queryControl.Database = database;
                queryControl.EditorText = string.Format("SELECT * FROM \"{0}\" WHERE time > now() - 5m LIMIT {1} OFFSET {2}",
                    measurement, InfluxDbHelper.DefaultQueryLimit, InfluxDbHelper.DefaultQueryOffset);

                // Add a tab with a query control in it
                tabControl.AddTabWithControl(connection.Name + "." + database, queryControl, Properties.Resources.RunQuery);

                // Update UI
                UpdateUIState();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Queries -> Show Queries
        async Task ShowQueries(TreeNode node)
        {
            try
            {
                // Get the connection for this node
                var connection = GetConnection(node);

                // Get the active client for this connection
                var client = GetClient(connection);

                // Create a new contiuous query control
                var runningQueriesControl = new RunningQueriesControl();
                runningQueriesControl.InfluxDbClient = client;

                // Add a tab with a query control in it
                tabControl.AddTabWithControl(Localization.Format("{0}.queries", connection.Name),
                    runningQueriesControl, Properties.Resources.ShowQueries);

                UpdateUIState();

                await runningQueriesControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Executes the current request control's request
        async Task ExecuteCurrentRequest()
        {
            // Execute the query
            try
            {
                // Get the selected tab
                var tab = tabControl.SelectedTab;
                if (tab == null) return;

                // Get the request control for this tab
                RequestControl requestControl = null;

                foreach (Control c in tabControl.SelectedTab.Controls)
                {
                    if (c is RequestControl)
                    {
                        requestControl = (RequestControl)c;
                        break;
                    }
                }

                if (requestControl == null) return;
                await requestControl.ExecuteRequestAsync();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        #endregion Queries/Requests

        #endregion Commands

        #region Settings

        // Load application settings
        void ApplySettings()
        {
            PopulateFormatCombo(timeFormatComboBox, AppSettings.TimeFormats, Settings.TimeFormat);
            PopulateFormatCombo(dateFormatComboBox, AppSettings.DateFormats, Settings.DateFormat);

            // Apply untrusted SSL
            allowUntrustedSSLToolStripMenuItem.Checked = Settings.AllowUntrustedSsl;

            englishToolStripMenuItem.Checked = !Localization.IsChinese;
            chineseToolStripMenuItem.Checked = Localization.IsChinese;
        }

        static void PopulateFormatCombo(ToolStripComboBox combo, string[] formats, string selected)
        {
            combo.Items.Clear();
            foreach (var format in formats)
                combo.Items.Add(format);

            if (!string.IsNullOrWhiteSpace(selected) && !combo.Items.Contains(selected))
                combo.Items.Add(selected);

            var index = string.IsNullOrWhiteSpace(selected) ? -1 : combo.Items.IndexOf(selected);
            combo.SelectedIndex = index >= 0 ? index : 0;
        }

        #endregion Settings

        #region User Interface

        // Updates the state of the tool strip to match the current user interaction
        void UpdateUIState()
        {
            var node = connectionsTreeView.SelectedNode;
            var type = node != null ? GetNodeType(node) : InfluxDbNodeTypes.LoadingPlacholder;
            var canRunQeury = CanRunQuery();

            #region File Menu

            // Reset
            refreshToolStripMenuItem.Enabled = false;
            runQueryToolStripMenuItem.Enabled = false;
            newQueryToolStripMenuItem2.Enabled = false;
            showQueriesToolStripMenuItem.Enabled = false;

            if (node != null)
            {
                refreshToolStripMenuItem.Enabled = true;
            }

            // Update run query based on whether or not a request control is currently in focus
            runQueryToolStripMenuItem.Enabled = canRunQeury;
            newQueryToolStripMenuItem2.Enabled = type == InfluxDbNodeTypes.Database || type == InfluxDbNodeTypes.Measurement;
            showQueriesToolStripMenuItem.Enabled = type == InfluxDbNodeTypes.Connection;

            #endregion File Menu

            #region Tool Strip

            // Start out by clearing all button states
            disconnectButton.Enabled = false;
            showQueriesButton.Enabled = false;
            showPoliciesButton.Enabled = false;
            showUsersButton.Enabled = false;
            showStatsButton.Enabled = false;
            showDiagnosticsButton.Enabled = false;
            refreshButton.Enabled = false;
            runQueryButton.Enabled = false;
            newQueryButton.Enabled = false;
            createDatabaseButton.Enabled = false;
            continuousQueryButton.Enabled = false;
            backFillButton.Enabled = false;
            dropDatabaseButton.Enabled = false;
            tagKeysButton.Enabled = false;
            tagValuesButton.Enabled = false;
            fieldKeysButton.Enabled = false;
            showSeriesButton.Enabled = false;
            dropSeriesButton.Enabled = false;
            dropMeasurementButton.Enabled = false;

            // If a node is selected, update button states according to selected node
            if (node != null)
            {
                // Update button states accordingly
                disconnectButton.Enabled = true;
                refreshButton.Enabled = type == InfluxDbNodeTypes.Connection || type == InfluxDbNodeTypes.Database;
                newQueryButton.Enabled = type == InfluxDbNodeTypes.Database || type == InfluxDbNodeTypes.Measurement;
                showQueriesButton.Enabled = type == InfluxDbNodeTypes.Connection;
                showPoliciesButton.Enabled = type == InfluxDbNodeTypes.Database;
                showUsersButton.Enabled = type == InfluxDbNodeTypes.Connection;
                showStatsButton.Enabled = type == InfluxDbNodeTypes.Connection;
                showDiagnosticsButton.Enabled = type == InfluxDbNodeTypes.Connection;
                createDatabaseButton.Enabled = type == InfluxDbNodeTypes.Connection;
                continuousQueryButton.Enabled = type == InfluxDbNodeTypes.Database;
                backFillButton.Enabled = type == InfluxDbNodeTypes.Database;
                dropDatabaseButton.Enabled = type == InfluxDbNodeTypes.Database && !InfluxDbHelper.IsInternalDatabase(GetDatabaseName(node));
                tagKeysButton.Enabled = type == InfluxDbNodeTypes.Measurement;
                tagValuesButton.Enabled = type == InfluxDbNodeTypes.Measurement;
                fieldKeysButton.Enabled = type == InfluxDbNodeTypes.Measurement;
                showSeriesButton.Enabled = type == InfluxDbNodeTypes.Measurement;
                dropSeriesButton.Enabled = type == InfluxDbNodeTypes.Measurement;
                dropMeasurementButton.Enabled = type == InfluxDbNodeTypes.Measurement;
            }

            // Update run query based on whether or not a request control is currently in focus
            runQueryButton.Enabled = canRunQeury;

            #endregion Tool Strip

            #region Context Menus

            showQueriesContextMenuItem.Enabled = type == InfluxDbNodeTypes.Connection;
            retentionPoliciesToolStripMenuItem.Enabled = type == InfluxDbNodeTypes.Database;
            continousQueriesToolStripMenuItem.Enabled = type == InfluxDbNodeTypes.Database;
            backFillToolStripMenuItem.Enabled = type == InfluxDbNodeTypes.Database;
            dropDatabaseToolStripMenuItem.Enabled = type == InfluxDbNodeTypes.Database && !InfluxDbHelper.IsInternalDatabase(GetDatabaseName(node));

            #endregion Context Menus
        }

        // Determines whether or not a query is in focus that can run
        bool CanRunQuery()
        {
            return tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0 && tabControl.SelectedTab.Controls[0] is RequestControl;
        }

        #endregion User Interface

        #region Connections

        #region Utility

        // Gets a connection given its tree node.
        InfluxDbConnection GetConnection(TreeNode node)
        {
            TreeNode current = node;

            while (current.Parent != null)
            {
                current = current.Parent;
            }

            return current.Tag as InfluxDbConnection;
        }

        // Gets the real InfluxDB database name for a database or measurement node.
        string GetDatabaseName(TreeNode node)
        {
            if (node == null) return null;

            switch (GetNodeType(node))
            {
                case InfluxDbNodeTypes.Database:
                    return node.Tag as string ?? node.Text;

                case InfluxDbNodeTypes.Measurement:
                    if (node.Parent == null) return null;
                    return node.Parent.Tag as string ?? node.Parent.Text;

                default:
                    return null;
            }
        }

        // Gets the active InfluxDB for a given connection
        InfluxDbClient GetClient(InfluxDbConnection connection)
        {
            foreach (var client in ActiveClients)
            {
                if (client.Connection.Id == connection.Id || client.Connection == connection) return client;
            }

            return null;
        }

        #endregion Utility

        #region Rendering

        // Updates and shows the connection dialog
        async Task ShowConnectionsDialog()
        {
            // Update the current list of connections in the dialog
            manageConnectionsDialog.RedrawConnections();

            // Show the dialog
            if (manageConnectionsDialog.ShowDialog() == DialogResult.OK)
            {
                // Get the selected connection
                var c = manageConnectionsDialog.SelectedConnection;

                // Add the selected connection to the active connection view
                if (c != null) await RenderConnection(c);
            }
        }

        // Adds an InfluxDB connection to the active list
        async Task RenderConnection(InfluxDbConnection connection)
        {
            try
            {
                // Add the connection
                var connectionNode = CreateTreeNode(connection.Name, InfluxDbNodeTypes.Connection, connection);
                connectionNode.ContextMenuStrip = connectionsContextMenu;
                connectionsTreeView.Nodes.Add(connectionNode);

                // Create an active client for this connection
                var client = InfluxDbClientFactory.Create(connection);
                ActiveClients.Add(client);

                // Render the connection in the view
                await RenderConnectionDetails(connectionNode, connection);

                // If there are currently no selected nodes, select the new connection
                if (connectionsTreeView.SelectedNode == null) connectionsTreeView.SelectedNode = connectionNode;
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Render the connection details of a top-level connection node in the active connections list
        async Task RenderConnectionDetails(TreeNode connectionNode, InfluxDbConnection connection, bool update = false)
        {
            try
            {
                // If this is an update, update values and clear child nodes and redraw
                if (update)
                {
                    connectionNode.Text = connection.Name;
                    connectionNode.Collapse();
                    connectionNode.Nodes.Clear();
                }

                // Get the client for this connection
                var client = GetClient(connection);

                // List all databases under this connection
                await RenderDatabases(connectionNode, client);
                connectionNode.Expand();
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Renders the database nodes under a connection node
        async Task RenderDatabases(TreeNode connectionNode, InfluxDbClient client)
        {
            try
            {
                // Query all databases for this connection
                var dbNames = (await client.GetDatabaseNamesAsync()).ToList();

                // Clear current children
                connectionNode.Nodes.Clear();

                // Iterate through returned databases and add their details
                foreach (var dbName in dbNames)
                {
                    // Hide InfluxDB's internal system database
                    if (InfluxDbHelper.IsInternalDatabase(dbName)) continue;

                    var dbNode = CreateTreeNode(dbName, InfluxDbNodeTypes.Database, dbName);
                    dbNode.ContextMenuStrip = databaseContextMenu;
                    connectionNode.Nodes.Add(dbNode);

                    // Don't render measurement, instead include a loading place holder
                    var placeholderNode = CreateTreeNode(Localization.T("loading..."), InfluxDbNodeTypes.LoadingPlacholder);
                    dbNode.Nodes.Add(placeholderNode);
                }
            }
            catch(Exception ex)
            {
                DisplayException(ex);
            }
        }

        // Renders the measurement nodes under a database node
        async Task RenderMeasurements(TreeNode dbNode, InfluxDbClient client)
        {
            try
            {
                var dbName = GetDatabaseName(dbNode);

                // Add measurements
                var measurementNames = await client.GetMeasurementNamesAsync(dbName);

                // Clear current children
                dbNode.Nodes.Clear();

                // Render children
                foreach (var measurementName in measurementNames)
                {
                    var measurementNode = CreateTreeNode(measurementName, InfluxDbNodeTypes.Measurement);
                    measurementNode.ContextMenuStrip = measurementContextMenu;
                    dbNode.Nodes.Add(measurementNode);
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
            }
        }
        
        #endregion Rendering

        #endregion Connections

        #region Utility

        // Gets the InfluxDB type from a given tere node.
        InfluxDbNodeTypes GetNodeType(TreeNode node)
        {
            return (InfluxDbNodeTypes)node.ImageIndex;
        }

        // Determines whether or not a tree node is a specific InfluxDB type
        bool IsNodeType(TreeNode node, InfluxDbNodeTypes type)
        {
            return node.ImageIndex == (int)type;
        }

        // Returns a specific tree node type
        TreeNode CreateTreeNode(string text, InfluxDbNodeTypes type, object tag = null)
        {
            return new TreeNode(text, (int)type, (int)type) { Tag = tag };
        }

        #endregion Utility

        #region Static

        /// <summary>
        /// Opens the Back Fill dialog and allows a user to run a back fill query.
        /// </summary>
        public static async Task RunBackFill()
        {
            if (instance == null || instance.connectionsTreeView.SelectedNode == null) return;
            await instance.RunBackFill(instance.connectionsTreeView.SelectedNode);
        }

        /// <summary>
        /// Sets the text of the applications status bar.
        /// </summary>
        /// <param name="text">The text to set.</param>
        public static void SetStatus(string text)
        {
            if (instance == null) return;
            instance.statusLabel.Text = text;
        }

        /// <summary>
        /// Displays an unexpected exception that occured in the application to the user.
        /// </summary>
        /// <param name="ex">The exception that occured.</param>
        /// <param name="caption">The optional error message caption to display.</param>
        /// <param name="stackTrace">Determines whether or not the exception's stack trace should be displayed.</param>
        public static void DisplayException(Exception ex, string caption = null, bool stackTrace = false)
        {
            if (ex == null) throw new ArgumentNullException("ex");

            // Workaround for KILL QUERY command
            if (ex is InfluxData.Net.Common.Infrastructure.InfluxDataApiException)
            {
                var apiEx = (InfluxData.Net.Common.Infrastructure.InfluxDataApiException)ex;
                if (apiEx.ResponseBody.Contains("query interrupted")) return;
            }

            var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            if (stackTrace) message += "\n\n{0}" + ex.StackTrace;
            message = Localization.T(message);

            try { Log.ErrorFormat("{0}", ex); }
            catch { }

            MessageBox.Show(message, Localization.T(caption ?? "Unexpected Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Displays an error message to the user.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        /// <param name="caption">The optional error caption to display.</param>
        public static void DisplayError(string message, string caption = null)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentNullException("message");

            try { Log.ErrorFormat("{0}", message); }
            catch { }

            MessageBox.Show(Localization.T(message), Localization.T(caption ?? "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        #endregion Static

        #endregion Methods
    }
}
