using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HS.Framework.ProjectManagement;
using HS.Framework.Scene;
using HS.Framework.Settings;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace HS.Framework.Runtime
{
    /// <summary>프레임워크 설정과 최초 씬 전환을 LifetimeScope 수명으로 초기화한다.</summary>
    public sealed class FrameworkInitializer : IAsyncStartable, IDisposable
    {
        private readonly FrameworkProjectConfiguration _projectConfiguration;
        private readonly IObjectResolver _resolver;
        private readonly ISceneTransitionService _sceneTransitionService;

        public FrameworkInitializer(
            FrameworkProjectConfiguration projectConfiguration,
            IObjectResolver resolver,
            ISceneTransitionService sceneTransitionService)
        {
            _projectConfiguration = projectConfiguration ?? throw new ArgumentNullException(nameof(projectConfiguration));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _sceneTransitionService = sceneTransitionService ?? throw new ArgumentNullException(nameof(sceneTransitionService));
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            InjectScene(SceneManager.GetActiveScene());

            var clientSettings = _projectConfiguration.ClientSettings;
            DisplaySettingsService.Initialize(clientSettings.DisplaySettingsPreset);
            AudioSettingsService.Initialize(clientSettings.AudioSettingsPreset);
            InputSettingsService.Initialize(clientSettings.InputActionAsset);

            await LocaleSettingsService.InitializeAsync(clientSettings.LocaleSettingsPreset);
            cancellation.ThrowIfCancellationRequested();
            await _sceneTransitionService.LoadSceneAfterLoadingSceneAsync(
                _projectConfiguration.LoadingScene,
                _projectConfiguration.MainMenuScene);
        }

        public void Dispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            InjectScene(scene);
        }

        /// <summary>
        /// 씬의 최상위 오브젝트마다 VContainer의 <see cref="ObjectResolverUnityExtensions.InjectGameObject"/>로
        /// 계층 전체(비활성 포함)에 주입한다. 스크립트가 빠진 슬롯은 그 확장이 걸러 준다.
        /// </summary>
        private void InjectScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                _resolver.InjectGameObject(rootGameObject);
            }
        }
    }
}
