using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System;

public class ManoDataHubTool : EditorWindow
{
    private string webAppUrl = "";
    private string projectName = "ManoDataHub";
    private bool isWorking = false;

    [MenuItem("Mano Tools/Mano Data Hub")]
    public static void ShowWindow() => GetWindow<ManoDataHubTool>("Mano Data Setup");

    private void OnEnable() => webAppUrl = EditorPrefs.GetString("Mano_UserUrl", "");

    void OnGUI()
    {
        GUILayout.Label("Mano Data Hub: One-Click Setup", EditorStyles.boldLabel);

        // --- STEP 1: Script Setup ---
        EditorGUILayout.HelpBox("1. Click the button below, then paste the code into Google Apps Script and Deploy as Web App.", MessageType.Info);
        if (GUILayout.Button("Copy Script Code to Clipboard"))
        {
            GUIUtility.systemCopyBuffer = GetGASCode(); // Code from above
            EditorUtility.DisplayDialog("Success", "Code copied! Now paste it in script.google.com", "OK");
            Application.OpenURL("https://script.google.com/");
        }

        EditorGUILayout.Space();

        // --- STEP 2: Connection Setup ---
        webAppUrl = EditorGUILayout.TextField("2. WebApp URL", webAppUrl);
        if (GUI.changed) EditorPrefs.SetString("Mano_UserUrl", webAppUrl);

        EditorGUILayout.Space();

        // --- STEP 3: Execution ---
        projectName = EditorGUILayout.TextField("Project Name", projectName);

        // Disable UI while processing to prevent duplicate requests
        GUI.enabled = !isWorking && !string.IsNullOrEmpty(webAppUrl);
        if (GUILayout.Button(isWorking ? "Setting up Project..." : "3. Setup Project & Template"))
        {
            _ = SetupProjectAsync();
        }
        GUI.enabled = true;
    }

    /// <summary>
    /// Sends a request to Google WebApp to create a new Spreadsheet with Template
    /// </summary>
    private async Task SetupProjectAsync()
    {
        isWorking = true;
        Repaint();

        string json = "{\"action\":\"setup_project\", \"fileName\":\"" + projectName + "\"}";

        // Use a standard POST request for Google Apps Script
        using (UnityWebRequest www = new UnityWebRequest(webAppUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            var op = www.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // Successfully connected
                Debug.Log("<color=green>Server Response:</color> " + www.downloadHandler.text);
                var res = JsonUtility.FromJson<GASResponse>(www.downloadHandler.text);
                if (res != null && !string.IsNullOrEmpty(res.url))
                {
                    Application.OpenURL(res.url);
                }
            }
            else
            {
                // Detailed Error Logging
                Debug.LogError($"Setup Failed: {www.error}");
                Debug.LogError($"Response Code: {www.responseCode}");
                // If it's a 401, this will print the HTML error page from Google to help debug
                Debug.Log("Full Error Response: " + www.downloadHandler.text);
            }
        }

        isWorking = false;
        Repaint();
    }

    [Serializable]
    private class GASResponse { public string url; public string spreadsheetId; }

    private string GetGASCode()
    {
        return @"/**
 * MANO DATA HUB - SAFE REFERENCE ENGINE
 */

function onOpen() {
  SpreadsheetApp.getUi()
      .createMenu('🛠️ Mano Tool')
      .addItem('Open Control Panel', 'showSidebar')
      .addToUi();
}

// 1. SMART AUTO-COLUMN (ตรวจจับการพิมพ์ Header)
function onEdit(e) {
  var range = e.range;
  var sheet = range.getSheet();
  var sheetName = sheet.getName();

  if (sheetName === 'ManoDataHub' || sheetName.startsWith('_')) return;

  if (range.getRow() === 1 && range.getValue() !== '') {
    setupSmartColumn(sheet, range.getColumn());
  }
}

// ฟังก์ชันสร้าง Config แบบปลอดภัย
function getOrCreateConfig(ss) {
  var config = ss.getSheetByName('_Config');
  if (!config) {
    config = ss.insertSheet('_Config');
    var types = [['string'],['int'],['float'],['bool'],['vector2'],['vector3'],['color'],['enum']];
    config.getRange(1,1,types.length,1).setValues(types);
    config.hideSheet();
    SpreadsheetApp.flush();
  }
  return config;
}

function setupSmartColumn(sheet, col) {
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  var config = getOrCreateConfig(ss);

  // 1. Format Header
  sheet.getRange(1, col, 2, 1).setBackground('#111111').setFontColor('#ffffff').setFontWeight('bold');
  var descCell = sheet.getRange(3, col);
  descCell.setFontColor('#666666').setFontStyle('italic');
  if (descCell.getValue() === '') descCell.setValue('Description...');

  // 2. Setup Validation
  var typeRule = SpreadsheetApp.newDataValidation()
      .requireValueInRange(config.getRange('A1:A8'))
      .setAllowInvalid(false)
      .build();
  sheet.getRange(2, col).setDataValidation(typeRule);

  updateSheetVisuals(sheet);
}

function updateSheetVisuals(sheet) {
  var lastCol = sheet.getLastColumn();
  if (lastCol < 1) return;
  var dataRange = sheet.getRange(4, 1, 997, lastCol);

  sheet.clearConditionalFormatRules();
  var emptyRule = SpreadsheetApp.newConditionalFormatRule()
      .whenCellEmpty()
      .setBackground('#757575')
      .setRanges([dataRange])
      .build();
  
  var rules = sheet.getConditionalFormatRules();
  rules.push(emptyRule);
  sheet.setConditionalFormatRules(rules);

  var bandings = dataRange.getBandings();
  for (var i = 0; i < bandings.length; i++) bandings[i].remove();
  dataRange.applyRowBanding(SpreadsheetApp.BandingTheme.LIGHT_GREY, false, false);
}

function doPost(e) {
  var params = JSON.parse(e.postData.contents);
  var ss = SpreadsheetApp.getActiveSpreadsheet();

  try {
    if (params.action === 'setup_project') {
      // สร้าง Config ก่อนเสมอด้วยฟังก์ชันปลอดภัย
      getOrCreateConfig(ss);
      
      // สร้างหน้าคู่มือ
      createWelcomeSheet(ss);
      
      // สร้างหน้าเริ่มต้น
      var defaultSheet = ss.getSheetByName('DefaultTable') || ss.insertSheet('DefaultTable');
      defaultSheet.getRange(1,1).setValue('ID');
      setupSmartColumn(defaultSheet, 1);
      defaultSheet.setFrozenRows(3);
      
      // ลบ Sheet1 ถ้ามี
      var sheet1 = ss.getSheetByName('Sheet1');
      if (sheet1) ss.deleteSheet(sheet1);
      
      SpreadsheetApp.flush();
      return ContentService.createTextOutput(JSON.stringify({success: true, url: ss.getUrl()})).setMimeType(ContentService.MimeType.JSON);
    }

    if (params.action === 'fetch_all_data') {
      var sheets = ss.getSheets();
      var result = {};
      sheets.forEach(sheet => {
        var name = sheet.getName();
        if (name === 'ManoDataHub' || name.startsWith('_')) return;
        var data = sheet.getDataRange().getValues();
        if (data.length < 4) return;
        var headers = data[0];
        result[name] = data.slice(3).map(row => {
          var obj = {};
          row.forEach((cell, i) => { if (headers[i]) obj[headers[i]] = cell; });
          return obj;
        });
      });
      return ContentService.createTextOutput(JSON.stringify(result)).setMimeType(ContentService.MimeType.JSON);
    }
  } catch (err) {
    return ContentService.createTextOutput(JSON.stringify({success: false, error: err.toString()})).setMimeType(ContentService.MimeType.JSON);
  }
}

function showSidebar() {
  var html = HtmlService.createHtmlOutput('<body style=""background:#0f172a;color:white;padding:20px;font-family:sans-serif""><h3>MANO HUB</h3><button style=""width:100%;padding:10px;background:#2563eb;color:white;border:none;border-radius:4px;cursor:pointer"" onclick=""google.script.run.addNewGroup()"">+ NEW TABLE</button></body>').setTitle('Mano Control').setWidth(300);
  SpreadsheetApp.getUi().showSidebar(html);
}

function createWelcomeSheet(ss) {
  var sheet = ss.getSheetByName('ManoDataHub') || ss.insertSheet('ManoDataHub', 0);
  sheet.getRange('A1:Z100').setBackground('white');
  sheet.getRange('B2').setValue('Language (th/en):').setFontWeight('bold');
  sheet.getRange('C2').setValue('th');
}

function addNewGroup() {
  var name = Browser.inputBox('Enter Table Name:');
  if (name && name !== 'cancel') {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.insertSheet(name);
    sheet.getRange(1,1).setValue('ID');
    setupSmartColumn(sheet, 1);
    sheet.setFrozenRows(3);
  }
}";
    }
}