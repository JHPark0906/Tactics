using HS.Framework.Scene;
using UnityEditor;
using UnityEngine;

namespace HS.Framework.Editor.ProjectManagement
{
    /// <summary>SceneReference를 이동에 안전한 SceneAsset 필드로 표시한다.</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var pathProperty = property.FindPropertyRelative("scenePath");
            var guidProperty = property.FindPropertyRelative("sceneGuid");
            SynchronizeMovedScene(pathProperty, guidProperty);

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(pathProperty.stringValue);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var selectedScene = EditorGUI.ObjectField(position, label, sceneAsset, typeof(SceneAsset), false) as SceneAsset;
            if (EditorGUI.EndChangeCheck())
            {
                var scenePath = selectedScene == null ? string.Empty : AssetDatabase.GetAssetPath(selectedScene);
                pathProperty.stringValue = SceneLoader.NormalizeScenePath(scenePath);
                guidProperty.stringValue = string.IsNullOrEmpty(scenePath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(scenePath);
            }

            EditorGUI.EndProperty();
        }

        private static void SynchronizeMovedScene(
            SerializedProperty pathProperty,
            SerializedProperty guidProperty)
        {
            if (string.IsNullOrWhiteSpace(guidProperty.stringValue))
            {
                if (!string.IsNullOrWhiteSpace(pathProperty.stringValue))
                {
                    guidProperty.stringValue = AssetDatabase.AssetPathToGUID(pathProperty.stringValue);
                }

                return;
            }

            var currentPath = AssetDatabase.GUIDToAssetPath(guidProperty.stringValue);
            if (!string.IsNullOrWhiteSpace(currentPath) && currentPath.EndsWith(".unity"))
            {
                pathProperty.stringValue = SceneLoader.NormalizeScenePath(currentPath);
            }
            else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(pathProperty.stringValue) != null)
            {
                guidProperty.stringValue = AssetDatabase.AssetPathToGUID(pathProperty.stringValue);
            }
        }
    }
}
