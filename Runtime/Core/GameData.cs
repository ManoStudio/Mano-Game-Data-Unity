using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ManoData
{
    public static class GameData
    {
        private static List<GameDataDocumentSO> _documents = new List<GameDataDocumentSO>();
        public static System.Action<GameDataDocumentSO> OnPreWarm;

        /// <summary>
        /// Initialize GameData with the provided GameDataDocumentSO.
        /// </summary>
        /// <param name="docs"></param>
        public static void Init(params GameDataDocumentSO[] docs)
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

            foreach (var doc in _documents)
            {
                if (doc.GetTable(tableName) != null)
                {
                    T result = doc.GetCachedObject<T>(tableName, rowId);
                    if (result != null) return result;

                    var table = doc.GetTable(tableName);
                    var rowData = table.data.FirstOrDefault(r => r["ID"].ToString() == rowId);

                    if (rowData != null)
                    {
                        doc.PreWarmObject<T>(tableName, rowId, rowData);
                        return doc.GetCachedObject<T>(tableName, rowId);
                    }
                }
            }

            Debug.LogWarning($"[GameData] Data not found for ID: {rowId} in any Document.");
            return default;
        }
    }
}