using System;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Framework.Scene
{
    /// <summary>
    /// Build Settings의 씬을 검증하고 Unity SceneManager의 단일 씬 로드를 실행한다.
    /// </summary>
    public sealed class SceneLoader : ISceneLoader
    {
        private bool _isLoading;

        /// <summary>
        /// 씬 로딩이 진행 중인지 여부이다.
        /// </summary>
        public bool IsLoading => _isLoading;

        /// <summary>
        /// 레거시 문자열 씬 이름이 비어 있지 않은지 확인한다.
        /// </summary>
        public static bool IsSceneNameValid(string sceneName) => !string.IsNullOrWhiteSpace(sceneName);

        /// <summary>
        /// 레거시 문자열 씬 이름이 Build Settings에 있는지 확인한다.
        /// </summary>
        public static bool IsSceneInBuildSettings(string sceneName, bool requireEnabled = true)
        {
            return TryGetBuildIndex(sceneName, requireEnabled, out _);
        }

        /// <summary>
        /// 레거시 문자열 씬 이름을 로드할 수 있는지 확인한다.
        /// </summary>
        public static bool CanLoadScene(string sceneName) => TryGetBuildIndex(sceneName, true, out _);

        /// <summary>
        /// 씬 참조를 로드할 수 있는지 확인한다.
        /// </summary>
        public static bool CanLoadScene(SceneReference scene) => TryGetBuildIndex(scene, out _);

        /// <summary>
        /// 전체 경로 씬 참조가 가리키는 빌드 인덱스를 찾는다.
        /// </summary>
        public static bool TryGetBuildIndex(SceneReference scene, out int buildIndex)
        {
            return TryGetBuildIndex(scene?.ScenePath, true, out buildIndex, true);
        }

        /// <summary>
        /// 지정한 씬을 단독으로 비동기 로드한다.
        /// </summary>
        public async UniTask LoadSceneAsync(SceneReference scene, Action<float> progress = null)
        {
            ThrowIfSceneCannotBeLoaded(scene);
            if (_isLoading)
            {
                throw new InvalidOperationException("A scene load is already in progress.");
            }

            _isLoading = true;
            try
            {
                await LoadSceneOperationAsync(scene, progress);
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// 레거시 문자열 씬 이름을 검사한다.
        /// </summary>
        public static void ThrowIfSceneCannotBeLoaded(string sceneName)
        {
            if (!TryGetBuildIndex(sceneName, true, out _))
            {
                throw new InvalidOperationException($"Scene '{sceneName}' is not enabled in Build Settings or is ambiguous.");
            }
        }

        /// <summary>
        /// 씬 참조를 검사한다.
        /// </summary>
        public static void ThrowIfSceneCannotBeLoaded(SceneReference scene)
        {
            if (scene == null || !scene.IsAssigned || !TryGetBuildIndex(scene, out _))
            {
                throw new InvalidOperationException($"Scene '{scene?.DisplayName ?? "(empty)"}' is not enabled in Build Settings.");
            }
        }

        /// <summary>
        /// 경로를 Unity 형식으로 정규화한다.
        /// </summary>
        public static string NormalizeScenePath(string scenePath)
        {
            return string.IsNullOrWhiteSpace(scenePath) ? string.Empty : scenePath.Trim().Replace('\\', '/');
        }

        /// <summary>
        /// 경로에서 표시용 파일 이름을 가져온다.
        /// </summary>
        public static string GetSceneDisplayName(string scenePath)
        {
            var path = NormalizeScenePath(scenePath);
            var slash = path.LastIndexOf('/');
            var name = slash >= 0 ? path.Substring(slash + 1) : path;
            return name.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 6) : name;
        }

        private static async UniTask LoadSceneOperationAsync(SceneReference scene, Action<float> progress)
        {
            TryGetBuildIndex(scene, out var buildIndex);
            var operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException($"Scene '{scene.DisplayName}' could not be loaded.");
            }

            while (!operation.isDone)
            {
                progress?.Invoke(operation.progress / 0.9f);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            progress?.Invoke(1f);
        }

        private static bool TryGetBuildIndex(string sceneName, bool requireEnabled, out int buildIndex, bool requireExactPath = false)
        {
            buildIndex = -1;
            if (!IsSceneNameValid(sceneName))
            {
                return false;
            }

            var normalized = NormalizeScenePath(sceneName);
            var matches = 0;
#if UNITY_EDITOR
            var scenes = EditorBuildSettings.scenes;
            var enabledBuildIndex = -1;
            for (var i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                if (scene.enabled)
                {
                    enabledBuildIndex++;
                }

                if (!Matches(normalized, scene.path, requireExactPath) || requireEnabled && !scene.enabled)
                {
                    continue;
                }

                buildIndex = scene.enabled ? enabledBuildIndex : -1;
                matches++;
            }
#else
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                if (!Matches(normalized, SceneUtility.GetScenePathByBuildIndex(i), requireExactPath))
                {
                    continue;
                }

                buildIndex = i;
                matches++;
            }
#endif
            return matches == 1;
        }

        private static bool Matches(string requested, string candidate, bool requireExactPath)
        {
            var path = NormalizeScenePath(candidate);
            if (string.Equals(requested, path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !requireExactPath
                   && string.Equals(GetSceneDisplayName(requested), GetSceneDisplayName(path), StringComparison.OrdinalIgnoreCase);
        }
    }
}
