using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ManoData
{
    [CustomEditor(typeof(GameDataDocumentSO))]
    public class GameDataDocumentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GameDataDocumentSO so = (GameDataDocumentSO)target;

            GUILayout.Space(10);
            GUILayout.Label("Code Generation Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            so.generatedCodePath = EditorGUILayout.TextField("Output Path", so.generatedCodePath);

            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string absolutePath = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(absolutePath))
                {
                    if (absolutePath.Contains(Application.dataPath))
                    {
                        so.generatedCodePath = "Assets" + absolutePath.Substring(Application.dataPath.Length) + "/";
                    }
                    else
                    {
                        Debug.LogWarning("Please select a folder inside the project Assets directory.");
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUI.backgroundColor = new Color(0.1f, 0.7f, 0.2f);
            if (GUILayout.Button("SYNC MANO DATA", GUILayout.Height(45)))
            {
                SyncFromSupabase(so);
            }
            GUI.backgroundColor = Color.white;
        }

        private void SyncFromSupabase(GameDataDocumentSO so)
        {
            EditorUtility.DisplayProgressBar("Mano Data Hub", "Fetching data from Supabase...", 0.3f);

            string url = $"{so.supabaseUrl}/rest/v1/projects?id=eq.{so.projectId}&select=data";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("apikey", so.anonKey);
                request.SetRequestHeader("Authorization", $"Bearer {so.anonKey}");

                var operation = request.SendWebRequest();
                while (!operation.isDone) { }

                EditorUtility.ClearProgressBar();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;

                    so.rawJson = json;
                    so.lastSyncTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    so.LoadDataFromJSON();

                    EditorUtility.SetDirty(so);
                    AssetDatabase.SaveAssets();

                    ManoDataCodeGenerator.Generate(so);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    EditorUtility.DisplayDialog("Mano Data Hub", "Sync & Code Generated! \n\nNote: Data will be Pre-warmed on next Play or by clicking 'Pre-warm' button.", "OK");
                }
                else
                {
                    Debug.LogError($"ManoData Sync Failed: {request.error}");
                    EditorUtility.DisplayDialog("Sync Error", request.error, "Close");
                }
            }

            AssetDatabase.Refresh();
        }

        private class SupabaseResponse { public GameDataDocument data; }
    }
}