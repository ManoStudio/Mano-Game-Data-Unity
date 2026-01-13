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

            return GameData.GetCachedObject<T>(tableName, rowId);
        }

        private void ValidateInit()
        {
            if (gameDataDocumentSO != null)
            {
                GameData.Init(gameDataDocumentSO);
            }
        }
    }
}