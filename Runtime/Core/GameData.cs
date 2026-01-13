using System.Collections.Generic;
using System.Linq;

namespace ManoData
{
    public static class GameData
    {
        private static GameDataDocumentSO _instance;
        public static System.Action<GameDataDocumentSO> OnPreWarm;

        /// <summary>
        /// Initialize GameData with the provided GameDataDocumentSO.
        /// </summary>
        /// <param name="so"></param>
        public static void Init(GameDataDocumentSO so)
        {
            if (_instance == so && _instance.document != null) return;
            _instance = so;
            _instance.LoadDataFromJSON();

            OnPreWarm?.Invoke(_instance);
        }

        public static T GetCachedObject<T>(string tableName, string rowId)
        {
            return _instance != null ? _instance.GetCachedObject<T>(tableName, rowId) : default;
        }
    }
}