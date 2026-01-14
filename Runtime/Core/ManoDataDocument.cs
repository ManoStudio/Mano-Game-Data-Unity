using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mano.Data
{
    [Serializable]
    public class ManoDataDocument
    {
        public List<TableContent> tables = new List<TableContent>();
        public List<string> groups = new List<string>();
        public string lastEdit;
    }

    [Serializable]
    public class TableContent
    {
        public string name;
        public string group;

        public List<RowData> rows = new List<RowData>();
        public List<ColumnSchema> schema = new List<ColumnSchema>();

        public List<Dictionary<string, object>> GetRuntimeData()
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var row in rows)
            {
                result.Add(row.ToDictionary());
            }
            return result;
        }
    }

    [Serializable]
    public class RowData
    {
        public string id;

        [TextArea(1, 10)]
        public string jsonValues;

        public Dictionary<string, object> ToDictionary()
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonValues);
        }
    }

    [Serializable]
    public class ColumnSchema
    {
        public string name;
        public string type; // string, int, float, vector2, vector3, color, bool, fp, qstring
    }
}