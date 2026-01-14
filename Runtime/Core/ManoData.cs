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
            string targetClassName = typeof(T).Name;

            foreach (var doc in _documents)
            {
                var table = doc.document.tables.FirstOrDefault(t => SanitizeTableName(t.name) == targetClassName);

                if (table != null)
                {
                    string originalTableName = table.name;

                    T result = doc.GetCachedObject<T>(originalTableName, rowId);
                    if (result != null) return result;

                    var rowEntry = table.rows.FirstOrDefault(r => r.id == rowId);

                    if (rowEntry != null)
                    {
                        var dictData = rowEntry.ToDictionary();

                        doc.PreWarmObject<T>(originalTableName, rowId, dictData);

                        return doc.GetCachedObject<T>(originalTableName, rowId);
                    }
                }
            }

            Debug.LogWarning($"<color=yellow>[ManoData]</color> Data not found for ID: {rowId} in any Document (Target Class: {targetClassName})");
            return default;
        }

        private static string SanitizeTableName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Replace("_", "").Replace(" ", "").Replace("-", "");
        }

#if UNITY_EDITOR
        public static void WarmupAllDocs()
        {
            var docs = UnityEditor.AssetDatabase.FindAssets("t:ManoDataDocumentSO")
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<ManoDataDocumentSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)));

            foreach (var doc in docs)
            {
                doc.EditorWarmup();
            }
        }
#endif
    }
}