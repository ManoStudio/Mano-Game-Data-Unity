using UnityEditor;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public static class ManoDataAsmdefUtility
{
    public static void FixDependencyForPath(string generatedPath)
    {
        string absolutePath = Path.GetFullPath(generatedPath);
        string asmdefPath = FindNearestAsmdef(absolutePath);

        if (!string.IsNullOrEmpty(asmdefPath))
        {
            AddPackageReference(asmdefPath);
        }
        else
        {
            Debug.Log("[ManoData] No asmdef found in parent hierarchy. Using Assembly-CSharp.");
        }
    }

    private static string FindNearestAsmdef(string currentPath)
    {
        string projectRoot = Path.GetFullPath(Application.dataPath);

        DirectoryInfo dir = new DirectoryInfo(currentPath);

        while (dir != null && dir.FullName.StartsWith(projectRoot))
        {
            var files = dir.GetFiles("*.asmdef");
            if (files.Length > 0)
            {
                return "Assets" + files[0].FullName.Substring(projectRoot.Length).Replace("\\", "/");
            }
            dir = dir.Parent;
        }

        return null;
    }

    private static void AddPackageReference(string asmdefPath)
    {
        string packageRuntimeAsm = "ManoData.Runtime";
        string json = File.ReadAllText(asmdefPath);
        var data = JsonUtility.FromJson<AsmdefData>(json);

        if (data.references == null) data.references = new List<string>();

        if (!data.references.Contains(packageRuntimeAsm))
        {
            data.references.Add(packageRuntimeAsm);
            string updatedJson = JsonUtility.ToJson(data, true);
            File.WriteAllText(asmdefPath, updatedJson);
            AssetDatabase.ImportAsset(asmdefPath);
            Debug.Log($"[ManoData] Automatically added {packageRuntimeAsm} to {asmdefPath}");
        }
    }

    [System.Serializable]
    private class AsmdefData
    {
        public string name;
        public List<string> references;
    }
}