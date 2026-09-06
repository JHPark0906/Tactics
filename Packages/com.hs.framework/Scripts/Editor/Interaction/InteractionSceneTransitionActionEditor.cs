using System;
using System.Collections.Generic;
using System.IO;
using HS.Framework.Interaction.Actions;
using UnityEditor;
using UnityEngine;

namespace HS.Framework.Editor.Interaction
{
    /// <summary>
    /// 씬 전환 상호작용 동작의 씬 참조를 Build Settings 목록에서 선택하도록 표시한다.
    /// </summary>
    [CustomEditor(typeof(InteractionSceneTransitionAction))]
    public sealed class InteractionSceneTransitionActionEditor : UnityEditor.Editor
    {
        private SerializedProperty _destinationScene;
        private SerializedProperty _loadingScene;
        private SerializedProperty _useLoadingScene;

        private void OnEnable()
        {
            _useLoadingScene = serializedObject.FindProperty("useLoadingScene");
            _loadingScene = serializedObject.FindProperty("loadingScene");
            _destinationScene = serializedObject.FindProperty("destinationScene");
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var scenePaths = GetTransitionScenePaths();
            EditorGUILayout.LabelField("Scene Transition", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useLoadingScene, new GUIContent("로딩 씬 경유"));
            if (_useLoadingScene.boolValue)
            {
                DrawScenePopup(_loadingScene, "로딩 씬", scenePaths, true);
            }

            DrawScenePopup(_destinationScene, "도착 씬", scenePaths, false);
            DrawValidation();

            if (GUILayout.Button("Build Profiles에서 씬 목록 열기"))
            {
                OpenBuildSceneSettings();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static List<string> GetTransitionScenePaths()
        {
            var scenePaths = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 1; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    scenePaths.Add(scenes[i].path);
                }
            }

            return scenePaths;
        }

        private static void DrawScenePopup(SerializedProperty sceneReference, string label, IReadOnlyList<string> scenePaths, bool isOptional)
        {
            var scenePath = sceneReference.FindPropertyRelative("scenePath");
            var sceneGuid = sceneReference.FindPropertyRelative("sceneGuid");
            if (scenePaths.Count == 0)
            {
                EditorGUILayout.HelpBox("Build Settings에 선택 가능한 씬이 없습니다.", MessageType.Error);
                return;
            }

            var offset = isOptional ? 1 : 0;
            var options = new string[scenePaths.Count + offset];
            if (isOptional)
            {
                options[0] = "사용 안 함";
            }

            var selectedIndex = isOptional && string.IsNullOrWhiteSpace(scenePath.stringValue) ? 0 : -1;
            for (var i = 0; i < scenePaths.Count; i++)
            {
                options[i + offset] = GetSceneOptionLabel(scenePaths[i], scenePaths);
                if (string.Equals(scenePath.stringValue, scenePaths[i], StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i + offset;
                }
            }

            if (selectedIndex < 0)
            {
                EditorGUILayout.HelpBox($"{label}으로 지정된 씬이 활성화된 Build Settings 목록에 없습니다.", MessageType.Error);
                selectedIndex = 0;
            }

            var newIndex = EditorGUILayout.Popup(label, selectedIndex, options);
            scenePath.stringValue = isOptional && newIndex == 0 ? string.Empty : scenePaths[newIndex - offset];
            sceneGuid.stringValue = string.IsNullOrWhiteSpace(scenePath.stringValue)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(scenePath.stringValue);
        }

        private void DrawValidation()
        {
            var destinationPath = _destinationScene.FindPropertyRelative("scenePath").stringValue;
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                EditorGUILayout.HelpBox("도착 씬은 반드시 지정해야 합니다.", MessageType.Error);
                return;
            }

            if (!_useLoadingScene.boolValue)
            {
                return;
            }

            var loadingPath = _loadingScene.FindPropertyRelative("scenePath").stringValue;
            if (string.IsNullOrWhiteSpace(loadingPath))
            {
                EditorGUILayout.HelpBox("로딩 씬 경유를 선택한 경우 로딩 씬을 지정해야 합니다.", MessageType.Error);
            }
            else if (string.Equals(loadingPath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox("로딩 씬과 도착 씬은 서로 달라야 합니다.", MessageType.Error);
            }
        }

        private static string GetSceneOptionLabel(string scenePath, IReadOnlyList<string> scenePaths)
        {
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var count = 0;
            for (var i = 0; i < scenePaths.Count; i++)
            {
                if (string.Equals(sceneName, Path.GetFileNameWithoutExtension(scenePaths[i]), StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            if (count == 1)
            {
                return sceneName;
            }

            var directory = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            return string.IsNullOrEmpty(directory) ? sceneName : $"{sceneName}  ({directory})";
        }

        private static void OpenBuildSceneSettings()
        {
            if (!EditorApplication.ExecuteMenuItem("File/Build Profiles"))
            {
                EditorApplication.ExecuteMenuItem("File/Build Settings...");
            }
        }
    }
}
