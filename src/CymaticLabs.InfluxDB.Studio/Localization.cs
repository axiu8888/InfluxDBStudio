using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace CymaticLabs.InfluxDB.Studio
{
    /// <summary>
    /// Applies English/Chinese UI translations and persists the selected language.
    /// </summary>
    public static class Localization
    {
        public const string English = "en";
        public const string Chinese = "zh-CN";

        static readonly Dictionary<string, string> ZhMap;

        public static string CurrentLanguage { get; private set; }

        public static bool IsChinese
        {
            get { return string.Equals(CurrentLanguage, Chinese, StringComparison.OrdinalIgnoreCase); }
        }

        static Localization()
        {
            CurrentLanguage = English;
            ZhMap = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                #region Menus and toolbar
                { "File", "文件" },
                { "Import", "导入" },
                { "Import Line Protocol...", "导入行协议..." },
                { "Import Line Protocol", "导入行协议" },
                { "Import Line Protocol into database \"{0}\"?", "将行协议导入到数据库 \"{0}\"？" },
                { "Imported {0} line(s) into \"{1}\".", "已导入 {0} 行到 \"{1}\"。" },
                { "The selected file has no Line Protocol data to import.", "所选文件没有可导入的行协议数据。" },
                { "Error Importing Line Protocol", "导入行协议失败" },
                { "Export Measurements...", "按条件导出表..." },
                { "Export Measurements", "按条件导出表" },
                { "Delete Measurements...", "按条件删除表..." },
                { "Delete Measurements", "按条件删除表" },
                { "Preview SQL", "查看 SQL" },
                { "(optional tag filters only; fields are not supported by DELETE)", "（可选标签过滤；DELETE 不支持字段条件）" },
                { "(InfluxDB DELETE does not support retention policy; uses database default)", "（InfluxDB 的 DELETE 不支持指定保留策略，始终使用库默认策略）" },
                { "Warning: deleted points cannot be recovered. DROP MEASUREMENT is not used; only matching points are removed.", "警告：删除的数据点无法恢复。不会执行 DROP MEASUREMENT，只删除匹配条件的数据点。" },
                { "Permanently delete matching points from {0} measurement(s) in database '{1}'?\n\nFrom: {2}\nTo: {3}\n\nThis cannot be undone.", "将永久删除数据库 '{1}' 中 {0} 张表的匹配数据点？\n\n从: {2}\n到: {3}\n\n此操作不可撤销。" },
                { "Deleting from {0}...", "正在删除 {0}..." },
                { "Done. Succeeded: {0}, failed: {1}.", "完成。成功: {0}，失败: {1}。" },
                { "Delete cancelled.", "已取消删除。" },
                { "Delete failed.", "删除失败。" },
                { "Measurements:", "表:" },
                { "Select None", "全不选" },
                { "From:", "从:" },
                { "To:", "到:" },
                { "Extra WHERE:", "额外条件:" },
                { "(optional filters only; multiple lines OK, e.g. host = 'server01')", "（可选过滤条件，可多行，例如 host = 'server01'）" },
                { "(optional filters only, e.g. host = 'server01')", "（可选过滤条件，例如 host = 'server01'）" },
                { "LIMIT:", "LIMIT:" },
                { "(optional; leave blank for no LIMIT)", "（可选；留空表示不加 LIMIT）" },
                { "Formats:", "导出格式:" },
                { "Output folder:", "输出目录:" },
                { "Select output folder", "选择输出目录" },
                { "Starting...", "开始..." },
                { "Cancelling...", "正在取消..." },
                { "Export cancelled.", "已取消导出。" },
                { "Export failed.", "导出失败。" },
                { "Querying {0}...", "正在查询 {0}..." },
                { "Querying {0}... chunk {1}, {2} points", "正在查询 {0}... 第 {1} 批，已 {2} 点" },
                { "[{0}/{1}] {2}", "[{0}/{1}] {2}" },
                { "Done. Succeeded: {0}, empty skipped: {1}, failed: {2}.", "完成。成功: {0}，无数据跳过: {1}，失败: {2}。" },
                { "Please select at least one measurement.", "请至少选择一张表。" },
                { "From time must be earlier than To time.", "开始时间必须早于结束时间。" },
                { "Please select at least one export format.", "请至少选择一种导出格式。" },
                { "Please select an output folder.", "请选择输出目录。" },
                { "LIMIT must be a positive integer, or left blank.", "LIMIT 必须是正整数，或留空。" },
                { "No measurements found in this database.", "该数据库中没有表。" },
                { "Connection not found.", "未找到连接。" },
                { "File:", "文件:" },
                { "Browse...", "浏览..." },
                { "Retention Policy:", "保留策略:" },
                { "Consistency:", "一致性:" },
                { "(Database default)", "（数据库默认）" },
                { " (default)", "（默认）" },
                { "Please select a Line Protocol file.", "请选择行协议文件。" },
                { "The selected file does not exist.", "所选文件不存在。" },
                { "Export", "导出" },
                { "Settings", "设置" },
                { "Exit", "退出" },
                { "Connections", "连接" },
                { "Refresh", "刷新" },
                { "Manage", "管理" },
                { "Query", "查询" },
                { "New Query", "新建查询" },
                { "Run", "运行" },
                { "Show Queries", "显示查询" },
                { "Time Format", "时间格式" },
                { "Date Format", "日期格式" },
                { "Allow Untrusted SSL", "允许不受信任的 SSL" },
                { "Language", "语言" },
                { "English", "English" },
                { "中文", "中文" },
                { "Help", "帮助" },
                { "About", "关于" },
                { "Manage Connections", "管理连接" },
                { "Disconnect", "断开连接" },
                { "Show Retention Policies", "显示保留策略" },
                { "Retention Policies", "保留策略" },
                { "Drop Retention Policy", "删除保留策略" },
                { "Select a retention policy to delete:", "选择要删除的保留策略:" },
                { "There are no retention policies to delete.", "没有可删除的保留策略。" },
                { "A database must have at least one retention policy.", "每个数据库至少需要保留一条保留策略。" },
                { "Yes", "是" },
                { "No", "否" },
                { "{0}.{1} policies", "{0}.{1} 策略" },
                { "Show Users", "显示用户" },
                { "Show Statistics", "显示统计" },
                { "Show Diagnostics", "显示诊断" },
                { "Run Query", "运行查询" },
                { "Create Database", "创建数据库" },
                { "Show Continuous Queries", "显示连续查询" },
                { "Show Continous Queries", "显示连续查询" },
                { "Continuous Queries", "连续查询" },
                { "View", "查看" },
                { "New", "新建" },
                { "Delete", "删除" },
                { "Run Backfill", "运行回填" },
                { "Drop Database", "删除数据库" },
                { "Show Tag Keys", "显示标签键" },
                { "Show Tag Values", "显示标签值" },
                { "Show Field Keys", "显示字段键" },
                { "Show Series", "显示序列" },
                { "Show Show Series", "显示序列" },
                { "Drop Series", "删除序列" },
                { "Drop Measurement", "删除 Measurement" },
                { "Diagnostics", "诊断" },
                { "Close", "关闭" },
                { "Close All But This", "关闭其他" },
                { "Close Other", "关闭其他" },
                { "Close All", "全部关闭" },
                { "2:30:45 PM  12 Hour", "2:30:45 PM  12 小时" },
                { "     14:30:45  24 Hour", "     14:30:45  24 小时" },
                { "12/31/2016  mm/dd/yyyy", "12/31/2016  mm/dd/yyyy" },
                { "31/12/2016  dd/mm/yyyy ", "31/12/2016  dd/mm/yyyy " },
                #endregion

                #region Connection / database / measurement menus
                { "Connection Settings", "连接设置" },
                { "Manage InfluxDB Connections", "管理 InfluxDB 连接" },
                { "Create", "创建" },
                { "Edit", "编辑" },
                { "Remove", "删除" },
                { "Connect", "连接" },
                { "Name", "名称" },
                { "Address", "地址" },
                { "Cancel", "取消" },
                { "OK", "确定" },
                { "Save", "保存" },
                { "Test", "测试" },
                { "Ping", "Ping" },
                { "Use SSL", "使用 SSL" },
                { "Address:", "地址:" },
                { "Security:", "安全:" },
                { "Database:", "数据库:" },
                { "Password:", "密码:" },
                { "User Name:", "用户名:" },
                { "Name:", "名称:" },
                { "New Connection", "新建连接" },
                { "Specify the host and port of the InfluxDB server.", "指定 InfluxDB 服务器的主机和端口。" },
                { "Choose a connection name that will help you to identify this connection.", "请填写便于识别的连接名称。" },
                { "Optionally specify the name of a single database to connect to.\r\nThis can be useful when you don't have admin privileges to list databases.", "可选择填写单个数据库名称。\r\n在没有列出全部数据库权限时会很有用。" },
                { "Select a database from the list, or leave it blank to list all databases.\r\nClick refresh to load the list from the server, or type a name.", "从列表选择数据库，或留空以列出全部数据库。\r\n点击刷新可从服务器加载列表，也可直接输入名称。" },
                { "Refresh database list", "刷新数据库列表" },
                { "{0} (internal)", "{0}（内部）" },
                { "InfluxDB internal system database. Hidden unless entered manually.", "InfluxDB 内部系统数据库，一般无需使用；仅在手动指定时显示。" },
                { "Connection Name", "连接名称" },
                #endregion

                #region Dialogs
                { "Create User", "创建用户" },
                { "Edit User", "编辑用户" },
                { "Change Password", "修改密码" },
                { "Drop User", "删除用户" },
                { "Grant Privilege", "授予权限" },
                { "Edit Privilege", "编辑权限" },
                { "Privilege:", "权限:" },
                { "User:", "用户:" },
                { "Admin:", "管理员:" },
                { "Grant", "授予" },
                { "Create Continuous Query", "创建连续查询" },
                { "Details", "详细信息" },
                { "For:", "持续:" },
                { "Resample", "重采样" },
                { "Tags:", "标签:" },
                { "Destination:", "目标:" },
                { "Fill Type:", "填充类型:" },
                { "Source:", "来源:" },
                { "Every:", "每隔:" },
                { "Interval:", "间隔:" },
                { "CQ Name:", "CQ 名称:" },
                { "Run Back Fill Query", "运行回填查询" },
                { "Filters:", "过滤:" },
                { "From Time:", "开始时间:" },
                { "To Time:", "结束时间:" },
                { "Create Retention Policy", "创建保留策略" },
                { "Policy Name:", "策略名称:" },
                { "Duration:", "时长:" },
                { "Replication:", "副本数:" },
                { "Default:", "默认:" },
                { "Update", "更新" },
                { "About InfluxDB Studio", "关于 InfluxDB Studio" },
                { "Visual InfluxDB Management Tool", "可视化 InfluxDB 管理工具" },
                { "Visit the project website on", "项目主页：" },
                { "Copyright 2017 Michael Everett. All  rights reserved.", "Copyright 2017 Michael Everett. 保留所有权利。" },
                { "The program is provided AS IS with NO WARRANTY OF ANY KIND, INCLUDING THE WARRANTY OF DESIGN, MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.", "本程序按现状提供，不附带任何形式的保证，包括对设计、适销性和特定用途适用性的保证。" },
                { "Powered by ", "底层库：" },
                { "Interval", "间隔" },
                { "Fill Type", "填充类型" },
                { "Tags", "标签" },
                { "Destination", "目标" },
                { "Source", "来源" },
                { "Filters", "过滤" },
                { "Enable Resampling", "启用重采样" },
                { "Resample For", "重采样持续" },
                { "Resample Every", "重采样间隔" },
                { "Duration", "时长" },
                #endregion

                #region Controls
                { "Create CQ", "创建 CQ" },
                { "Edit CQ", "编辑 CQ" },
                { "Edit Continuous Query", "编辑连续查询" },
                { "Drop CQ", "删除 CQ" },
                { "Drop Continuous Query", "删除连续查询" },
                { "CQ Name", "CQ 名称" },
                { "Kill Query", "终止查询" },
                { "PID", "PID" },
                { "Database", "数据库" },
                { "tag keys: ", "标签键: " },
                { "Select Statistic:", "选择统计项:" },
                { "Select Database:", "选择数据库:" },
                { "Create Policy", "创建策略" },
                { "Alter Policy", "修改策略" },
                { "Drop Policy", "删除策略" },
                { "Policy Name", "策略名称" },
                { "Shard Group Duration", "分片组时长" },
                { "Replica N", "副本数" },
                { "Default", "默认" },
                { "Export All", "导出全部" },
                { "Export Selected", "导出所选" },
                { "Copy Cell", "复制单元格" },
                { "Copy Row(s)", "复制行" },
                { "Copy as JSON", "复制为 JSON" },
                { "Copy as Key=Value", "复制为 Key=Value" },
                { "Copy Column", "复制列" },
                { "Copy Header", "复制表头" },
                { "Copy All Headers", "复制全部表头" },
                { "Nothing to copy.", "没有可复制的内容。" },
                { "CSV", "CSV" },
                { "Excel", "Excel" },
                { "JSON", "JSON" },
                { "Line Protocol", "行协议" },
                { "System", "系统" },
                { "Uptime:", "运行时间:" },
                { "Started:", "启动时间:" },
                { "Current Time:", "当前时间:" },
                { "PID:", "PID:" },
                { "Build", "构建" },
                { "Version:", "版本:" },
                { "Commit:", "提交:" },
                { "Branch:", "分支:" },
                { "Go Runtime", "Go 运行时" },
                { "OS:", "操作系统:" },
                { "Max Procs:", "最大进程数:" },
                { "Architecture:", "架构:" },
                { "Network", "网络" },
                { "Hostname:", "主机名:" },
                { "results", "结果" },
                { "#", "#" },
                { "tagKey", "标签键" },
                { "tagValue", "标签值" },
                { "fieldKey", "字段键" },
                { "fieldType", "字段类型" },
                { "Admin", "管理员" },
                { "Read", "读" },
                { "Write", "写" },
                { "All", "全部" },
                #endregion

                #region Runtime messages
                { "loading...", "加载中..." },
                { "Confirm Drop", "确认删除" },
                { "Confirm Delete", "确认删除" },
                { "Confirm User Drop", "确认删除用户" },
                { "Drop CQ: {0}?", "删除 CQ: {0}？" },
                { "Unable to parse this continuous query for editing.", "无法解析该连续查询，不能编辑。" },
                { "The updated continuous query was created, but the original could not be dropped: {0}", "已创建更新后的连续查询，但无法删除原来的 CQ: {0}" },
                { "There are no continuous queries to delete.", "没有可删除的连续查询。" },
                { "Select a continuous query to delete:", "选择要删除的连续查询:" },
                { "Query:", "查询:" },
                { "Delete connection: {0}?", "删除连接: {0}？" },
                { "Drop retention policy: {0}?", "删除保留策略: {0}？" },
                { "Drop user: {0}?", "删除用户: {0}？" },
                { "Drop database: {0}?", "删除数据库: {0}？" },
                { "Drop measurement: {0}?", "删除 Measurement: {0}？" },
                { "Drop series for: {0}?", "删除序列: {0}？" },
                { "Connection Exists", "连接已存在" },
                { "A connection already exists with name: {0}", "已存在同名连接: {0}" },
                { "Error Creating Database", "创建数据库失败" },
                { "Database name cannot be blank.", "数据库名称不能为空。" },
                { "A database named '{0}' already exists.", "数据库 '{0}' 已存在。" },
                { "Error Dropping Database", "删除数据库失败" },
                { "Error Dropping Measurement", "删除 Measurement 失败" },
                { "Error Dropping Series", "删除序列失败" },
                { "Connection not found: {0}", "未找到连接: {0}" },
                { "Unexpected Error", "意外错误" },
                { "Error", "错误" },
                { "Success", "成功" },
                { "Failure", "失败" },
                { "Connection Successful!", "连接成功！" },
                { "Pong", "Pong" },
                { "Response Time: {0:0} ms\nInfluxDB Version: {1}", "响应时间: {0:0} 毫秒\nInfluxDB 版本: {1}" },
                { "There was an error connecting to the server.", "连接服务器时出错。" },
                { "Create Error", "创建失败" },
                { "User already exists: {0}", "用户已存在: {0}" },
                { "Bad User Name", "用户名无效" },
                { "User name cannot be blank.", "用户名不能为空。" },
                { "Bad Password", "密码无效" },
                { "Password cannot be blank.", "密码不能为空。" },
                { "You must supply a database to grant a privilege for.", "必须指定要授予权限的数据库。" },
                { "You must supply a database to update a privilege for.", "必须指定要更新权限的数据库。" },
                { "Retention Policy name cannot be blank.", "保留策略名称不能为空。" },
                { "Duration cannot be blank. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.", "时长不能为空。请使用 InfluxDB 时间间隔，例如：1d、2h、10m、30s。" },
                { "Duration value is invalid. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.", "时长无效。请使用 InfluxDB 时间间隔，例如：1d、2h、10m、30s。" },
                { "Continuous Query name cannot be blank.", "连续查询名称不能为空。" },
                { "Destination cannot be blank.", "目标不能为空。" },
                { "Source cannot be blank.", "来源不能为空。" },
                { "Confirm", "确认" },
                { "Source is the same as the Destination. These are typically different values for a Continous Query. Are you sure that you want to have duplicate values?", "来源与目标相同。连续查询通常应使用不同的值。确定要使用相同值吗？" },
                { "Source is the same as the Destination. These are typically different values for a Backfill Query. Are you sure that you want to have duplicate values?", "来源与目标相同。回填查询通常应使用不同的值。确定要使用相同值吗？" },
                { "Interval cannot be blank. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.", "间隔不能为空。请使用 InfluxDB 时间间隔，例如：1d、2h、10m、30s。" },
                { "Interval value is invalid. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.", "间隔无效。请使用 InfluxDB 时间间隔，例如：1d、2h、10m、30s。" },
                { "Resample Every interval value is invalid. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.", "重采样 EVERY 间隔无效。请使用 InfluxDB 时间间隔，例如：1d、2h、10m、30s。" },
                { "Resample For interval value is invalid. It should be an InfluxDB time interval value such as: 1d, 2h, 10m, 30s, etc.", "重采样 FOR 间隔无效。请使用 InfluxDB 时间间隔，例如：1d、2h、10m、30s。" },
                { "SubQuery cannot be blank.", "子查询不能为空。" },
                { "'From Time' is later than 'To Time'. 'From Time' should come before 'To Time'.", "开始时间晚于结束时间。开始时间应早于结束时间。" },
                { "No InfluxDB client available.", "没有可用的 InfluxDB 客户端。" },
                { "Group", "分组" },
                { "Results", "结果" },
                { "results: {0}, response time: {1:0} ms", "结果: {0} 行，响应时间: {1:0} 毫秒" },
                { "  (LIMIT {0} added automatically)", "  （已自动加上 LIMIT {0}）" },
                { "  (OFFSET {0} added automatically)", "  （已自动加上 OFFSET {0}）" },
                { "  (LIMIT {0} OFFSET {1} added automatically)", "  （已自动加上 LIMIT {0} OFFSET {1}）" },
                { "Beautify SQL (multiple lines)", "美化 SQL（多行）" },
                { "Compact SQL (one line)", "压缩 SQL（单行）" },
                { "Run SQL", "运行 SQL" },
                { "Run Selected SQL", "运行选中 SQL" },
                { "Add to Favorites...", "添加到收藏..." },
                { "Add to Favorites", "添加到收藏" },
                { "Favorites", "收藏" },
                { "Query History", "查询历史" },
                { "View All History...", "查看全部历史..." },
                { "Search:", "搜索:" },
                { "Time", "时间" },
                { "Clear All", "全部清除" },
                { "Clear all query history?", "清除全部查询历史？" },
                { "(no matching history)", "（无匹配历史）" },
                { "(none)", "（无）" },
                { "Select", "选择" },
                { "Name (optional):", "名称（可选）:" },
                { "SQL:", "SQL:" },
                { "Edit Favorite", "编辑收藏" },
                { "Edit History Query", "编辑历史查询" },
                { "SQL cannot be blank.", "SQL 不能为空。" },
                { "Delete favorite '{0}'?", "删除收藏 '{0}'？" },
                { "Delete this query from history?", "从历史记录中删除该查询？" },
                { "Cut", "剪切" },
                { "Copy", "复制" },
                { "Paste", "粘贴" },
                { "Select All", "全选" },
                { "Field", "字段" },
                { "Tag", "标签" },
                { "Function", "函数" },
                { "Keyword", "关键字" },
                { "Measurement", "表" },
                { "#edit your query / downsamplers here", "#在此编辑查询 / 降采样" },
                { "{0}.policies", "{0}.策略" },
                { "{0}.users", "{0}.用户" },
                { "{0}.statistics", "{0}.统计" },
                { "{0}.diagnostics", "{0}.诊断" },
                { "{0}.queries", "{0}.查询" },
                { "{0}.{1} CQs", "{0}.{1} 连续查询" },
                { "{0}.{1}.series", "{0}.{1}.序列" },
                { "{0}.{1}.tag_keys", "{0}.{1}.标签键" },
                { "{0}.{1}.tag_values", "{0}.{1}.标签值" },
                { "{0}.{1}.field_keys", "{0}.{1}.字段键" },
                { "JSON files|*.json|All files|*.*", "JSON 文件|*.json|所有文件|*.*" },
                { "CSV files|*.csv|All files|*.*", "CSV 文件|*.csv|所有文件|*.*" },
                { "Excel files|*.xlsx|All files|*.*", "Excel 文件|*.xlsx|所有文件|*.*" },
                { "Line Protocol files|*.lp|All files|*.*", "行协议文件|*.lp|所有文件|*.*" },
                { "Cannot export Line Protocol: the result has no measurement name. Use a SELECT query against a measurement.", "无法导出行协议：结果没有 measurement 名称。请对某个 measurement 执行 SELECT 查询。" },
                { "No Line Protocol lines were generated (need at least one field per point).", "没有生成任何行协议内容（每个 point 至少需要一个 field）。" },
                { "The destination measurement will store the results of the Continuous Query.\r\nSelect an existing measurement or type in the name of a new destination.", "目标 Measurement 将保存连续查询的结果。\n请选择已有 Measurement，或输入新的目标名称。" },
                { "This is the source measurement for data selected by the Continuous Query.\r\nSelect or type in the name of the existing measurement for the query.", "这是连续查询读取数据的源 Measurement。\n请选择或输入已有 Measurement 名称。" },
                { "The CQ's interval: 1h, 30m, 45s, etc.", "CQ 间隔，例如：1h、30m、45s。" },
                { "how empty values will be treated using: fill()", "空值处理方式，对应 fill()" },
                { "By default, all INTO queries convert any tags in the source measurement to fields in the destination measurement.\r\nType a comma-separated list of tag names to preserve in the destination measurement: tag1, tag2, tag3, etc.", "默认情况下，INTO 查询会把源 Measurement 的标签转成目标中的字段。\n请输入要保留的标签名，逗号分隔：tag1, tag2, tag3。" },
                { "The destination measurement will store the results of the Back Fill.\r\nSelect an existing measurement or type in the name of a new destination.", "目标 Measurement 将保存回填结果。\n请选择已有 Measurement，或输入新的目标名称。" },
                { "This is the source measurement for data selected by the Back Fill.\r\nSelect or type in the name of the existing measurement for the query.", "这是回填查询读取数据的源 Measurement。\n请选择或输入已有 Measurement 名称。" },
                { "The Back Fill's interval: 1h, 30m, 45s, etc.", "回填间隔，例如：1h、30m、45s。" },
                { "A comma-separated list of \"WHERE\" filters to apply to the query.", "应用到查询的 WHERE 过滤条件，逗号分隔。" },
                { "The duration of the retention policy: 1h, 30m, 45s, etc.", "保留策略时长，例如：1h、30m、45s。" },
                { "Use an EVERY interval in the RESAMPLE clause to specify the CQ’s execution interval.", "在 RESAMPLE 子句中用 EVERY 指定 CQ 的执行间隔。" },
                { "Use a FOR interval in the RESAMPLE clause to specify the length of the CQ’s time range.", "在 RESAMPLE 子句中用 FOR 指定 CQ 覆盖的时间范围。" },
                { "CQ's need at least one subquery/downsampler to be created.\r\nTo add additional subqueries/downsamplers that will run as part of the CQ,\r\nclick the \"Add Subquery\" button. Click the \"X\" button on the subquery tabs\r\nto remove unwanted subqueries.", "创建 CQ 至少需要一个子查询/降采样。\n要添加更多子查询，请点击“添加子查询”。\n点击子查询标签上的“X”可删除不需要的子查询。" },
                #endregion
            };
        }

        /// <summary>
        /// Reads the saved language (or OS default) and applies it before any UI is created.
        /// </summary>
        public static void ApplySavedCulture()
        {
            var language = AppSettings.ReadPortableLanguage();
            if (string.IsNullOrWhiteSpace(language))
            {
                language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                    ? Chinese
                    : English;
            }

            ApplyCulture(language);
        }

        public static void ApplyCulture(string language)
        {
            CurrentLanguage = string.Equals(language, Chinese, StringComparison.OrdinalIgnoreCase)
                ? Chinese
                : English;

            var culture = IsChinese ? new CultureInfo("zh-CN") : CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        public static void ChangeLanguage(string language)
        {
            var normalized = string.Equals(language, Chinese, StringComparison.OrdinalIgnoreCase)
                ? Chinese
                : English;

            if (string.Equals(normalized, CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                return;

            if (AppForm.Settings != null)
                AppForm.Settings.Language = normalized;
            ApplyCulture(normalized);
            Application.Restart();
        }

        public static string T(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsChinese) return text;

            string translated;
            if (ZhMap.TryGetValue(text, out translated)) return translated;

            var normalized = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            if (ZhMap.TryGetValue(normalized, out translated)) return translated;

            normalized = text.Replace("\r\n", "\n");
            if (ZhMap.TryGetValue(normalized, out translated)) return translated;

            return text;
        }

        public static string Format(string englishFormat, params object[] args)
        {
            return string.Format(T(englishFormat), args);
        }

        public static void Apply(IComponent component)
        {
            if (component == null || !IsChinese) return;

            var control = component as Control;
            if (control != null) ApplyControl(control);

            ApplyComponentFields(component);
        }

        static void ApplyControl(Control control)
        {
            if (control == null) return;

            if (ShouldTranslateControlText(control))
                control.Text = T(control.Text);

            var listView = control as ListView;
            if (listView != null)
            {
                foreach (ColumnHeader column in listView.Columns)
                    column.Text = T(column.Text);
            }

            var toolStrip = control as ToolStrip;
            if (toolStrip != null) ApplyToolStrip(toolStrip);

            if (control.ContextMenuStrip != null)
                ApplyToolStrip(control.ContextMenuStrip);

            foreach (Control child in control.Controls)
                ApplyControl(child);
        }

        static void ApplyToolStrip(ToolStrip strip)
        {
            if (strip == null) return;
            strip.Text = T(strip.Text);

            foreach (ToolStripItem item in strip.Items)
                ApplyToolStripItem(item);
        }

        static void ApplyToolStripItem(ToolStripItem item)
        {
            if (item == null) return;

            var combo = item as ToolStripComboBox;
            if (combo != null)
            {
                var selected = combo.SelectedIndex;
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    var value = combo.Items[i] as string;
                    if (value != null) combo.Items[i] = T(value);
                }
                if (selected >= 0 && selected < combo.Items.Count)
                    combo.SelectedIndex = selected;
            }
            else
            {
                item.Text = T(item.Text);
            }

            item.ToolTipText = T(item.ToolTipText);

            var dropDown = item as ToolStripDropDownItem;
            if (dropDown != null)
            {
                foreach (ToolStripItem child in dropDown.DropDownItems)
                    ApplyToolStripItem(child);
            }
        }

        static void ApplyComponentFields(object component)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in component.GetType().GetFields(flags))
            {
                var value = field.GetValue(component);
                if (value == null) continue;

                var toolTip = value as ToolTip;
                if (toolTip != null)
                {
                    toolTip.ToolTipTitle = T(toolTip.ToolTipTitle);
                    continue;
                }

                var column = value as ColumnHeader;
                if (column != null)
                {
                    column.Text = T(column.Text);
                    continue;
                }

                var strip = value as ToolStrip;
                if (strip != null)
                    ApplyToolStrip(strip);
            }
        }

        static bool ShouldTranslateControlText(Control control)
        {
            if (control is TextBox || control is RichTextBox || control is MaskedTextBox)
                return false;
            if (control is ComboBox || control is ListBox || control is NumericUpDown)
                return false;
            if (control is TreeView || control is ListView || control is DateTimePicker)
                return false;
            if (control.GetType().Name == "Scintilla")
                return false;

            // Designer leftover names used as hidden button text
            if (!string.IsNullOrEmpty(control.Text) && control.Text.IndexOf(' ') < 0 &&
                control.Text.Length > 0 && char.IsLower(control.Text[0]))
                return false;

            return true;
        }
    }
}
