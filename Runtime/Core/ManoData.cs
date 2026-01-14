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
        /// Initialize GameData with the provided GameDataDocumentSO.
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
        }

        public static T GetCachedObject<T>(string rowId) where T : IManoDataRow, new()
        {
            string tableName = typeof(T).Name;
            string[] possibleIdKeys = { "ID", "id", "Id" };

            foreach (var doc in _documents)
            {
                var table = doc.GetTable(tableName);
                if (table != null)
                {
                    T result = doc.GetCachedObject<T>(tableName, rowId);
                    if (result != null) return result;

                    var rowData = table.data.FirstOrDefault(r =>
                    {
                        var actualKey = possibleIdKeys.FirstOrDefault(key => r.ContainsKey(key));

                        return actualKey != null && r[actualKey].ToString() == rowId;
                    });

                    if (rowData != null)
                    {
                        doc.PreWarmObject<T>(tableName, rowId, rowData);
                        return doc.GetCachedObject<T>(tableName, rowId);
                    }
                }
            }

            Debug.LogWarning($"[ManoData] Data not found for ID: {rowId} in any Document.");
            return default;
        }
    }
}