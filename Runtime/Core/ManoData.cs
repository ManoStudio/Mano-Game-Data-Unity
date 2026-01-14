using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mano.Data
{
    public static class ManoData
    {
        private static List<ManoDataDocumentSO> _documents = new List<ManoDataDocumentSO>();
        public static System.Action<ManoDataDocumentSO> OnPreWarm;

        /// <summary>
        /// Initialize ManoData with the provided ManoDataDocumentSO.
        /// </summary>
        /// <param name="docs"></param>
        public static void Init(params ManoDataDocumentSO[] docs)
        {
            _documents.Clear();
            _documents.AddRange(docs);
            
            foreach (var doc in _documents)
            {
                doc.LoadDataFromJSON();
                OnPreWarm?.Invoke(doc);
            }

            Debug.Log($"[ManoData] Initialized with {_documents.Count} Document(s).");
        }

        public static T GetCachedObject<T>(string rowId) where T : IManoDataRow, new()
        {
            string targetName = typeof(T).Name;

            string[] possibleIdKeys = { "ID", "id", "Id" };

            foreach (var doc in _documents)
            {
                var table = doc.document.tables.FirstOrDefault(t => SanitizeTableName(t.name) == targetName);

                if (table != null)
                {
                    string originalTableName = table.name;

                    T result = doc.GetCachedObject<T>(originalTableName, rowId);
                    if (result != null) return result;

                    var rowData = table.data.FirstOrDefault(r =>
                    {
                        var actualKey = possibleIdKeys.FirstOrDefault(key => r.ContainsKey(key));
                        return actualKey != null && r[actualKey].ToString() == rowId;
                    });

                    if (rowData != null)
                    {
                        doc.PreWarmObject<T>(originalTableName, rowId, rowData);
                        return doc.GetCachedObject<T>(originalTableName, rowId);
                    }
                }
            }

            Debug.LogWarning($"[ManoData] Data not found for ID: {rowId} in any Document (Target Class: {targetName})");
            return default;
        }

        private static string SanitizeTableName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Replace("_", "").Replace(" ", "").Replace("-", "");
        }
    }
}