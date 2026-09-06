using System;
using System.Collections.Generic;
using HS.Framework.ProjectManagement;
using HS.Framework.Settings;
using UnityEditor;
using UnityEngine;

namespace HS.Framework.Editor.ProjectManagement
{
    /// <summary>Unity Project Settings에서 Framework 초기 설정과 씬 목록을 편집한다.</summary>
    public sealed class FrameworkProjectSettingsProvider : SettingsProvider
    {
        private FrameworkProjectConfiguration _configuration;
        private SerializedObject _serializedConfiguration;

        private FrameworkProjectSettingsProvider(string path, SettingsScope scope)
            : base(path, scope, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "HS", "Framework", "Project", "Scene", "Bootstrap", "Loading", "Settings"
            })
        {
        }

        /// <summary>HS Framework 프로젝트 설정 페이지를 등록한다.</summary>
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new FrameworkProjectSettingsProvider("Project/HS Framework", SettingsScope.Project);
        }

        /// <summary>메뉴에서 Framework 프로젝트 설정을 연다.</summary>
        [MenuItem("Tools/HS Framework/Open Project Settings")]
        public static void OpenProjectSettings()
        {
            SettingsService.OpenProjectSettings("Project/HS Framework");
        }

        /// <inheritdoc />
        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            ReloadConfiguration();
        }

        /// <inheritdoc />
        public override void OnGUI(string searchContext)
        {
            if (_configuration == null)
            {
                EditorGUILayout.HelpBox(
                    $"프로젝트 설정 에셋을 찾을 수 없습니다.\n{FrameworkProjectSettingsSynchronizer.ConfigurationAssetPath}",
                    MessageType.Error);
                if (GUILayout.Button("설정 에셋 다시 찾기"))
                {
                    ReloadConfiguration();
                }

                return;
            }

            _serializedConfiguration.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Application Initial Settings", EditorStyles.boldLabel);
            var clientSettingsProperty = _serializedConfiguration.FindProperty("clientSettings");
            EditorGUILayout.PropertyField(clientSettingsProperty, new GUIContent("초기 클라이언트 설정"));
            DrawClientSettings(clientSettingsProperty.objectReferenceValue as ClientSettingsConfiguration);
            EditorGUILayout.PropertyField(
                _serializedConfiguration.FindProperty("userConfigurableSettings"),
                new GUIContent("플레이어 변경 가능 설정"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Project Scenes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _serializedConfiguration.FindProperty("defaultGameplayLevelId"),
                new GUIContent("기본 Gameplay 레벨 ID"));
            EditorGUILayout.PropertyField(
                _serializedConfiguration.FindProperty("scenes"),
                new GUIContent("분류된 씬 목록"),
                true);

            var changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                _serializedConfiguration.ApplyModifiedProperties();
                EditorUtility.SetDirty(_configuration);
                FrameworkProjectSettingsSynchronizer.TrySynchronize(_configuration, false, out _);
            }

            EditorGUILayout.Space();
            DrawValidation();
            if (GUILayout.Button("프로젝트 씬 생성/복구 (Setup Project Scenes)"))
            {
                FrameworkProjectSceneProvisioner.SetupProjectScenes();
                ReloadConfiguration();
            }

            if (GUILayout.Button("Unity Build Settings와 지금 동기화"))
            {
                FrameworkProjectSettingsSynchronizer.TrySynchronize(_configuration, false, out _);
            }
        }

        private static void DrawClientSettings(ClientSettingsConfiguration settings)
        {
            if (settings == null)
            {
                return;
            }

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("inputActionAsset"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("displaySettingsPreset"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("audioSettingsPreset"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("localeSettingsPreset"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawValidation()
        {
            var contentErrors = FrameworkSceneContentValidator.CollectErrors(_configuration);
            var contentWarnings = FrameworkSceneContentValidator.CollectWarnings(_configuration);
            if (_configuration.TryValidate(out var error))
            {
                if (contentErrors.Count == 0 && contentWarnings.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "설정이 유효합니다. 씬 목록은 Unity Build Settings에 자동 반영됩니다.",
                        MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            foreach (var contentError in contentErrors)
            {
                EditorGUILayout.HelpBox(contentError, MessageType.Error);
            }

            foreach (var contentWarning in contentWarnings)
            {
                EditorGUILayout.HelpBox(contentWarning, MessageType.Warning);
            }
        }

        private void ReloadConfiguration()
        {
            _configuration = FrameworkProjectSettingsSynchronizer.LoadDefaultConfiguration();
            _configuration ??= FrameworkProjectSettingsSynchronizer.CreateDefaultConfiguration();
            _serializedConfiguration = _configuration == null ? null : new SerializedObject(_configuration);
        }
    }
}
