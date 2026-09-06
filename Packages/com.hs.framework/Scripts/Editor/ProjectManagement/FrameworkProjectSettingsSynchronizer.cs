using System;
using System.Collections.Generic;
using System.Linq;
using HS.Framework.ProjectManagement;
using HS.Framework.Scene;
using HS.Framework.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HS.Framework.Editor.ProjectManagement
{
    /// <summary>Framework 프로젝트 설정을 Unity Build Settings와 자동 동기화한다.</summary>
    [InitializeOnLoad]
    public static class FrameworkProjectSettingsSynchronizer
    {
        /// <summary>Framework가 사용하는 프로젝트 설정 에셋의 고정 경로이다.</summary>
        public const string ConfigurationAssetPath =
            "Assets/_HS/ProjectSettings/Resources/FrameworkProjectConfiguration.asset";

        static FrameworkProjectSettingsSynchronizer()
        {
            EditorApplication.projectChanged += ScheduleSynchronization;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ScheduleSynchronization();
        }

        /// <summary>기본 프로젝트 설정 에셋을 불러온다.</summary>
        public static FrameworkProjectConfiguration LoadDefaultConfiguration()
        {
            return AssetDatabase.LoadAssetAtPath<FrameworkProjectConfiguration>(ConfigurationAssetPath);
        }

        /// <summary>새 프로젝트에 Framework 기본 설정과 특수 씬 항목을 생성한다.</summary>
        public static FrameworkProjectConfiguration CreateDefaultConfiguration()
        {
            var existing = LoadDefaultConfiguration();
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets", "_HS");
            EnsureFolder("Assets/_HS", "ProjectSettings");
            EnsureFolder("Assets/_HS/ProjectSettings", "Resources");
            var clientSettings = AssetDatabase.LoadAssetAtPath<ClientSettingsConfiguration>(
                "Packages/com.hs.framework/Settings/DefaultClientSettingsConfiguration.asset");
            var bootstrapScenePath = FrameworkProjectSceneProvisioner.EnsureProjectSceneAsset(
                FrameworkSceneTemplates.BootstrapTemplateScenePath, "Bootstrap")
                ?? FrameworkSceneTemplates.BootstrapTemplateScenePath;
            var loadingScenePath = FrameworkProjectSceneProvisioner.EnsureProjectSceneAsset(
                FrameworkSceneTemplates.LoadingTemplateScenePath, "Loading")
                ?? FrameworkSceneTemplates.LoadingTemplateScenePath;
            var configuration = FrameworkProjectConfiguration.CreateRuntime(
                clientSettings,
                ProjectUserSetting.All,
                1,
                new ProjectSceneDefinition(
                    "bootstrap",
                    ProjectSceneCategory.Bootstrap,
                    SceneReference.Create(bootstrapScenePath)),
                new ProjectSceneDefinition(
                    "loading",
                    ProjectSceneCategory.Loading,
                    SceneReference.Create(loadingScenePath)));
            AssetDatabase.CreateAsset(configuration, ConfigurationAssetPath);
            AssetDatabase.SaveAssets();
            return configuration;
        }

        /// <summary>기본 설정의 씬 이동 정보를 갱신하고 Build Settings를 동기화한다.</summary>
        [MenuItem("Tools/HS Framework/Synchronize Project Settings")]
        public static void SynchronizeDefaultConfiguration()
        {
            var configuration = LoadDefaultConfiguration();
            if (configuration != null)
            {
                TrySynchronize(configuration, false, out _);
            }
        }

        private static void ScheduleSynchronization()
        {
            EditorApplication.delayCall -= SynchronizeDefaultConfiguration;
            EditorApplication.delayCall += SynchronizeDefaultConfiguration;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            var configuration = LoadDefaultConfiguration();
            if (!TrySynchronize(configuration, false, out _))
            {
                EditorApplication.isPlaying = false;
                return;
            }

            foreach (var contentError in FrameworkSceneContentValidator.CollectErrors(configuration))
            {
                Debug.LogError($"[HS Framework Project Settings] {contentError}");
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>설정 에셋을 검증하고 Unity Build Settings를 같은 순서와 활성 상태로 갱신한다.</summary>
        public static bool TrySynchronize(
            FrameworkProjectConfiguration configuration,
            bool throwOnFailure,
            out string error)
        {
            if (configuration == null)
            {
                return Fail("Framework 프로젝트 설정 에셋을 찾을 수 없습니다.", throwOnFailure, out error);
            }

            SynchronizeSceneReferences(configuration);
            if (!configuration.TryValidate(out error))
            {
                return Fail(error, throwOnFailure, out error);
            }

            foreach (var definition in configuration.Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.Scene.ScenePath) == null)
                {
                    return Fail(
                        $"씬 '{definition.SceneId}'의 에셋을 찾을 수 없습니다: {definition.Scene.ScenePath}",
                        throwOnFailure,
                        out error);
                }
            }

            var orderedScenes = configuration.Scenes
                .OrderBy(GetCategoryOrder)
                .ThenBy(definition => definition.Category == ProjectSceneCategory.Gameplay
                    ? definition.GameplayLevelId
                    : 0)
                .ThenBy(definition => definition.SceneId, StringComparer.Ordinal)
                .Select(definition => new EditorBuildSettingsScene(
                    definition.Scene.ScenePath,
                    definition.ShouldEnableInBuild()))
                .ToArray();

            if (!BuildSettingsEqual(EditorBuildSettings.scenes, orderedScenes))
            {
                EditorBuildSettings.scenes = orderedScenes;
            }

            error = null;
            return true;
        }

        private static void SynchronizeSceneReferences(FrameworkProjectConfiguration configuration)
        {
            var serializedObject = new SerializedObject(configuration);
            var scenesProperty = serializedObject.FindProperty("scenes");
            var changed = false;
            for (var index = 0; index < scenesProperty.arraySize; index++)
            {
                var sceneProperty = scenesProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("scene");
                var pathProperty = sceneProperty.FindPropertyRelative("scenePath");
                var guidProperty = sceneProperty.FindPropertyRelative("sceneGuid");
                var path = pathProperty.stringValue;
                var guid = guidProperty.stringValue;

                if (!string.IsNullOrWhiteSpace(guid))
                {
                    var movedPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrWhiteSpace(movedPath) &&
                        !string.Equals(path, movedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        pathProperty.stringValue = movedPath;
                        changed = true;
                    }
                    else if (string.IsNullOrWhiteSpace(movedPath) &&
                             AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    {
                        guidProperty.stringValue = AssetDatabase.AssetPathToGUID(path);
                        changed = true;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(path))
                {
                    var resolvedGuid = AssetDatabase.AssetPathToGUID(path);
                    if (!string.IsNullOrWhiteSpace(resolvedGuid))
                    {
                        guidProperty.stringValue = resolvedGuid;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(configuration);
            }
        }

        private static int GetCategoryOrder(ProjectSceneDefinition definition)
        {
            return definition.Category switch
            {
                ProjectSceneCategory.Bootstrap => 0,
                ProjectSceneCategory.Loading => 1,
                ProjectSceneCategory.MainMenu => 2,
                ProjectSceneCategory.Gameplay => 3,
                ProjectSceneCategory.General => 4,
                ProjectSceneCategory.Test => 5,
                _ => int.MaxValue
            };
        }

        private static bool BuildSettingsEqual(
            IReadOnlyList<EditorBuildSettingsScene> current,
            IReadOnlyList<EditorBuildSettingsScene> expected)
        {
            if (current.Count != expected.Count)
            {
                return false;
            }

            for (var index = 0; index < current.Count; index++)
            {
                if (current[index].enabled != expected[index].enabled ||
                    !string.Equals(current[index].path, expected[index].path, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Fail(string message, bool throwOnFailure, out string error)
        {
            error = message;
            if (throwOnFailure)
            {
                throw new BuildFailedException(message);
            }

            Debug.LogError($"[HS Framework Project Settings] {message}");
            return false;
        }

        private static void EnsureFolder(string parentFolder, string folderName)
        {
            var path = $"{parentFolder}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
    }

    /// <summary>빌드 직전에 Framework 설정과 Build Settings의 일치를 보장한다.</summary>
    public sealed class FrameworkProjectBuildValidator : IPreprocessBuildWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => -1000;

        /// <inheritdoc />
        public void OnPreprocessBuild(BuildReport report)
        {
            var configuration = FrameworkProjectSettingsSynchronizer.LoadDefaultConfiguration();
            FrameworkProjectSettingsSynchronizer.TrySynchronize(configuration, true, out _);
            var contentErrors = FrameworkSceneContentValidator.CollectErrors(configuration);
            if (contentErrors.Count > 0)
            {
                throw new BuildFailedException(string.Join("\n", contentErrors));
            }
        }
    }

    /// <summary>설정 에셋이나 씬 이동 후 Build Settings 동기화를 예약한다.</summary>
    public sealed class FrameworkProjectAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsRelevantAsset(importedAssets) ||
                ContainsRelevantAsset(deletedAssets) ||
                ContainsRelevantAsset(movedAssets) ||
                ContainsRelevantAsset(movedFromAssetPaths))
            {
                EditorApplication.delayCall -=
                    FrameworkProjectSettingsSynchronizer.SynchronizeDefaultConfiguration;
                EditorApplication.delayCall +=
                    FrameworkProjectSettingsSynchronizer.SynchronizeDefaultConfiguration;
            }
        }

        private static bool ContainsRelevantAsset(IEnumerable<string> paths)
        {
            return paths.Any(path =>
                string.Equals(
                    path,
                    FrameworkProjectSettingsSynchronizer.ConfigurationAssetPath,
                    StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
        }
    }
}
