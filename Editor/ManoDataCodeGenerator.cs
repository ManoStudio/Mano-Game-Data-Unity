using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace ManoData
{
    public static class ManoDataCodeGenerator
    {
        public static void Generate(GameDataDocumentSO so)
        {
            if (so == null || so.document == null) return;

            string outputPath = string.IsNullOrEmpty(so.generatedCodePath) ? "Assets/" : so.generatedCodePath;
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            GenerateAsmdef(outputPath);

            foreach (var table in so.document.tables)
            {
                string className = SanitizeName(table.name);
                string code = BuildClassCode(className, table.schema);
                File.WriteAllText(Path.Combine(outputPath, className + ".cs"), code);
            }

            string registryCode = BuildRegistryCode(so.document.tables);
            File.WriteAllText(Path.Combine(outputPath, "ManoDataRegistry.cs"), registryCode);

            AssetDatabase.Refresh();
        }

        private static void GenerateAsmdef(string path)
        {
            string asmdefName = "ManoData.Generated";
            string filePath = Path.Combine(path, asmdefName + ".asmdef");

            if (File.Exists(filePath)) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"name\": \"{asmdefName}\",");
            sb.AppendLine("    \"rootNamespace\": \"ManoData.Generated\",");
            sb.AppendLine("    \"references\": [");
            sb.AppendLine("        \"GUID:f59a9ad2b6946f140880f089a803730c\",");
            sb.AppendLine("        \"ManoData.Runtime\"");
            sb.AppendLine("    ],");
            sb.AppendLine("    \"includePlatforms\": [],");
            sb.AppendLine("    \"excludePlatforms\": [],");
            sb.AppendLine("    \"allowUnsafeCode\": false,");
            sb.AppendLine("    \"overrideReferences\": false,");
            sb.AppendLine("    \"precompiledReferences\": [],");
            sb.AppendLine("    \"autoReferenced\": true,");
            sb.AppendLine("    \"defineConstraints\": [],");
            sb.AppendLine("    \"versionDefines\": [],");
            sb.AppendLine("    \"noEngineReferences\": false");
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"<color=green>[ManoData]</color> Generated {asmdefName}.asmdef at {path}");
        }

        private static string BuildRegistryCode(List<TableContent> tables)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("// Auto genarate by Mano Data Hub");
            sb.AppendLine("");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("");
            sb.AppendLine("namespace ManoData.Generated");
            sb.AppendLine("{");
            sb.AppendLine("     public static class ManoDataRegistry");
            sb.AppendLine("     {");
            sb.AppendLine("         [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]");
            sb.AppendLine("         static void Register()");
            sb.AppendLine("         {");
            sb.AppendLine("             GameData.OnPreWarm = PreWarmAll;");
            sb.AppendLine("         }");
            sb.AppendLine("");
            sb.AppendLine("         public static void PreWarmAll(GameDataDocumentSO so)");
            sb.AppendLine("         {");
            sb.AppendLine("             if (so == null || so.document == null) return;");
            sb.AppendLine("");

            foreach (var table in tables)
            {
                string className = SanitizeName(table.name);
                sb.AppendLine($"            // Pre-warm table: {table.name}");
                sb.AppendLine($"            var table_{className} = so.document.tables.FirstOrDefault(t => t.name == \"{table.name}\");");
                sb.AppendLine($"            if (table_{className} != null)");
                sb.AppendLine("            {");
                sb.AppendLine($"                foreach (var row in table_{className}.data)");
                sb.AppendLine("                {");
                sb.AppendLine("                     string id = row.Values.FirstOrDefault()?.ToString();");
                sb.AppendLine($"                    if (!string.IsNullOrEmpty(id)) so.PreWarmObject<{className}>(\"{table.name}\", id, row);");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("");
            }

            sb.AppendLine("         }");
            sb.AppendLine("     }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildClassCode(string className, List<ColumnSchema> schema)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// Auto genarate by Mano Data Hub");
            sb.AppendLine("");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("");
            sb.AppendLine("namespace ManoData.Generated");
            sb.AppendLine("{");
            sb.AppendLine("     [System.Serializable]");
            sb.AppendLine($"     public class {className} : IManoDataRow");
            sb.AppendLine("     {");

            foreach (var col in schema)
            {
                string type = MapType(col.type);
                sb.AppendLine($"        public {type} {SanitizeName(col.name)};");
            }

            sb.AppendLine("");
            sb.AppendLine("         public void SetData(Dictionary<string, object> rawData)");
            sb.AppendLine("         {");

            foreach (var col in schema)
            {
                string fieldName = SanitizeName(col.name);
                string type = col.type.ToLower();

                sb.AppendLine($"            if (rawData.ContainsKey(\"{col.name}\"))");
                sb.AppendLine("            {");
                if (type == "int")
                    sb.AppendLine($"                {fieldName} = System.Convert.ToInt32(rawData[\"{col.name}\"]);");
                else if (type == "float")
                    sb.AppendLine($"                {fieldName} = System.Convert.ToSingle(rawData[\"{col.name}\"]);");
                else if (type == "bool")
                    sb.AppendLine($"                {fieldName} = System.Convert.ToBoolean(rawData[\"{col.name}\"]);");
                else if (type == "vector2")
                    sb.AppendLine($"                {fieldName} = ManoData.ManoDataExtensions.ParseVector2(rawData[\"{col.name}\"].ToString());");
                else if (type == "vector3")
                    sb.AppendLine($"                {fieldName} = ManoData.ManoDataExtensions.ParseVector3(rawData[\"{col.name}\"].ToString());");
                else if (type == "color")
                    sb.AppendLine($"                {fieldName} = ManoData.ManoDataExtensions.ParseColor(rawData[\"{col.name}\"].ToString());");
                else
                    sb.AppendLine($"                {fieldName} = rawData[\"{col.name}\"].ToString();");
                sb.AppendLine("            }");
                sb.AppendLine("");
            }

            sb.AppendLine("         }");
            sb.AppendLine("     }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string MapType(string supabaseType)
        {
            switch (supabaseType.ToLower())
            {
                case "int": return "int";
                case "float": return "float";
                case "bool": return "bool";
                case "vector2": return "Vector2";
                case "vector3": return "Vector3";
                case "color": return "Color";
                default: return "string";
            }
        }

        private static string SanitizeName(string name) => name.Replace(" ", "").Replace("-", "_");
    }
}