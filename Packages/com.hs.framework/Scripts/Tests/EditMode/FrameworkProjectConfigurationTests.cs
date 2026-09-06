using HS.Framework.ProjectManagement;
using HS.Framework.Scene;
using HS.Framework.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>프로젝트 초기 설정, 씬 분류와 Unity Build Settings 동기화를 검증한다.</summary>
    public sealed class FrameworkProjectConfigurationTests
    {
        [Test]
        public void ConfigurationResolvesSpecialScenesAndGameplayLevels()
        {
            var clientSettings = LoadClientSettings();
            var configuration = CreateValidConfiguration(clientSettings);
            try
            {
                Assert.That(configuration.TryValidate(out var error), Is.True, error);
                Assert.That(configuration.BootstrapScene.DisplayName, Is.EqualTo("Bootstrap"));
                Assert.That(configuration.LoadingScene.DisplayName, Is.EqualTo("Loading"));
                Assert.That(configuration.MainMenuScene.DisplayName, Is.EqualTo("MainMenu"));
                Assert.That(configuration.IsUserConfigurable(ProjectUserSetting.Graphics), Is.True);
                Assert.That(configuration.IsUserConfigurable(ProjectUserSetting.Audio), Is.False);
                Assert.That(configuration.TryGetGameplayScene(1, out var gameplayScene), Is.True);
                Assert.That(gameplayScene.DisplayName, Is.EqualTo("Level1"));
                Assert.That(configuration.TryGetGameplayLevelId(gameplayScene.ScenePath, out var levelId), Is.True);
                Assert.That(levelId, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void ConfigurationRejectsDuplicateSpecialSceneCategories()
        {
            var clientSettings = LoadClientSettings();
            var configuration = FrameworkProjectConfiguration.CreateRuntime(
                clientSettings,
                ProjectUserSetting.All,
                1,
                Scene("bootstrap", ProjectSceneCategory.Bootstrap, "Assets/Scenes/Bootstrap.unity"),
                Scene("bootstrap-copy", ProjectSceneCategory.Bootstrap, "Assets/Scenes/Level2.unity"),
                Scene("loading", ProjectSceneCategory.Loading, "Assets/Scenes/Loading.unity"),
                Scene("main-menu", ProjectSceneCategory.MainMenu, "Assets/Scenes/MainMenu.unity"),
                Scene("level-1", ProjectSceneCategory.Gameplay, "Assets/Scenes/Level1.unity", 1));
            try
            {
                Assert.That(configuration.TryValidate(out var error), Is.False);
                Assert.That(error, Does.Contain("각각 정확히 하나"));
            }
            finally
            {
                Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void DefaultConfigurationIsAvailableAndMatchesRuntimeBuildSettings()
        {
            var configuration = FrameworkProjectConfiguration.Load();

            Assert.That(configuration, Is.Not.Null);
            Assert.That(IsRegisteredInBuildSettings(configuration.BootstrapScene.ScenePath), Is.True,
                $"부트스트랩 씬이 빌드 세팅에 없다: {configuration.BootstrapScene.ScenePath}");
            Assert.That(IsRegisteredInBuildSettings(configuration.LoadingScene.ScenePath), Is.True,
                $"로딩 씬이 빌드 세팅에 없다: {configuration.LoadingScene.ScenePath}");
            Assert.That(configuration.TryValidate(out var error), Is.True, error);
            Assert.That(configuration.TryValidateRuntime(out error), Is.True, error);
        }

        private static FrameworkProjectConfiguration CreateValidConfiguration(
            ClientSettingsConfiguration clientSettings)
        {
            return FrameworkProjectConfiguration.CreateRuntime(
                clientSettings,
                ProjectUserSetting.Graphics | ProjectUserSetting.Input,
                1,
                Scene("bootstrap", ProjectSceneCategory.Bootstrap, "Assets/Scenes/Bootstrap.unity"),
                Scene("loading", ProjectSceneCategory.Loading, "Assets/Scenes/Loading.unity"),
                Scene("main-menu", ProjectSceneCategory.MainMenu, "Assets/Scenes/MainMenu.unity"),
                Scene("level-1", ProjectSceneCategory.Gameplay, "Assets/Scenes/Level1.unity", 1));
        }

        /// <summary>그 경로가 빌드 세팅에 등록돼 있는지 본다.</summary>
        /// <remarks>
        /// 이 검사가 보려는 것은 설정과 빌드 세팅이 서로 맞는가이다. 경로를 코드에 적어 두면
        /// 씬이 옮겨질 때마다 낡고, 검사 이름이 말하는 것과 하는 일이 갈라진다.
        /// </remarks>
        /// <param name="scenePath">확인할 씬 경로이다.</param>
        /// <returns>빌드 세팅에 등록돼 있으면 참이다.</returns>
        private static bool IsRegisteredInBuildSettings(string scenePath)
        {
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                if (SceneUtility.GetScenePathByBuildIndex(index) == scenePath)
                {
                    return true;
                }
            }

            return false;
        }

        private static ClientSettingsConfiguration LoadClientSettings()
        {
            return FrameworkProjectConfiguration.Load().ClientSettings;
        }

        private static ProjectSceneDefinition Scene(
            string id,
            ProjectSceneCategory category,
            string path,
            int levelId = 0)
        {
            return new ProjectSceneDefinition(id, category, SceneReference.Create(path), levelId);
        }
    }
}
