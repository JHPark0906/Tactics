using System;
using System.Collections.Generic;
using HS.Framework.ProjectManagement;
using HS.Framework.Scene;
using UnityEditor;

namespace HS.Framework.Editor.ProjectManagement
{
    /// <summary>프로젝트 씬 에셋이 Framework가 요구하는 구성 요소를 포함하는지 검사한다.</summary>
    public static class FrameworkSceneContentValidator
    {
        private const string FrameworkFolderPrefix = "Packages/com.hs.framework/";

        private static readonly Dictionary<Type, string> ScriptPathCache = new();

        /// <summary>필수 컴포넌트 누락처럼 실행을 막아야 하는 문제를 수집한다.</summary>
        public static IReadOnlyList<string> CollectErrors(FrameworkProjectConfiguration configuration)
        {
            var errors = new List<string>();
            if (configuration == null)
            {
                return errors;
            }

            RequireScript(
                errors,
                configuration.LoadingScene,
                "Loading",
                typeof(LoadingSceneController),
                "FrameworkLoadingScreen 프리팹을 씬에 배치하거나 HS Loading 씬 템플릿으로 다시 생성하세요.");
            return errors;
        }

        /// <summary>권장 구조에서 벗어난 설정에 대한 경고를 수집한다.</summary>
        public static IReadOnlyList<string> CollectWarnings(FrameworkProjectConfiguration configuration)
        {
            var warnings = new List<string>();
            if (configuration == null)
            {
                return warnings;
            }

            WarnFrameworkOwnedScene(warnings, configuration.BootstrapScene, "Bootstrap");
            WarnFrameworkOwnedScene(warnings, configuration.LoadingScene, "Loading");
            return warnings;
        }

        private static void RequireScript(
            ICollection<string> errors,
            SceneReference scene,
            string categoryName,
            Type componentType,
            string guidance)
        {
            if (scene is not { IsAssigned: true })
            {
                return;
            }

            var scriptPath = FindScriptPath(componentType);
            if (string.IsNullOrEmpty(scriptPath))
            {
                return;
            }

            foreach (var dependencyPath in AssetDatabase.GetDependencies(scene.ScenePath, true))
            {
                if (string.Equals(dependencyPath, scriptPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            errors.Add(
                $"{categoryName} 씬 '{scene.ScenePath}'에 {componentType.Name} 컴포넌트가 없습니다. {guidance}");
        }

        private static void WarnFrameworkOwnedScene(
            ICollection<string> warnings,
            SceneReference scene,
            string categoryName)
        {
            if (scene is not { IsAssigned: true } ||
                !scene.ScenePath.StartsWith(FrameworkFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            warnings.Add(
                $"{categoryName} 씬이 Framework 소유 씬 '{scene.ScenePath}'을 직접 사용하고 있습니다. " +
                "Framework 업데이트와 충돌하지 않도록 Tools > HS Framework > Setup Project Scenes로 " +
                "프로젝트 소유 사본을 생성하세요.");
        }

        private static string FindScriptPath(Type componentType)
        {
            if (ScriptPathCache.TryGetValue(componentType, out var cachedPath))
            {
                return cachedPath;
            }

            var path = string.Empty;
            foreach (var guid in AssetDatabase.FindAssets($"t:MonoScript {componentType.Name}"))
            {
                var candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(candidatePath);
                if (script != null && script.GetClass() == componentType)
                {
                    path = candidatePath;
                    break;
                }
            }

            ScriptPathCache[componentType] = path;
            return path;
        }
    }
}
