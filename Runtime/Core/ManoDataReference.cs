using System;
using System.Collections.Generic;

namespace ManoData
{
    public interface IManoDataRow
    {
        void SetData(Dictionary<string, object> rawData);
    }

    [Serializable]
    public class ManoDataReference
    {
        public GameDataDocumentSO gameDataDocumentSO;
        public string groupName;
        public string tableName;
        public string rowId;

        public T GetData<T>() where T : IManoDataRow, new()
        {
            ValidateInit();

            if (string.IsNullOrEmpty(rowId)) return default;
            return GameData.GetCachedObject<T>(rowId);
        }

        private void ValidateInit()
        {
            if (gameDataDocumentSO != null)
            {
                GameData.Init(gameDataDocumentSO);
            }
        }

        public bool IsValid => !string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(rowId);
    }
}