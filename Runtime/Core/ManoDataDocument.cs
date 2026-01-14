using System;
using System.Collections.Generic;

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
        public List<Dictionary<string, object>> data = new List<Dictionary<string, object>>();
        public List<ColumnSchema> schema = new List<ColumnSchema>();
    }

    [Serializable]
    public class ColumnSchema
    {
        public string name;
        public string type; // string, int, float, vector2, vector3, color, bool
    }
}