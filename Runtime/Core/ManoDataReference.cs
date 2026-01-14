using System;
using System.Collections.Generic;

namespace Mano.Data
{
    public interface IManoDataRow
    {
        void SetData(Dictionary<string, object> rawData);
    }

    [Serializable]
    public class ManoDataReference
    {
        public ManoDataDocumentSO gameDataDocumentSO;
        public string groupName;
        public string tableName;
        public string rowId;

        public T GetData<T>() where T : IManoDataRow, new()
        {
            ValidateInit();

            if (string.IsNullOrEmpty(rowId)) return default;
            return ManoData.GetCachedObject<T>(rowId);
        }

        private void ValidateInit()
        {
            if (gameDataDocumentSO != null)
            {
                ManoData.Init(gameDataDocumentSO);
            }
        }

        public bool IsValid => !string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(rowId);
    }
}