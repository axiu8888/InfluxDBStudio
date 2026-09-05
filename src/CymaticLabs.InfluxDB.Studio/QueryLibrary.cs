using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using CymaticLabs.InfluxDB.Studio.Properties;

namespace CymaticLabs.InfluxDB.Studio
{
    /// <summary>
    /// A saved favorite or history query.
    /// </summary>
    public class SavedQuery
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Sql { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Persists query favorites and execution history.
    /// </summary>
    public static class QueryLibrary
    {
        public const int MaxHistory = 50;

        static List<SavedQuery> favorites;
        static List<SavedQuery> history;

        public static IList<SavedQuery> Favorites
        {
            get
            {
                EnsureLoaded();
                return favorites;
            }
        }

        public static IList<SavedQuery> History
        {
            get
            {
                EnsureLoaded();
                return history;
            }
        }

        public static SavedQuery AddFavorite(string name, string sql)
        {
            EnsureLoaded();
            var item = Create(name, sql);
            favorites.Insert(0, item);
            SaveFavorites();
            return item;
        }

        public static void UpdateFavorite(SavedQuery item)
        {
            if (item == null) return;
            EnsureLoaded();
            item.Timestamp = DateTime.Now;
            SaveFavorites();
        }

        public static void RemoveFavorite(Guid id)
        {
            EnsureLoaded();
            favorites.RemoveAll(q => q.Id == id);
            SaveFavorites();
        }

        public static void AddHistory(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;
            EnsureLoaded();

            sql = sql.Trim();
            history.RemoveAll(q => string.Equals(q.Sql, sql, StringComparison.Ordinal));
            history.Insert(0, Create(null, sql));

            while (history.Count > MaxHistory)
                history.RemoveAt(history.Count - 1);

            SaveHistory();
        }

        public static void UpdateHistory(SavedQuery item)
        {
            if (item == null) return;
            EnsureLoaded();
            item.Timestamp = DateTime.Now;
            SaveHistory();
        }

        public static void RemoveHistory(Guid id)
        {
            EnsureLoaded();
            history.RemoveAll(q => q.Id == id);
            SaveHistory();
        }

        public static string GetDisplayText(SavedQuery item, bool includeTime)
        {
            if (item == null) return string.Empty;

            var label = !string.IsNullOrWhiteSpace(item.Name) ? item.Name.Trim() : FirstLine(item.Sql);
            if (string.IsNullOrWhiteSpace(label)) label = "(empty)";
            label = CollapseWhitespace(label);
            if (label.Length > 64) label = label.Substring(0, 61) + "...";

            if (includeTime)
                label = AppForm.Settings.FormatDateTime(item.Timestamp) + "  " + label;

            return label;
        }

        static SavedQuery Create(string name, string sql)
        {
            return new SavedQuery
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                Sql = sql == null ? string.Empty : sql.Trim(),
                Timestamp = DateTime.Now
            };
        }

        static void EnsureLoaded()
        {
            if (favorites == null)
                favorites = LoadList(Settings.Default.QueryFavoritesJson);
            if (history == null)
                history = LoadList(Settings.Default.QueryHistoryJson);
        }

        static List<SavedQuery> LoadList(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SavedQuery>();

            try
            {
                var loaded = JsonConvert.DeserializeObject<List<SavedQuery>>(json);
                return loaded ?? new List<SavedQuery>();
            }
            catch
            {
                return new List<SavedQuery>();
            }
        }

        static void SaveFavorites()
        {
            Settings.Default.QueryFavoritesJson = JsonConvert.SerializeObject(favorites);
            Settings.Default.Save();
        }

        static void SaveHistory()
        {
            Settings.Default.QueryHistoryJson = JsonConvert.SerializeObject(history);
            Settings.Default.Save();
        }

        static string FirstLine(string sql)
        {
            if (string.IsNullOrEmpty(sql)) return string.Empty;
            var line = sql.Trim();
            var breakIndex = line.IndexOfAny(new[] { '\r', '\n' });
            if (breakIndex >= 0) line = line.Substring(0, breakIndex).Trim();
            return line;
        }

        static string CollapseWhitespace(string value)
        {
            return string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
