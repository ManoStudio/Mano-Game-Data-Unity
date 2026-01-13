using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // เพิ่มตัวนี้เข้ามา
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Scripting;

namespace ManoData
{
    [CreateAssetMenu(fileName = "GameDataDocument", menuName = "ManoData/Document")]
    public class GameDataDocumentSO : ScriptableObject
    {
        [Header("Document Data")]
        [RequiredMember] public string NameSpaceDocument = default!;
        public string generatedCodePath = "Assets/Scripts/GeneratedData/";

        [Header("Sync Status")]
        [ManoOnly] public string lastEdit;
        [ManoOnly][TextArea(5, 20)] public string rawJson;

        [ManoOnly] public List<SelectedSheet> availableSheets = new List<SelectedSheet>();
        [ManoOnly] public List<SelectedSheet> generatedSheets = new List<SelectedSheet>();

        [System.NonSerialized]
        public GameDataDocument document = new GameDataDocument();

        private Dictionary<string, TableContent> _tableCache = new Dictionary<string, TableContent>();
        private Dictionary<string, Dictionary<string, object>> _objectCache = new Dictionary<string, Dictionary<string, object>>();

        public void LoadDataFromJSON()
        {
            if (string.IsNullOrEmpty(rawJson)) return;
            try
            {
                JObject googleData = JObject.Parse(rawJson);

                if (document == null) document = new GameDataDocument();
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

            string lastId = "";
            Dictionary<string, object> currentRow = null;
            string[] lastValues = new string[names.Count()];

            for (int col = 0; col < names.Count(); col++)
            {
                table.schema.Add(new ColumnSchema { name = names[col].ToString(), type = types.Count() > col ? types[col].ToString() : "string" });
            }

            for (int row = 2; row < values.Count(); row++)
            {
                var rowData = values[row] as JArray;
                if (rowData == null || rowData.Count == 0) continue;

                string currentId = rowData[0]?.ToString();

                // ถ้าเจอ ID ใหม่ หรือ ID เดิมแต่ต้องการสร้าง Row ใหม่ (กรณีไม่ Merge)
                if (!string.IsNullOrEmpty(currentId) && currentId != lastId)
                {
                    currentRow = new Dictionary<string, object>();
                    for (int col = 0; col < table.schema.Count; col++)
                    {
                        string val = col < rowData.Count ? rowData[col].ToString() : "";
                        // เก็บเป็น String ธรรมดาไว้ก่อน (ถ้ามีหลายบรรทัดจะใช้ | ขั้น)
                        currentRow[table.schema[col].name] = val;
                        lastValues[col] = val;
                    }
                    table.data.Add(currentRow);
                    lastId = currentId;
                }
                else if (currentRow != null)
                {
                    // กรณี ID ซ้ำหรือ Merge Cell: ให้เอาข้อมูลคอลัมน์อื่นมาต่อท้ายด้วยเครื่องหมาย |
                    for (int col = 0; col < table.schema.Count; col++)
                    {
                        string val = col < rowData.Count ? rowData[col].ToString() : "";
                        if (string.IsNullOrEmpty(val)) val = lastValues[col];
                        else lastValues[col] = val;

                        string colName = table.schema[col].name;
                        // ต่อ String เข้าไปด้วย Pipe | เพื่อให้ตอน Gen Code เอาไป Split ได้
                        currentRow[colName] = currentRow[colName].ToString() + "|" + val;
                    }
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

        public T GetCachedObject<T>(string tableName, string rowId)
        {
            if (_objectCache.TryGetValue(tableName, out var table) && table.TryGetValue(rowId, out var obj))
                return (T)obj;
            return default;
        }

        public TableContent GetTable(string tableName)
        {
            if (_tableCache.Count == 0) BuildIndex();
            _tableCache.TryGetValue(tableName, out TableContent table);
            return table;
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