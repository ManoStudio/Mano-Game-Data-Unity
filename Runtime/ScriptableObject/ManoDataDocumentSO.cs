using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Scripting;
using Newtonsoft.Json;

namespace Mano.Data
{
    [CreateAssetMenu(fileName = "GameDataDocument", menuName = "ManoData/Document")]
    public class ManoDataDocumentSO : ScriptableObject
    {
        [Header("Document Data")]
        public string NameSpaceDocument = "Mano.Data.Generated";
        public string generatedCodePath = "Assets/Scripts/GeneratedData/";

        [Header("Sync Status")]
        public string lastEdit;
        [TextArea(5, 20)] public string rawJson;

        public List<SelectedSheet> availableSheets = new List<SelectedSheet>();
        public List<SelectedSheet> generatedSheets = new List<SelectedSheet>();

        [SerializeField]
        public ManoDataDocument document = new ManoDataDocument();

        public bool HasDataToPreview => document?.tables != null && document.tables.Count > 0;

        private Dictionary<string, TableContent> _tableCache = new Dictionary<string, TableContent>();

        // _objectCache จะไม่ถูก Serialize เพราะเก็บ instance ของ Class ที่เรา gen
        private Dictionary<string, Dictionary<string, object>> _objectCache = new Dictionary<string, Dictionary<string, object>>();

        // --- ISerializationCallbackReceiver ---
        public void OnBeforeSerialize() { } // ไม่ต้องทำอะไร เพราะเราเก็บเป็น List<RowData> ใน document อยู่แล้ว

        public void OnAfterDeserialize()
        {
            // เมื่อ Unity โหลดไฟล์ SO นี้ขึ้นมา ให้สร้าง Index ใหม่ทันที
            BuildIndex();
        }

        public void LoadDataFromJSON()
        {
            if (string.IsNullOrEmpty(rawJson)) return;
            try
            {
                JObject googleData = JObject.Parse(rawJson);

                if (document == null) document = new ManoDataDocument();
                document.tables.Clear();
                document.groups.Clear();

                if (googleData.ContainsKey("valueRanges"))
                {
                    var ranges = googleData["valueRanges"] as JArray;
                    foreach (var range in ranges)
                    {
                        string sheetName = range["range"]?.ToString().Split('!')[0].Replace("'", "");
                        ParseGoogleSheet(sheetName, range["values"] as JArray);
                    }
                }
                else if (googleData.ContainsKey("values"))
                {
                    ParseGoogleSheet("ImportedSheet", googleData["values"] as JArray);
                }

                document.lastEdit = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                lastEdit = document.lastEdit;
                if (!document.groups.Contains("Default")) document.groups.Add("Default");

                BuildIndex();
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
                Debug.Log("<color=green>[ManoData]</color> Tables Loaded: " + document.tables.Count);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManoData] Parse Error: {e.Message}");
            }
        }

        private void ParseGoogleSheet(string sheetName, JArray values)
        {
            if (values == null || values.Count < 2) return;

            TableContent table = new TableContent { name = sheetName, group = "Default" };
            var names = values[0];
            var types = values[1];

            for (int col = 0; col < names.Count(); col++)
            {
                table.schema.Add(new ColumnSchema
                {
                    name = names[col].ToString(),
                    type = types.Count() > col ? types[col].ToString() : "string"
                });
            }

            string lastId = "";
            Dictionary<string, object> currentRow = null;
            string[] lastValues = new string[names.Count()];

            for (int row = 2; row < values.Count(); row++)
            {
                var rowDataJson = values[row] as JArray;
                if (rowDataJson == null || rowDataJson.Count == 0) continue;

                string currentId = rowDataJson[0]?.ToString();

                if (!string.IsNullOrEmpty(currentId) && currentId != lastId)
                {
                    currentRow = new Dictionary<string, object>();
                    for (int col = 0; col < table.schema.Count; col++)
                    {
                        string val = col < rowDataJson.Count ? rowDataJson[col].ToString() : "";
                        currentRow[table.schema[col].name] = val;
                        lastValues[col] = val;
                    }

                    // แปลง Dictionary เป็น RowData เพื่อให้ Unity เซฟได้
                    table.rows.Add(new RowData
                    {
                        id = currentId,
                        jsonValues = JsonConvert.SerializeObject(currentRow)
                    });
                    lastId = currentId;
                }
                else if (currentRow != null)
                {
                    // กรณี Row Merge (ID ซ้ำ)
                    for (int col = 0; col < table.schema.Count; col++)
                    {
                        string val = col < rowDataJson.Count ? rowDataJson[col].ToString() : "";
                        if (string.IsNullOrEmpty(val)) val = lastValues[col];
                        else lastValues[col] = val;

                        string colName = table.schema[col].name;
                        currentRow[colName] = currentRow[colName].ToString() + "|" + val;
                    }
                    // อัปเดต JSON string ของ Row ล่าสุด
                    table.rows.Last().jsonValues = JsonConvert.SerializeObject(currentRow);
                }
            }
            document.tables.Add(table);
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

        public T GetCachedObject<T>(string tableName, string rowId) where T : IManoDataRow, new()
        {
            // 1. ลองหาใน Memory Cache ก่อน (เร็วสุด)
            if (_objectCache.TryGetValue(tableName, out var table) && table.TryGetValue(rowId, out var obj))
                return (T)obj;

            // 2. ถ้าไม่มีใน Cache แต่มีใน document (ที่เพิ่งโหลดมาจากไฟล์)
            var tableContent = GetTable(tableName);
            if (tableContent != null)
            {
                var row = tableContent.rows.FirstOrDefault(r => r.id == rowId);
                if (row != null)
                {
                    var dict = row.ToDictionary();
                    PreWarmObject<T>(tableName, rowId, dict);
                    return (T)_objectCache[tableName][rowId];
                }
            }
            return default;
        }

        public TableContent GetTable(string tableName)
        {
            if (_tableCache.Count == 0) BuildIndex();
            _tableCache.TryGetValue(tableName, out TableContent table);
            return table;
        }

        public void RestoreFromRawJson()
        {
            if (!string.IsNullOrEmpty(rawJson))
            {
                LoadDataFromJSON();
                Debug.Log("<color=cyan>[ManoData]</color> Preview data restored from rawJson.");
            }
        }

#if UNITY_EDITOR
        public void EditorWarmup()
        {
            // เปลี่ยนจากเช็ค rawJson เป็นเช็ค document.tables เพราะตอนนี้เราเซฟลง Asset แล้ว
            if (document == null || document.tables.Count == 0)
            {
                // ถ้าใน document ว่าง แต่มี rawJson ให้ลองโหลดใหม่ก่อน
                if (!string.IsNullOrEmpty(rawJson)) LoadDataFromJSON();
                else return;
            }

            _objectCache.Clear();

            foreach (var table in document.tables)
            {
                string sanitizedName = SanitizeTableName(table.name);
                string typeName = $"{NameSpaceDocument}.{sanitizedName}";

                Type rowType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == typeName || t.Name == sanitizedName);

                if (rowType != null && typeof(IManoDataRow).IsAssignableFrom(rowType))
                {
                    var method = GetType().GetMethod("PreWarmObject")?.MakeGenericMethod(rowType);

                    foreach (var row in table.rows)
                    {
                        if (string.IsNullOrEmpty(row.id)) continue;

                        var dictData = row.ToDictionary();

                        if (dictData != null)
                        {
                            method?.Invoke(this, new object[] { table.name, row.id, dictData });
                        }
                    }
                }
            }
            Debug.Log($"<color=cyan>[ManoData]</color> {name} : Editor Warmup Complete (Serialized Mode).");
        }
#endif

        private string SanitizeTableName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Replace("_", "").Replace(" ", "").Replace("-", "");
        }
    }

    [Serializable]
    public class SelectedSheet
    {
        public string SheetName;
        public bool IsSelected = true;
        public bool IsGenCode = true;
    }
}