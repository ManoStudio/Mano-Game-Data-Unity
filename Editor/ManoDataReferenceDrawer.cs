using UnityEngine;
using UnityEditor;
using System.Linq;

namespace ManoData
{
    [CustomPropertyDrawer(typeof(ManoDataReference))]
    public class ManoDataReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty dataSOProp = property.FindPropertyRelative("gameDataDocumentSO");
            SerializedProperty groupProp = property.FindPropertyRelative("groupName");
            SerializedProperty tableProp = property.FindPropertyRelative("tableName");
            SerializedProperty idProp = property.FindPropertyRelative("rowId");

            GameDataDocumentSO dataSO = dataSOProp.objectReferenceValue as GameDataDocumentSO;

            if (dataSO == null)
            {
                EditorGUI.PropertyField(position, dataSOProp, label);
                EditorGUI.EndProperty();
                return;
            }

            if (dataSO.document == null || dataSO.document.tables.Count == 0)
                dataSO.LoadDataFromJSON();

            Rect contentRect = EditorGUI.PrefixLabel(position, label);
            float w = contentRect.width / 3;

            string[] groups = dataSO.document.groups.ToArray();
            if (groups.Length == 0) groups = new[] { "Default" };

            int gIdx = System.Array.IndexOf(groups, groupProp.stringValue);
            if (gIdx == -1)
            {
                gIdx = 0;
                groupProp.stringValue = groups[0];
            }
            gIdx = EditorGUI.Popup(new Rect(contentRect.x, contentRect.y, w - 2, contentRect.height), gIdx, groups);
            groupProp.stringValue = groups[gIdx];

            var tablesInGroup = dataSO.document.tables.Where(t => t.group == groups[gIdx]).Select(t => t.name).ToArray();
            if (tablesInGroup.Length == 0) tablesInGroup = new[] { "No Tables" };

            int tIdx = System.Array.IndexOf(tablesInGroup, tableProp.stringValue);
            if (tIdx == -1)
            {
                tIdx = 0;
                tableProp.stringValue = tablesInGroup[0];
            }
            tIdx = EditorGUI.Popup(new Rect(contentRect.x + w, contentRect.y, w - 2, contentRect.height), tIdx, tablesInGroup);
            tableProp.stringValue = tablesInGroup[tIdx];

            var currentTable = dataSO.document.tables.FirstOrDefault(t => t.name == tableProp.stringValue);
            if (currentTable != null && currentTable.data.Count > 0)
            {
                string[] ids = currentTable.data.Select(d => d.Values.FirstOrDefault()?.ToString() ?? "N/A").ToArray();
                int iIdx = System.Array.IndexOf(ids, idProp.stringValue);

                if (iIdx == -1)
                {
                    iIdx = 0;
                    idProp.stringValue = ids[0];
                }

                EditorGUI.BeginChangeCheck();
                iIdx = EditorGUI.Popup(new Rect(contentRect.x + w * 2, contentRect.y, w, contentRect.height), iIdx, ids);
                if (EditorGUI.EndChangeCheck())
                {
                    idProp.stringValue = ids[iIdx];
                }
            }
            else
            {
                EditorGUI.LabelField(new Rect(contentRect.x + w * 2, contentRect.y, w, contentRect.height), "Empty Table");
                idProp.stringValue = "";
            }

            if (GUI.changed)
            {
                property.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(property.serializedObject.targetObject);
            }

            EditorGUI.EndProperty();
        }
    }
}