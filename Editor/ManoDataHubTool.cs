using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ManoData
{
    public class ManoDataHubTool : EditorWindow
    {
        private const float TOOL_VERSION = 0.1f;

        private string webAppUrl = "";
        private string spreadsheetUrl = "";
        private string sheetName = "ManoDataSheet"; // Default name for new users
        private bool isWorking = false;
        public GameDataDocumentSO targetSO;

        [MenuItem("Mano Tools/Mano Data Hub")]
        public static void ShowWindow() => GetWindow<ManoDataHubTool>("Mano Dashboard");

        private void OnEnable()
        {
            webAppUrl = EditorPrefs.GetString("Mano_UserUrl", "");
            spreadsheetUrl = EditorPrefs.GetString("Mano_SheetUrl", "");
        }

        void OnGUI()
        {
            if (isWorking)
            {
                EditorGUILayout.HelpBox("Connecting to Google Cloud... Please wait.", MessageType.Info);
                return;
            }

            if (string.IsNullOrEmpty(spreadsheetUrl)) DrawSetupView();
            else DrawDashboardView();
        }

        void DrawSetupView()
        {
            GUILayout.Label("🛠️ SETUP MANO DATA HUB", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Follow these steps:\n1. Copy Script & Open Editor\n2. Add 'Drive API' in Services (+)\n3. Deploy as Web App (Access: Anyone)\n4. If first time, click 'Open Authorize Page' and Run function.", MessageType.Info);

            if (GUILayout.Button("1. Copy & Open Google Script Editor", GUILayout.Height(30)))
            {
                GUIUtility.systemCopyBuffer = GetGASCode();
                Application.OpenURL("https://script.google.com/home/projects/create"); // Open direct new project page
            }

            webAppUrl = EditorGUILayout.TextField("2. WebApp URL (Exec)", webAppUrl);
            sheetName = EditorGUILayout.TextField("3. Spreadsheet Name", sheetName);

            if (GUILayout.Button("4. Connect & Setup Project", GUILayout.Height(40))) _ = SetupProjectAsync();

            if (!string.IsNullOrEmpty(webAppUrl))
            {
                if (GUILayout.Button("⚠️ Need Help? Open Authorize Page"))
                {
                    // Redirects user to the script editor to click 'Run'
                    string editorUrl = webAppUrl.Replace("/exec", "/edit");
                    Application.OpenURL(editorUrl);
                }
            }
        }

        void DrawDashboardView()
        {
            EditorGUILayout.LabelField($"System Version: {TOOL_VERSION}", EditorStyles.miniLabel);
            targetSO = (GameDataDocumentSO)EditorGUILayout.ObjectField("Target Data SO", targetSO, typeof(GameDataDocumentSO), false);

            GUILayout.Space(10);
            if (GUILayout.Button("🌐 OPEN GOOGLE SHEET", GUILayout.Height(40))) Application.OpenURL(spreadsheetUrl);

            GUILayout.Space(10);

            // ปุ่ม Update แยกออกมา (ใส่สีให้เด่นถ้าต้องการให้อัปเดต)
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f); // สีฟ้าอ่อน
            if (GUILayout.Button("🚀 UPDATE SYSTEM FEATURES", GUILayout.Height(30)))
            {
                _ = UpdateSystemAsync();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("📥 IMPORT & SYNC DATA", GUILayout.Height(50))) _ = ImportDataAsync();
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Reset Connection"))
            {
                if (EditorUtility.DisplayDialog("Reset", "Clear connection settings?", "Yes", "No"))
                {
                    spreadsheetUrl = "";
                    EditorPrefs.SetString("Mano_SheetUrl", "");
                    Repaint();
                }
            }
        }

        private async Task UpdateSystemAsync()
        {
            if (EditorUtility.DisplayDialog("Confirm Update", "This will update your Google Sheet templates and styles to the latest version. Your data rows will remain safe.", "Update Now", "Cancel"))
            {
                isWorking = true;
                string json = $"{{\"action\":\"update_system\", \"version\":{TOOL_VERSION}}}";

                using (UnityWebRequest www = CreatePost(webAppUrl, json))
                {
                    await www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        EditorUtility.DisplayDialog("Update Success", "System features have been updated to v" + TOOL_VERSION, "OK");
                    }
                    else
                    {
                        Debug.LogError("Update Failed: " + www.error);
                    }
                }
                isWorking = false;
            }
        }

        private async Task SetupProjectAsync()
        {
            if (string.IsNullOrEmpty(webAppUrl)) return;
            isWorking = true;

            // แก้ไขโครงสร้าง JSON ให้ถูกต้อง (ลบเครื่องหมายคอมม่าที่เกินออก)
            string json = $"{{\"action\":\"setup_project\", \"sheet_name\":\"{sheetName}\", \"current_version\":{TOOL_VERSION}}}";

            using (UnityWebRequest www = CreatePost(webAppUrl, json))
            {
                await www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Network Error: " + www.error);
                    isWorking = false;
                    return;
                }

                string responseText = www.downloadHandler.text;

                // ตรวจสอบ Authorization (กรณี User ยังไม่ได้กด Run ใน GAS)
                if (responseText.Contains("Authorization is required") || responseText.Contains("คุณไม่ได้รับอนุญาต"))
                {
                    EditorUtility.DisplayDialog("Auth Required", "Please click 'Open Authorize Page' and Run the INITIAL_AUTH_CLICK_HERE function first.", "OK");
                }
                else
                {
                    try
                    {
                        var res = JsonUtility.FromJson<GASResponse>(responseText);
                        if (res != null && res.success)
                        {
                            spreadsheetUrl = res.url;
                            EditorPrefs.SetString("Mano_SheetUrl", spreadsheetUrl);
                            EditorPrefs.SetString("Mano_UserUrl", webAppUrl);
                            Application.OpenURL(spreadsheetUrl);
                            Repaint();
                        }
                        else
                        {
                            Debug.LogError("GAS Error: " + (res != null ? res.error : "Unknown Error"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("JSON Parse Error: " + ex.Message + "\nRaw Response: " + responseText);
                    }
                }
            }
            isWorking = false;
        }

        private async Task ImportDataAsync()
        {
            if (targetSO == null) return;
            isWorking = true;
            string json = "{\"action\":\"import_data\"}";
            using (UnityWebRequest www = CreatePost(webAppUrl, json))
            {
                await www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    targetSO.rawJson = www.downloadHandler.text;
                    targetSO.LoadDataFromJSON();
                    EditorUtility.SetDirty(targetSO);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("Success", "Data synchronized!", "OK");
                }
            }
            isWorking = false;
        }

        private UnityWebRequest CreatePost(string url, string json)
        {
            UnityWebRequest www = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            return www;
        }

        [Serializable] private class GASResponse { public bool success; public string url; public string error; }

        private string GetGASCode()
        {
            return @"/**
 * MANO DATA HUB - PROFESSIONAL ENGINE (SAFE UPDATE VERSION)
 */

var CURRENT_VERSION = 0.1; // เลขเวอร์ชันของระบบ

/** * STEP 1: CLICK RUN ON THIS FUNCTION TO AUTHORIZE */
function INITIAL_AUTH_CLICK_HERE() {
  DriveApp.getRootFolder();
  SpreadsheetApp.getActive();
  console.log('Authorization Successful! Version: ' + CURRENT_VERSION);
}

function onOpen() {
  SpreadsheetApp.getUi()
      .createMenu('🛠️ Mano Tool')
      .addItem('Open Dashboard', 'showSidebar')
      .addToUi();
}

function showSidebar() {
  var htmlContent = `
    <!DOCTYPE html>
    <html>
      <head>
        <link href=""https://cdn.jsdelivr.net/npm/tailwindcss@2.2.19/dist/tailwind.min.css"" rel=""stylesheet"">
        <style>
          body { background-color: #0f172a; color: #f1f5f9; font-family: sans-serif; }
          .btn-primary { background: #2563eb; transition: all 0.3s; font-weight: bold; }
          .btn-primary:hover { background: #3b82f6; transform: translateY(-1px); }
          .card { background: #1e293b; border: 1px solid #334155; }
        </style>
      </head>
      <body class=""p-6"">
        <div class=""mb-6"">
          <h1 class=""text-2xl font-black text-blue-500 tracking-tighter"">MANO HUB</h1>
          <p class=""text-[10px] text-slate-500 uppercase font-bold"">Database Controller v${CURRENT_VERSION}</p>
        </div>
        
        <div class=""card rounded-xl p-4 mb-4"">
          <h2 class=""text-xs font-bold text-slate-400 uppercase mb-3"">Quick Actions</h2>
          <button onclick=""google.script.run.addNewGroup()"" class=""btn-primary w-full py-3 rounded-lg text-sm shadow-lg mb-3"">
            + CREATE NEW TABLE
          </button>
          <button onclick=""location.reload()"" class=""w-full py-2 text-slate-400 text-xs font-bold hover:text-white"">
            REFRESH PANEL
          </button>
        </div>
        <div class=""text-[10px] text-slate-600 text-center mt-10"">
          Connected to Unity Editor
        </div>
      </body>
    </html>
  `;
  var htmlOutput = HtmlService.createHtmlOutput(htmlContent).setTitle('Mano Dashboard').setWidth(300);
  SpreadsheetApp.getUi().showSidebar(htmlOutput);
}

function onEdit(e) {
  var range = e.range;
  var sheet = range.getSheet();
  if (sheet.getName() === 'Welcome' || sheet.getName().startsWith('_')) return;
  if (range.getRow() === 1 && range.getValue() !== '') {
    setupSmartColumn(sheet, range.getColumn());
  }
}

function getOrCreateConfig(ss) {
  var config = ss.getSheetByName('_Config') || ss.insertSheet('_Config');
  if (config.getRange(1,1).getValue() === '') {
    var types = [['string'],['int'],['float'],['bool'],['vector2'],['vector3'],['color'],['enum']];
    config.getRange(1,1,types.length,1).setValues(types);
    config.getRange('B1').setValue(CURRENT_VERSION); // บันทึกเวอร์ชันครั้งแรก
    config.hideSheet();
  }
  return config;
}

function runMigration(ss, userVersion) {
  if (userVersion < 0.1) {
    var sheets = ss.getSheets();
    sheets.forEach(function(s) {
      if (s.getName() === 'Welcome' || s.getName().startsWith('_')) return;
      // อัปเดตสี Header ใหม่ให้สวยขึ้น โดยไม่ลบข้อมูล
      var lastCol = s.getLastColumn();
      if (lastCol > 0) s.getRange(1, 1, 2, lastCol).setBackground('#1e293b').setFontColor('#f1f5f9');
    });
  }
}

function setupSmartColumn(sheet, col) {
  var ss = sheet.getParent();
  var config = getOrCreateConfig(ss);
  sheet.getRange(1, col, 2, 1).setBackground('#1e293b').setFontColor('#f1f5f9').setFontWeight('bold').setHorizontalAlignment('center');
  sheet.getRange(3, col).setFontColor('#64748b').setFontStyle('italic').setFontSize(9);
  if (sheet.getRange(3, col).getValue() === '') sheet.getRange(3, col).setValue('Description...');
  var typeRule = SpreadsheetApp.newDataValidation().requireValueInRange(config.getRange('A1:A8')).setAllowInvalid(false).build();
  sheet.getRange(2, col).setDataValidation(typeRule);
  updateSheetVisuals(sheet);
}

function updateSheetVisuals(sheet) {
  var lastCol = sheet.getLastColumn();
  if (lastCol < 1) return;
  var dataRange = sheet.getRange(4, 1, 997, lastCol);
  sheet.clearConditionalFormatRules();
  var emptyRule = SpreadsheetApp.newConditionalFormatRule().whenCellEmpty().setBackground('#f8fafc').setRanges([dataRange]).build();
  sheet.setConditionalFormatRules([emptyRule]);
  var bandings = dataRange.getBandings();
  bandings.forEach(function(b) { b.remove(); });
  dataRange.applyRowBanding(SpreadsheetApp.BandingTheme.LIGHT_GREY, false, false);
}

function doPost(e) {
  try {
    var params = JSON.parse(e.postData.contents);
    var ss = SpreadsheetApp.getActive();
    var requestedName = params.sheet_name || 'ManoDataSheet';
    
    if (!ss) {
      var files = DriveApp.getFilesByName(requestedName);
      ss = files.hasNext() ? SpreadsheetApp.open(files.next()) : SpreadsheetApp.create(requestedName);
    }

    if (params.action === 'setup_project') {
      ss.setName(requestedName);
      var config = getOrCreateConfig(ss);
      
      // ตรวจสอบ Version และ Migrate
      var userVersion = config.getRange('B1').getValue() || 0;
      if (userVersion < CURRENT_VERSION) {
        runMigration(ss, userVersion);
        config.getRange('B1').setValue(CURRENT_VERSION);
      }

      createWelcomeSheet(ss);
      
      var defaultSheet = ss.getSheetByName('Items_Example') || ss.insertSheet('Items_Example');
      if (defaultSheet.getRange(1,1).getValue() === '') {
        defaultSheet.getRange(1, 1, 3, 4).setValues([
          ['ID', 'Name', 'Price', 'IsActive'],
          ['string', 'string', 'int', 'bool'],
          ['POT_01', 'Health Potion', '50', 'true']
        ]);
        for(var i=1; i<=4; i++) setupSmartColumn(defaultSheet, i);
        defaultSheet.setFrozenRows(3);
      }
      
      var file = DriveApp.getFileById(ss.getId());
      file.setSharing(DriveApp.Access.ANYONE_WITH_LINK, DriveApp.Permission.EDIT);
      return ContentService.createTextOutput(JSON.stringify({success: true, url: ss.getUrl()})).setMimeType(ContentService.MimeType.JSON);
    }

    // --- ACTION: UPDATE SYSTEM (ปุ่มใหม่ที่คุณต้องการ) ---
    if (params.action === 'update_system') {
       var config = getOrCreateConfig(ss);
       var userVersion = config.getRange('B1').getValue() || 0;
       var newVersion = params.version;

       runMigration(ss, userVersion);
       
       createWelcomeSheet(ss); 
       
       config.getRange('B1').setValue(newVersion);
       
       SpreadsheetApp.flush();
       return ContentService.createTextOutput(JSON.stringify({success: true, message: 'Updated to ' + newVersion})).setMimeType(ContentService.MimeType.JSON);
    }

    if (params.action === 'import_data') {
      var tables = [];
      ss.getSheets().forEach(function(sheet) {
        var name = sheet.getName();
        if (name === 'Welcome' || name.startsWith('_')) return;
        var vals = sheet.getDataRange().getValues();
        if (vals.length < 2) return;
        var schema = [], headers = vals[0], types = vals[1];
        for (var i = 0; i < headers.length; i++) { 
          if (headers[i]) schema.push({ name: headers[i].toString(), type: types[i] || 'string' }); 
        }
        var data = [];
        for (var r = 3; r < vals.length; r++) {
          var row = {}, hasVal = false;
          for (var c = 0; c < headers.length; c++) { 
            if (headers[c]) { 
              row[headers[c]] = vals[r][c]; 
              if (vals[r][c] !== '') hasVal = true; 
            } 
          }
          if (hasVal) data.push(row);
        }
        tables.push({ name: name, group: 'Default', schema: schema, data: data });
      });
      return ContentService.createTextOutput(JSON.stringify([{ data: { tables: tables, groups: ['Default'], lastEdit: new Date().toISOString() } }])).setMimeType(ContentService.MimeType.JSON);
    }
  } catch (err) {
    return ContentService.createTextOutput(JSON.stringify({success: false, error: err.toString()})).setMimeType(ContentService.MimeType.JSON);
  }
}

function createWelcomeSheet(ss) {
  var sheet = ss.getSheetByName('Welcome') || ss.insertSheet('Welcome', 0);
  if (sheet.getRange('B2').getValue() !== '') return; // ถ้ามีเนื้อหาแล้วไม่ทับ
  sheet.clear();
  sheet.getRange('B2').setValue('MANO DATA HUB').setFontSize(20).setFontWeight('bold').setFontColor('#2563eb');
  sheet.getRange('B3').setValue('Unity Synchronization Active').setFontColor('#64748b');
  sheet.getRange('B5').setValue('Instructions:').setFontWeight('bold');
  sheet.getRange('B6').setValue('1. Add columns in Row 1');
  sheet.getRange('B7').setValue('2. Select Types in Row 2');
  sheet.getRange('B8').setValue('3. Sync from Unity Editor');
  sheet.setColumnWidth(2, 250);
}

function addNewGroup() {
  var name = Browser.inputBox('🛠️ Create New Table', 'Enter table name:', Browser.Buttons.OK_CANCEL);
  if (name && name !== 'cancel') {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    if (ss.getSheetByName(name)) return;
    var sheet = ss.insertSheet(name);
    sheet.getRange(1,1).setValue('ID');
    setupSmartColumn(sheet, 1);
    sheet.setFrozenRows(3);
  }
}";
        }
    }
}