using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Mano.Data.Editor
{
    public static class ManoDataCodeGenerator
    {
        public static void Generate(ManoDataDocumentSO so, List<string> filterSheetNames = null)
        {
            if (so == null) return;

            so.LoadDataFromJSON();

            if (so.document == null || so.document.tables == null || so.document.tables.Count == 0)
            {
                Debug.LogError("[ManoData] No table found to generate code. Please import data first.");
                return;
            }

            string outputPath = string.IsNullOrEmpty(so.generatedCodePath) ? "Assets/Scripts/GeneratedData/" : so.generatedCodePath;
            string ns = string.IsNullOrEmpty(so.NameSpaceDocument) ? "Mano.Data.Generated" : $"Mano.Data.{so.NameSpaceDocument}";

            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            GenerateAsmdef(outputPath, ns);

            List<TableContent> generatedTables = new List<TableContent>();

            foreach (var table in so.document.tables)
            {
                if (filterSheetNames != null && !filterSheetNames.Contains(table.name))
                {
                    continue;
                }

                string className = SanitizeName(table.name);
                string code = BuildClassCode(className, table.schema, ns);
                File.WriteAllText(Path.Combine(outputPath, className + ".cs"), code);

                generatedTables.Add(table);

                Debug.Log($"[ManoData] Generated: {className}.cs");
            }

            string registryCode = BuildRegistryCode(generatedTables, ns);
            File.WriteAllText(Path.Combine(outputPath, "ManoDataRegistry.cs"), registryCode);

            AssetDatabase.Refresh();
            Debug.Log("[ManoData] Selective Generate Completed.");
        }

        private static void GenerateAsmdef(string path, string asmdefName = "ManoData.Generated")
        {
            string filePath = Path.Combine(path, asmdefName + ".asmdef");

            if (File.Exists(filePath)) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"name\": \"{asmdefName}\",");
            sb.AppendLine("    \"references\": [\"ManoData\"],");
            sb.AppendLine("    \"autoReferenced\": true");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString());
        }

        private static string BuildClassCode(string className, List<ColumnSchema> schemas, string nameSpaceData)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("");
            sb.AppendLine($"namespace {nameSpaceData}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public class {className} : IManoDataRow");
            sb.AppendLine("    {");

            foreach (var col in schemas)
            {
                string type = MapType(col.type);
                string fieldName = SanitizeName(col.name);
                sb.AppendLine($"        public {type} {fieldName} {{ get; private set;}}");
            }

            sb.AppendLine("");
            sb.AppendLine("        public void SetData(Dictionary<string, object> rawData)");
            sb.AppendLine("        {");

            foreach (var col in schemas)
            {
                string type = col.type.ToLower();
                string fieldName = SanitizeName(col.name);

                sb.AppendLine($"            if (rawData.ContainsKey(\"{col.name}\"))");
                sb.AppendLine("            {");

                if (type == "int")
                    sb.AppendLine($"                {fieldName} = int.Parse(rawData[\"{col.name}\"].ToString());");
                else if (type == "float")
                    sb.AppendLine($"                {fieldName} = float.Parse(rawData[\"{col.name}\"].ToString());");
                else if (type == "bool")
                    sb.AppendLine($"                {fieldName} = rawData[\"{col.name}\"].ToString().ToLower() == \"true\";");
                else if (type == "vector2")
                    sb.AppendLine($"                {fieldName} = ManoData.ManoDataExtensions.ParseVector2(rawData[\"{col.name}\"].ToString());");
                else if (type == "vector3")
                    sb.AppendLine($"                {fieldName} = ManoData.ManoDataExtensions.ParseVector3(rawData[\"{col.name}\"].ToString());");
                else if (type == "color")
                    sb.AppendLine($"                {fieldName} = ManoData.ManoDataExtensions.ParseColor(rawData[\"{col.name}\"].ToString());");
                else if (type.StartsWith("list_"))
                {
                    string innerType = type.Replace("list_", "");
                    sb.AppendLine($"                var rawStr = rawData[\"{col.name}\"].ToString();");
                    sb.AppendLine($"                var items = rawStr.Split('|');");
                    sb.AppendLine($"                {fieldName} = new {MapType(type)}();");
                    sb.AppendLine("                foreach(var s in items) {");

                    if (innerType == "int")
                        sb.AppendLine($"                    if(int.TryParse(s, out int v)) {fieldName}.Add(v);");
                    else if (innerType == "float")
                        sb.AppendLine($"                    if(float.TryParse(s, out float v)) {fieldName}.Add(v);");
                    else if (innerType == "bool")
                        sb.AppendLine($"                    {fieldName}.Add(s.ToLower() == \"true\");");
                    else
                        sb.AppendLine($"                    {fieldName}.Add(s);");

                    sb.AppendLine("                }");
                }
                else
                    sb.AppendLine($"                {fieldName} = rawData[\"{col.name}\"].ToString();");

                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string BuildRegistryCode(List<TableContent> tables, string nameSpaceData = "ManoData.Generated")
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("");
            sb.AppendLine($"namespace {nameSpaceData}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class ManoDataRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]");
            sb.AppendLine("        public static void Register()");
            sb.AppendLine("        {");
            sb.AppendLine("            ManoData.OnPreWarm += (so) =>");
            sb.AppendLine("            {");

            foreach (var table in tables)
            {
                string className = SanitizeName(table.name);
                sb.AppendLine($"                PreWarmTable<{className}>(so, \"{table.name}\");");
            }

            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("");
            sb.AppendLine("        private static void PreWarmTable<T>(ManoDataDocumentSO so, string tableName) where T : IManoDataRow, new()");
            sb.AppendLine("        {");
            sb.AppendLine("            var table = so.GetTable(tableName);");
            sb.AppendLine("            if (table == null) return;");
            sb.AppendLine("            foreach (var row in table.data)");
            sb.AppendLine("            {");
            sb.AppendLine("                var id = row.Values.FirstOrDefault()?.ToString();");
            sb.AppendLine("                if (!string.IsNullOrEmpty(id)) so.PreWarmObject<T>(tableName, id, row);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string MapType(string type)
        {
            switch (type.ToLower())
            {
                case "int": return "int";
                case "float": return "float";
                case "bool": return "bool";
                case "list_string": return "List<string>";
                case "list_int": return "List<int>";
                case "list_float": return "List<float>";
                case "list_bool": return "List<bool>";
                case "vector2": return "Vector2";
                case "vector3": return "Vector3";
                case "color": return "Color";
                default: return "string";
            }
        }

        private static string SanitizeName(string name)
        {
            return name.Replace(" ", "")
                        .Replace("-", "")
                        .Replace("'", "")
                        .Replace("\"", "")
                        .Replace(".", "")
                        .Replace(",", "")
                        .Replace("_", "");
        }
    }
}