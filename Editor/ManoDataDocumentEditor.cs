using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Mano.Data.Editor
{
    [CustomEditor(typeof(ManoDataDocumentSO))]
    public class ManoDataDocumentEditor : UnityEditor.Editor
    {
        private Dictionary<string, bool> tableFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, Vector2> tableScrollPositions = new Dictionary<string, Vector2>();

        // Cache ข้อมูล Dictionary สำหรับใช้วาดใน Editor เท่านั้น เพื่อความลื่นไหล
        private Dictionary<string, List<Dictionary<string, object>>> _editorDisplayCache = new Dictionary<string, List<Dictionary<string, object>>>();

        private const float COLUMN_WIDTH = 120f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ManoDataDocumentSO so = (ManoDataDocumentSO)target;

            EditorGUILayout.Space(15);
            GUILayout.Label("📊 DATA EXPLORER (SERIALIZED)", EditorStyles.boldLabel);

            if (!so.HasDataToPreview)
            {
                if (!string.IsNullOrEmpty(so.rawJson))
                {
                    EditorGUILayout.HelpBox("Data is available in Raw JSON but not yet parsed into Serialized Rows.", MessageType.Warning);
                    if (GUILayout.Button("📂 Parse JSON to Serialized Rows", GUILayout.Height(30)))
                    {
                        so.LoadDataFromJSON();
                        _editorDisplayCache.Clear();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No data available. Please Sync from Google Sheets.", MessageType.Info);
                }
                return;
            }

            foreach (var table in so.document.tables)
            {
                if (!tableFoldouts.ContainsKey(table.name)) tableFoldouts[table.name] = false;
                if (!tableScrollPositions.ContainsKey(table.name)) tableScrollPositions[table.name] = Vector2.zero;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                tableFoldouts[table.name] = EditorGUILayout.Foldout(tableFoldouts[table.name],
                    $"Table: {table.name} ({table.rows.Count} Rows)", true, EditorStyles.foldoutHeader);

                if (tableFoldouts[table.name])
                {
                    if (!_editorDisplayCache.ContainsKey(table.name) || _editorDisplayCache[table.name].Count != table.rows.Count)
                    {
                        _editorDisplayCache[table.name] = table.GetRuntimeData();
                    }

                    Vector2 currentPos = tableScrollPositions[table.name];
                    tableScrollPositions[table.name] = DrawFixedTableGrid(table, _editorDisplayCache[table.name], currentPos);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (Application.isPlaying) Repaint();
        }

        private Vector2 DrawFixedTableGrid(TableContent table, List<Dictionary<string, object>> displayData, Vector2 scrollPos)
        {
            if (table.schema == null || table.schema.Count == 0) return scrollPos;

            float totalWidth = table.schema.Count * COLUMN_WIDTH;

            float viewHeight = Mathf.Min(displayData.Count * 22 + 45, 300);
            Vector2 newScrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(viewHeight));

            EditorGUILayout.BeginVertical(GUILayout.Width(totalWidth));

            // --- DRAW HEADER ---
            EditorGUILayout.BeginHorizontal();
            GUIStyle headerStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 35
            };

            foreach (var col in table.schema)
            {
                string headerLabel = $"<b>{col.name}</b>\n<color=#00e6e6><size=9>{col.type}</size></color>";
                GUILayout.Label(headerLabel, headerStyle, GUILayout.Width(COLUMN_WIDTH));
            }
            EditorGUILayout.EndHorizontal();

            // --- DRAW ROWS ---
            GUIStyle cellStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 0, 0),
                fontSize = 11
            };

            for (int i = 0; i < displayData.Count; i++)
            {
                var row = displayData[i];
                Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));

                if (i % 2 == 0)
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, totalWidth, rowRect.height), new Color(0.3f, 0.3f, 0.3f, 0.2f));

                foreach (var col in table.schema)
                {
                    string valueStr = row.TryGetValue(col.name, out object val) ? val?.ToString() ?? "—" : "—";
                    if (valueStr.Contains("|")) valueStr = valueStr.Replace("|", ", ");
                    EditorGUILayout.LabelField(valueStr, cellStyle, GUILayout.Width(COLUMN_WIDTH));
                }
                EditorGUILayout.EndHorizontal();

                Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.3f);
                Handles.DrawLine(new Vector3(rowRect.x, rowRect.yMax), new Vector3(rowRect.x + totalWidth, rowRect.yMax));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            return newScrollPos;
        }
    }
}