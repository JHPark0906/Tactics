using System;
using HS.Framework.ProjectManagement;
using UnityEditor;
using UnityEngine;

namespace HS.Framework.Editor.ProjectManagement
{
    /// <summary>씬 템플릿에서 프로젝트 소유 Bootstrap과 Loading 씬을 생성하고 설정에 등록한다.</summary>
    public static class FrameworkProjectSceneProvisioner
    {
        /// <summary>프로젝트 소유 씬이 생성되는 폴더이다.</summary>
        public const string ProjectSceneFolder = "Assets/Scenes";

        private const string FrameworkFolderPrefix = "Packages/com.hs.framework/";

        /// <summary>프로젝트 씬 사본을 만들고 설정과 Build Settings까지 한 번에 맞춘다.</summary>
        [MenuItem("Tools/HS Framework/Setup Project Scenes")]
        public static void SetupProjectScenes()
        {
            var configuration = FrameworkProjectSettingsSynchronizer.LoadDefaultConfiguration();
            configuration ??= FrameworkProjectSettingsSynchronizer.CreateDefaultConfiguration();
            if (configuration == null)
            {
                Debug.LogError("[HS Framework Setup] 프로젝트 설정 에셋을 만들 수 없습니다.");
                return;
            }

            var changed = EnsureProjectScene(
                configuration,
                ProjectSceneCategory.Bootstrap,
                "bootstrap",
                FrameworkSceneTemplates.BootstrapTemplateScenePath,
                "Bootstrap");
            changed |= EnsureProjectScene(
                configuration,
                ProjectSceneCategory.Loading,
                "loading",
                FrameworkSceneTemplates.LoadingTemplateScenePath,
                "Loading");
            if (changed)
            {
                EditorUtility.SetDirty(configuration);
                AssetDatabase.SaveAssets();
            }

            FrameworkProjectSettingsSynchronizer.TrySynchronize(configuration, false, out _);
            foreach (var warning in FrameworkSceneContentValidator.CollectWarnings(configuration))
            {
                Debug.LogWarning($"[HS Framework Setup] {warning}");
            }

            foreach (var error in FrameworkSceneContentValidator.CollectErrors(configuration))
            {
                Debug.LogError($"[HS Framework Setup] {error}");
            }

            Debug.Log(
                "[HS Framework Setup] 프로젝트 씬 구성이 완료되었습니다. " +
                $"Bootstrap: {configuration.BootstrapScene?.ScenePath}, Loading: {configuration.LoadingScene?.ScenePath}");
        }

        /// <summary>템플릿 씬의 프로젝트 사본이 없으면 복사해서 만들고 경로를 반환한다.</summary>
        public static string EnsureProjectSceneAsset(string templateScenePath, string sceneName)
        {
            if (!AssetDatabase.IsValidFolder(ProjectSceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var targetPath = $"{ProjectSceneFolder}/{sceneName}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
            {
                return targetPath;
            }

            if (!AssetDatabase.CopyAsset(templateScenePath, targetPath))
            {
                Debug.LogError($"[HS Framework Setup] 씬 템플릿을 복사할 수 없습니다: {templateScenePath} -> {targetPath}");
                return null;
            }

            return targetPath;
        }

        private static bool EnsureProjectScene(
            FrameworkProjectConfiguration configuration,
            ProjectSceneCategory category,
            string sceneId,
            string templateScenePath,
            string sceneName)
        {
            var currentScene = FindCategoryScenePath(configuration, category);
            if (!string.IsNullOrEmpty(currentScene) &&
                !currentScene.StartsWith(FrameworkFolderPrefix, StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(currentScene) != null)
            {
                return false;
            }

            var targetPath = EnsureProjectSceneAsset(templateScenePath, sceneName);
            if (targetPath == null)
            {
                return false;
            }

            return AssignCategoryScene(configuration, category, sceneId, targetPath);
        }

        private static string FindCategoryScenePath(
            FrameworkProjectConfiguration configuration,
            ProjectSceneCategory category)
        {
            foreach (var definition in configuration.Scenes)
            {
                if (definition?.Category == category)
                {
                    return definition.Scene?.ScenePath;
                }
            }

            return null;
        }

        private static bool AssignCategoryScene(
            FrameworkProjectConfiguration configuration,
            ProjectSceneCategory category,
            string sceneId,
            string scenePath)
        {
            var serializedConfiguration = new SerializedObject(configuration);
            var scenesProperty = serializedConfiguration.FindProperty("scenes");
            var elementIndex = -1;
            for (var index = 0; index < scenesProperty.arraySize; index++)
            {
                var categoryProperty = scenesProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("category");
                if (categoryProperty.enumValueIndex == (int)category)
                {
                    elementIndex = index;
                    break;
                }
            }

            if (elementIndex < 0)
            {
                elementIndex = scenesProperty.arraySize;
                scenesProperty.InsertArrayElementAtIndex(elementIndex);
                var newElement = scenesProperty.GetArrayElementAtIndex(elementIndex);
                newElement.FindPropertyRelative("sceneId").stringValue = sceneId;
                newElement.FindPropertyRelative("category").enumValueIndex = (int)category;
                newElement.FindPropertyRelative("gameplayLevelId").intValue = 0;
                newElement.FindPropertyRelative("includeInBuild").boolValue = true;
            }

            var sceneProperty = scenesProperty.GetArrayElementAtIndex(elementIndex)
                .FindPropertyRelative("scene");
            sceneProperty.FindPropertyRelative("scenePath").stringValue = scenePath;
            sceneProperty.FindPropertyRelative("sceneGuid").stringValue = AssetDatabase.AssetPathToGUID(scenePath);
            serializedConfiguration.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
