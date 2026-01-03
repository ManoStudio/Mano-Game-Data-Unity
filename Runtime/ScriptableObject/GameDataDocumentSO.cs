using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ManoData
{
    [CreateAssetMenu(fileName = "GameDataDocument", menuName = "ManoData/Document")]
    public class GameDataDocumentSO : ScriptableObject
    {
        [Header("Supabase Configuration")]
        public string supabaseUrl = "https://your-project.supabase.co";
        [TextArea(1, 2)] public string anonKey = "your-key";
        public string projectId = "your-project-id";

        [Header("Sync Status")]
        [HideInInspector] public string lastSyncTime = "Never";
        [HideInInspector] public string generatedCodePath = "Assets/Scripts/GeneratedData/";
        [HideInInspector] public string rawJson;

        [System.NonSerialized]
        public GameDataDocument document;

        private Dictionary<string, TableContent> _tableCache = new Dictionary<string, TableContent>();

        private Dictionary<string, Dictionary<string, object>> _objectCache = new Dictionary<string, Dictionary<string, object>>();

        public void LoadDataFromJSON()
        {
            if (string.IsNullOrEmpty(rawJson)) return;
            try
            {
                var wrappedData = JsonConvert.DeserializeObject<List<SupabaseResponse>>(rawJson);
                if (wrappedData != null && wrappedData.Count > 0)
                {
                    document = wrappedData[0].data;
                    BuildIndex();
                    _objectCache.Clear();
                }
            }
            catch (System.Exception e) { Debug.LogError($"ManoData: {e.Message}"); }
        }

        private void BuildIndex()
        {
            _tableCache.Clear();
            if (document?.tables == null) return;
            foreach (var table in document.tables)
            {
                _tableCache[table.name] = table;
            }
        }

        public void PreWarmObject<T>(string tableName, string rowId, Dictionary<string, object> rawData) where T : IManoDataRow, new()
        {
            if (!_objectCache.ContainsKey(tableName))
                _objectCache[tableName] = new Dictionary<string, object>();

            T instance = new T();
            instance.SetData(rawData);
            _objectCache[tableName][rowId] = instance;
        }

        public T GetCachedObject<T>(string tableName, string rowId)
        {
            if (_objectCache.TryGetValue(tableName, out var table) && table.TryGetValue(rowId, out var obj))
            {
                return (T)obj;
            }
            return default;
        }

        public TableContent GetTable(string tableName)
        {
            if (_tableCache.Count == 0) BuildIndex();
            _tableCache.TryGetValue(tableName, out TableContent table);
            return table;
        }

        [System.Serializable]
        public class SupabaseResponse { public GameDataDocument data; }
    }
}