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
        // เก็บตำแหน่ง Scroll แยกตามชื่อตาราง
        private Dictionary<string, Vector2> tableScrollPositions = new Dictionary<string, Vector2>();
        private const float COLUMN_WIDTH = 120f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ManoDataDocumentSO so = (ManoDataDocumentSO)target;

            EditorGUILayout.Space(15);
            GUILayout.Label("📊 DATA EXPLORER (RAW DATA)", EditorStyles.boldLabel);

            // เช็คว่าข้อมูลหายไปหรือไม่ (เช่น หลังรีโหลดโค้ด)
            if (!so.HasDataToPreview)
            {
                if (!string.IsNullOrEmpty(so.rawJson))
                {
                    EditorGUILayout.HelpBox("Preview data is cleared from memory. Click 'Restore' to see it again without re-importing.", MessageType.Warning);
                    if (GUILayout.Button("📂 Restore Preview from JSON", GUILayout.Height(30)))
                    {
                        so.RestoreFromRawJson();
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
                    $"Table: {table.name} ({table.data.Count} Rows)", true, EditorStyles.foldoutHeader);

                if (tableFoldouts[table.name])
                {
                    // ส่งตำแหน่ง Scroll เฉพาะของตารางนี้เข้าไป
                    Vector2 currentPos = tableScrollPositions[table.name];
                    tableScrollPositions[table.name] = DrawFixedTableGrid(table, currentPos);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (Application.isPlaying) Repaint();
        }

        private Vector2 DrawFixedTableGrid(TableContent table, Vector2 scrollPos)
        {
            if (table.schema == null || table.schema.Count == 0) return scrollPos;

            float totalWidth = table.schema.Count * COLUMN_WIDTH;

            // แยก ScrollView ของตารางนี้
            Vector2 newScrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(Mathf.Min(table.data.Count * 22 + 60, 300)));

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

            for (int i = 0; i < table.data.Count; i++)
            {
                var row = table.data[i];
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

                Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                Handles.DrawLine(new Vector3(rowRect.x, rowRect.yMax), new Vector3(rowRect.x + totalWidth, rowRect.yMax));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            return newScrollPos;
        }
    }
}