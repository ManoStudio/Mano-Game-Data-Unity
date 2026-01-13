using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ManoData
{
    public class ManoDataHubTool : EditorWindow
    {
        private const float TOOL_VERSION = 0.1f;
        public GoogleSettingSO googleSetting;
        public GameDataDocumentSO targetSO;

        private string authCode = "";
        private bool isWorking = false;
        private string newTableName = "InventoryTable";

        private Dictionary<string, bool> selectedSheets = new Dictionary<string, bool>();
        private bool showSheetSelector = false;
        private Vector2 scrollPos;

        [MenuItem("Mano Tools/Mano Data Hub")]
        public static void ShowWindow() => GetWindow<ManoDataHubTool>("Mano Dashboard");

        void OnGUI()
        {
            if (googleSetting == null)
            {
                EditorGUILayout.HelpBox("Please assign GoogleSettingSO to start using.", MessageType.Warning);
                googleSetting = (GoogleSettingSO)EditorGUILayout.ObjectField("Setting SO", googleSetting, typeof(GoogleSettingSO), false);
                return;
            }

            if (isWorking)
            {
                EditorGUILayout.HelpBox("Connecting to Google Cloud... Please wait", MessageType.Info);
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawMainInterface();

            EditorGUILayout.EndScrollView();
        }

        void DrawMainInterface()
        {
            GUILayout.Label("🔐 MANO DATA HUB (PRO)", EditorStyles.boldLabel);
            googleSetting = (GoogleSettingSO)EditorGUILayout.ObjectField("Setting SO", googleSetting, typeof(GoogleSettingSO), false);
            targetSO = (GameDataDocumentSO)EditorGUILayout.ObjectField("Target Data SO", targetSO, typeof(GameDataDocumentSO), false);

            EditorGUILayout.Space(10);

            if (targetSO == null)
            {
                EditorGUILayout.HelpBox("Please assign 'Target Data SO' (GameDataDocumentSO) to reveal tool controls.", MessageType.Info);
                return;
            }

            if (string.IsNullOrEmpty(googleSetting.RefreshToken))
            {
                DrawLoginView();
            }
            else
            {
                DrawDashboardView();
            }
        }

        void DrawLoginView()
        {
            EditorGUILayout.HelpBox("Google Account not connected", MessageType.Warning);
            if (GUILayout.Button("1. Login & Get Auth Code", GUILayout.Height(30)))
                Application.OpenURL(GetAuthUrl());

            authCode = EditorGUILayout.TextField("2. Paste Auth Code", authCode);

            if (GUILayout.Button("3. Complete Connection", GUILayout.Height(30)))
                if (!string.IsNullOrEmpty(authCode)) _ = ExchangeCodeAsync();
        }

        void DrawDashboardView()
        {
            // --- SECTION: INITIALIZE PROJECT ---
            GUILayout.Label("🚀 PROJECT SETUP", EditorStyles.boldLabel);
            if (GUILayout.Button("INITIALIZE PROJECT (Welcome & Config)", GUILayout.Height(30)))
                RunAsyncAction(InitializeProjectOAuth);

            EditorGUILayout.Space(10);

            // --- SECTION: TABLE CREATION ---
            GUILayout.Label("🛠️ TABLE MANAGEMENT", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            newTableName = EditorGUILayout.TextField("Table Name", newTableName);

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("➕ CREATE PRO TEMPLATE TABLE", GUILayout.Height(35)))
                RunAsyncAction(() => CreateManoFullTemplateAsync(newTableName));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            // --- SECTION: DATA SYNC ---
            GUILayout.Label("📂 SHEET SELECTION", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            if (GUILayout.Button("🔍 FIND ALL SHEETS", GUILayout.Height(30)))
                RunAsyncAction(FindAllSheetsAsync);

            if (targetSO.availableSheets != null && targetSO.availableSheets.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Select sheets to import:", EditorStyles.miniBoldLabel);

                EditorGUILayout.BeginVertical("helpbox");

                // ใช้ for loop เพื่อความปลอดภัยในการเข้าถึง index
                for (int i = 0; i < targetSO.availableSheets.Count; i++)
                {
                    string sName = targetSO.availableSheets[i];

                    // ป้องกัน KeyNotFoundException
                    if (!selectedSheets.ContainsKey(sName))
                        selectedSheets[sName] = true;

                    selectedSheets[sName] = EditorGUILayout.ToggleLeft(sName, selectedSheets[sName]);
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All")) SetAllSheets(true);
                if (GUILayout.Button("Deselect All")) SetAllSheets(false);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("📥 IMPORT SELECTED SHEETS", GUILayout.Height(50)))
                    RunAsyncAction(SyncAndGenerateAsync);
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox("No sheets found. Click 'FIND ALL SHEETS' to fetch data from Google.", MessageType.None);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);
            if (GUILayout.Button("Disconnect / Logout"))
            {
                googleSetting.AccessToken = "";
                googleSetting.RefreshToken = "";
                EditorUtility.SetDirty(googleSetting);
            }
        }

        private async void RunAsyncAction(Func<Task> action)
        {
            try
            {
                isWorking = true;
                await action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManoData] Error: {e.Message}");
            }
            finally
            {
                isWorking = false;
                Repaint();
            }
        }

        private void SetAllSheets(bool val)
        {
            foreach (var key in targetSO.availableSheets) selectedSheets[key] = val;
        }

        // ==========================================
        // GOOGLE API LOGIC (BATCH UPDATES & FORMATTING)
        // ==========================================

        private async Task InitializeProjectOAuth()
        {
            isWorking = true;
            await RefreshTokenIfNeeded();

            // 1. Create Welcome and _Config (hidden) sheets
            var initRequest = new
            {
                requests = new object[] {
                    new { addSheet = new { properties = new { title = "Welcome", index = 0, tabColor = new { red = 0.2f, green = 0.6f, blue = 1.0f } } } },
                    new { addSheet = new { properties = new { title = "_Config", hidden = true } } }
                }
            };
            await ExecuteBatchUpdate(initRequest);

            // 2. Decorate Welcome page
            string welcomeUrl = $"https://sheets.googleapis.com/v4/spreadsheets/{googleSetting.SpreadSheetID}/values/Welcome!B2:B5?valueInputOption=USER_ENTERED";
            var welcomeData = new
            {
                values = new[] {
                    new[] { "MANO DATA HUB" },
                    new[] { "STATUS: CONNECTED (OAUTH2)" },
                    new[] { "VERSION: " + TOOL_VERSION },
                    new[] { "READY TO SYNC WITH UNITY" }
                }
            };
            await SendPutRequest(welcomeUrl, JsonConvert.SerializeObject(welcomeData));

            isWorking = false;
            EditorUtility.DisplayDialog("Mano Hub", "Project Initialized Successfully!", "OK");
        }

        private async Task FindAllSheetsAsync()
        {
            if (googleSetting == null)
            {
                Debug.LogError("[ManoData] GoogleSettingSO is null!");
                return;
            }

            if (string.IsNullOrEmpty(googleSetting.SpreadSheetID))
            {
                EditorUtility.DisplayDialog("Error", "Please enter Spreadsheet ID in GoogleSettingSO first.", "OK");
                return;
            }

            isWorking = true;
            await RefreshTokenIfNeeded();

            string url = $"https://sheets.googleapis.com/v4/spreadsheets/{googleSetting.SpreadSheetID}?fields=sheets.properties.title";

            using (UnityWebRequest www = CreateAuthGet(url))
            {
                await www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonConvert.DeserializeObject<JObject>(www.downloadHandler.text);
                    var sheets = res["sheets"];

                    if (sheets != null)
                    {
                        targetSO.availableSheets.Clear();
                        foreach (var sheet in sheets)
                        {
                            string title = sheet["properties"]?["title"]?.ToString();
                            if (!string.IsNullOrEmpty(title) && title != "Welcome" && !title.StartsWith("_"))
                            {
                                targetSO.availableSheets.Add(title);
                                if (!selectedSheets.ContainsKey(title)) selectedSheets[title] = true;
                            }
                        }

                        if (targetSO.availableSheets.Count == 0)
                            Debug.LogWarning("[ManoData] Connection successful but no valid sheets found (Must not be 'Welcome' or start with '_').");
                        else
                            Debug.Log($"[ManoData] Found {targetSO.availableSheets.Count} Sheets");

                        EditorUtility.SetDirty(targetSO);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        Repaint();
                    }
                }
                else
                {
                    string errorDetail = www.downloadHandler.text;
                    Debug.LogError($"[ManoData] API Error: {errorDetail}");
                    EditorUtility.DisplayDialog("API Error", "Could not fetch data: \n" + errorDetail, "OK");
                }
            }
            isWorking = false;
            Repaint();
        }

        private async Task SyncAndGenerateAsync()
        {
            var sheetsToImport = selectedSheets.Where(x => x.Value).Select(x => x.Key).ToList();
            if (sheetsToImport.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "Please select at least one sheet to import.", "OK");
                return;
            }
            if (targetSO == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign Target Data SO.", "OK");
                return;
            }

            isWorking = true;

            try
            {
                await RefreshTokenIfNeeded();
                string rangesQuery = string.Join("&", sheetsToImport.Select(s => $"ranges={UnityWebRequest.EscapeURL(s)}!A1:Z5000"));
                string url = $"https://sheets.googleapis.com/v4/spreadsheets/{googleSetting.SpreadSheetID}/values:batchGet?{rangesQuery}";

                using (UnityWebRequest www = CreateAuthGet(url))
                {
                    await www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        targetSO.rawJson = www.downloadHandler.text;
                        targetSO.LoadDataFromJSON();

                        EditorUtility.SetDirty(targetSO);
                        AssetDatabase.SaveAssets();

                        Debug.Log("[ManoData] Data received. Starting code generation...");
                        ManoDataCodeGenerator.Generate(targetSO);

                        EditorUtility.DisplayDialog("Mano Sync", "Data Sync and Code Generation successful!", "OK");
                    }
                    else
                    {
                        Debug.LogError("Sync Error: " + www.downloadHandler.text);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManoData] Error during Sync: {e.Message}");
            }
            finally
            {
                isWorking = false;
            }
        }

        private async Task CreateManoFullTemplateAsync(string sheetName)
        {
            isWorking = true;
            await RefreshTokenIfNeeded();

            await ExecuteBatchUpdate(new { requests = new[] { new { addSheet = new { properties = new { title = sheetName } } } } });

            int sheetId = await GetSheetIdByName(sheetName);

            string valuesUrl = $"https://sheets.googleapis.com/v4/spreadsheets/{googleSetting.SpreadSheetID}/values/{sheetName}!A1:D3?valueInputOption=USER_ENTERED";
            var headerData = new
            {
                values = new[] {
                    new[] { "ID", "Name", "Type", "Description" },
                    new[] { "string", "string", "enum", "string" },
                    new[] { "ITEM_001", "Sample Item", "Weapon", "Add description here..." }
                }
            };
            await SendPutRequest(valuesUrl, JsonConvert.SerializeObject(headerData));

            var styleRequest = new
            {
                requests = new object[] {
                    new { updateSheetProperties = new { properties = new { sheetId = sheetId, gridProperties = new { frozenRowCount = 2 } }, fields = "gridProperties.frozenRowCount" }},

                    new { repeatCell = new {
                        range = new { sheetId = sheetId, startRowIndex = 0, endRowIndex = 2 },
                        cell = new { userEnteredFormat = new {
                            backgroundColor = new { red = 0.12f, green = 0.16f, blue = 0.23f },
                            textFormat = new { foregroundColor = new { red = 1f, green = 1f, blue = 1f }, bold = true },
                            horizontalAlignment = "CENTER"
                        }},
                        fields = "userEnteredFormat(backgroundColor,textFormat,horizontalAlignment)"
                    }},

                    new { addConditionalFormatRule = new {
                        rule = new {
                            ranges = new[] { new { sheetId = sheetId, startRowIndex = 2, startColumnIndex = 0, endColumnIndex = 10 } },
                            booleanRule = new {
                                condition = new {
                                    type = "CUSTOM_FORMULA",
                                    values = new[] { new { userEnteredValue = "=AND(LEN(A3)>0, ISERROR(A3))" } }
                                },
                                format = new {
                                    backgroundColor = new { red = 1f, green = 0f, blue = 0f },
                                    textFormat = new { foregroundColor = new { red = 1f, green = 1f, blue = 1f }, bold = true }
                                }
                            }
                        },
                        index = 0
                    }},

                    new { setDataValidation = new {
                        range = new { sheetId = sheetId, startRowIndex = 2, endRowIndex = 1000, startColumnIndex = 3, endColumnIndex = 4 },
                        rule = new {
                            condition = new {
                                type = "NUMBER_BETWEEN",
                                values = new[] { new { userEnteredValue = "0" }, new { userEnteredValue = "999999" } }
                            },
                            inputMessage = "Please enter numbers only!",
                            strict = false
                        }
                    }}
                }
            };
            await ExecuteBatchUpdate(styleRequest);

            isWorking = false;
            EditorUtility.DisplayDialog("Success", $"Table {sheetName} created successfully!", "OK");
        }

        // ==========================================
        // HELPERS & NETWORK
        // ==========================================

        private async Task ExecuteBatchUpdate(object payload)
        {
            string url = $"https://sheets.googleapis.com/v4/spreadsheets/{googleSetting.SpreadSheetID}:batchUpdate";
            using (UnityWebRequest www = CreateAuthPost(url, JsonConvert.SerializeObject(payload)))
            {
                await www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success) Debug.LogError("BatchUpdate Error: " + www.downloadHandler.text);
            }
        }

        private async Task<int> GetSheetIdByName(string name)
        {
            string url = $"https://sheets.googleapis.com/v4/spreadsheets/{googleSetting.SpreadSheetID}?fields=sheets.properties";
            using (UnityWebRequest www = CreateAuthGet(url))
            {
                await www.SendWebRequest();
                var res = JsonConvert.DeserializeObject<dynamic>(www.downloadHandler.text);
                foreach (var sheet in res.sheets)
                {
                    if (sheet.properties.title == name) return (int)sheet.properties.sheetId;
                }
            }
            return 0;
        }

        private async Task SendPutRequest(string url, string json)
        {
            using (UnityWebRequest www = UnityWebRequest.Put(url, json))
            {
                www.SetRequestHeader("Authorization", "Bearer " + googleSetting.AccessToken);
                www.SetRequestHeader("Content-Type", "application/json");
                await www.SendWebRequest();
            }
        }

        private UnityWebRequest CreateAuthPost(string url, string json)
        {
            UnityWebRequest www = UnityWebRequest.PostWwwForm(url, "POST");
            www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Authorization", "Bearer " + googleSetting.AccessToken);
            www.SetRequestHeader("Content-Type", "application/json");
            return www;
        }

        private UnityWebRequest CreateAuthGet(string url)
        {
            UnityWebRequest www = UnityWebRequest.Get(url);
            www.SetRequestHeader("Authorization", "Bearer " + googleSetting.AccessToken);
            return www;
        }

        private string GetAuthUrl()
        {
            string scope = "https://www.googleapis.com/auth/spreadsheets";
            return $"https://accounts.google.com/o/oauth2/v2/auth?client_id={googleSetting.ClientId}&redirect_uri=urn:ietf:wg:oauth:2.0:oob&response_type=code&scope={scope}";
        }

        private async Task ExchangeCodeAsync()
        {
            isWorking = true;
            WWWForm form = new WWWForm();
            form.AddField("client_id", googleSetting.ClientId);
            form.AddField("client_secret", googleSetting.ClientSecret);
            form.AddField("code", authCode);
            form.AddField("grant_type", "authorization_code");
            form.AddField("redirect_uri", "urn:ietf:wg:oauth:2.0:oob");
            using (UnityWebRequest www = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
            {
                await www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    var data = JsonConvert.DeserializeObject<OAuthResponse>(www.downloadHandler.text);
                    googleSetting.AccessToken = data.access_token;
                    googleSetting.RefreshToken = data.refresh_token;
                    googleSetting.ExpiryTime = DateTimeOffset.Now.ToUnixTimeSeconds() + data.expires_in;
                    EditorUtility.SetDirty(googleSetting);
                    authCode = "";
                }
            }
            isWorking = false;
        }

        private async Task RefreshTokenIfNeeded()
        {
            if (!googleSetting.IsTokenExpired) return;
            WWWForm form = new WWWForm();
            form.AddField("client_id", googleSetting.ClientId);
            form.AddField("client_secret", googleSetting.ClientSecret);
            form.AddField("refresh_token", googleSetting.RefreshToken);
            form.AddField("grant_type", "refresh_token");
            using (UnityWebRequest www = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form))
            {
                await www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    var data = JsonConvert.DeserializeObject<OAuthResponse>(www.downloadHandler.text);
                    googleSetting.AccessToken = data.access_token;
                    googleSetting.ExpiryTime = DateTimeOffset.Now.ToUnixTimeSeconds() + data.expires_in;
                    EditorUtility.SetDirty(googleSetting);
                }
            }
        }

        [Serializable] public class OAuthResponse { public string access_token; public string refresh_token; public int expires_in; }
    }
}