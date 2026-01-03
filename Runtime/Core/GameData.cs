using System.Collections.Generic;
using System.Linq;

namespace ManoData
{
    public static class GameData
    {
        private static GameDataDocumentSO _instance;
        public static System.Action<GameDataDocumentSO> OnPreWarm;

        public static void Init(GameDataDocumentSO so)
        {
            if (_instance == so && _instance.document != null) return;
            _instance = so;
            _instance.LoadDataFromJSON();

            // จังหวะหัวใจ: สร้าง Object ทั้งหมดรอไว้ในแรมทันที
            OnPreWarm?.Invoke(_instance);
        }

        public static T GetCachedObject<T>(string tableName, string rowId)
        {
            return _instance != null ? _instance.GetCachedObject<T>(tableName, rowId) : default;
        }

        public static TableAccessor Tables(string tableName)
        {
            var table = _instance?.GetTable(tableName);
            return table != null ? new TableAccessor(table) : null;
        }
    }

    public class TableAccessor
    {
        private TableContent _table;
        private Dictionary<string, Dictionary<string, object>> _rowCache;

        public TableAccessor(TableContent table)
        {
            _table = table;
            BuildRowIndex();
        }

        private void BuildRowIndex()
        {
            _rowCache = new Dictionary<string, Dictionary<string, object>>();
            if (_table?.data == null) return;

            foreach (var row in _table.data)
            {
                var idValue = row.Values.FirstOrDefault()?.ToString();
                if (!string.IsNullOrEmpty(idValue))
                {
                    _rowCache[idValue] = row;
                }
            }
        }

        public Dictionary<string, object> Get(string rowId)
        {
            if (_rowCache == null) BuildRowIndex();

            _rowCache.TryGetValue(rowId, out var row);
            return row;
        }
    }
}