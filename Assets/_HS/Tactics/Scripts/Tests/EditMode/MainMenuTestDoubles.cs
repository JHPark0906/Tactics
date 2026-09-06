using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Framework.Scene;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 메인 메뉴 ViewModel 검사들이 함께 쓰는 게임 흐름 서비스 대역이다.
    /// 새 게임 요청과 스테이지 요청을 따로 세고, 각각을 붙들어 두거나 실패시킬 수 있다.
    /// </summary>
    internal sealed class FakeGameFlowService : IGameFlowService
    {
        /// <summary>새 게임 요청 횟수이다.</summary>
        public int StartCount { get; private set; }

        /// <summary>스테이지 요청으로 받은 레벨 식별자를 순서대로 담는다.</summary>
        public List<int> LoadedLevelIds { get; } = new();

        /// <summary>지정하면 새 게임 요청이 이것이 끝날 때까지 기다린다.</summary>
        public UniTaskCompletionSource PendingStart { get; set; }

        /// <summary>지정하면 스테이지 요청이 이것이 끝날 때까지 기다린다.</summary>
        public UniTaskCompletionSource PendingLoad { get; set; }

        /// <summary>새 게임 요청을 실패시킬지 여부이다.</summary>
        public bool ThrowOnStart { get; set; }

        /// <summary>스테이지 요청을 실패시킬지 여부이다.</summary>
        public bool ThrowOnLoad { get; set; }

        /// <summary>메인 메뉴 복귀 요청 횟수이다.</summary>
        public int ReturnToMainMenuCount { get; private set; }

        /// <summary>지정하면 메인 메뉴 복귀 요청이 이것이 끝날 때까지 기다린다.</summary>
        public UniTaskCompletionSource PendingReturn { get; set; }

        /// <summary>메인 메뉴 복귀 요청을 실패시킬지 여부이다.</summary>
        public bool ThrowOnReturn { get; set; }

        /// <summary>메인 메뉴 복귀 요청이 들어오는 순간 부른다. 떠나기 전에 무엇이 끝나 있어야 하는지를 재는 검사가 쓴다.</summary>
        public Action BeforeReturnToMainMenu { get; set; }

        /// <inheritdoc />
        public UniTask StartNewGameAsync()
        {
            StartCount++;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("시험용 전환 실패이다.");
            }

            return PendingStart?.Task ?? UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask LoadGameplayLevelAsync(int levelId)
        {
            LoadedLevelIds.Add(levelId);
            if (ThrowOnLoad)
            {
                throw new InvalidOperationException("시험용 스테이지 전환 실패이다.");
            }

            return PendingLoad?.Task ?? UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask ReturnToMainMenuAsync()
        {
            ReturnToMainMenuCount++;
            BeforeReturnToMainMenu?.Invoke();
            if (ThrowOnReturn)
            {
                throw new InvalidOperationException("시험용 복귀 실패이다.");
            }

            return PendingReturn?.Task ?? UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 주어진 씬 정의 목록을 그대로 내놓는 씬 카탈로그 대역이다.
    /// 게임플레이 씬은 <see cref="Gameplay"/>, 그 밖의 씬은 <see cref="MainMenu"/>로 만들어 넘긴다.
    /// </summary>
    internal sealed class FakeSceneCatalog : IProjectSceneCatalog
    {
        private readonly ProjectSceneDefinition[] _scenes;

        /// <summary>씬 정의 목록과 새 게임의 시작 레벨을 받아 카탈로그를 만든다.</summary>
        /// <param name="defaultGameplayLevelId">새 게임이 시작하는 레벨 식별자이다.</param>
        /// <param name="scenes">카탈로그가 내놓을 씬 정의이며, 순서를 그대로 지킨다.</param>
        public FakeSceneCatalog(int defaultGameplayLevelId, params ProjectSceneDefinition[] scenes)
        {
            DefaultGameplayLevelId = defaultGameplayLevelId;
            _scenes = scenes ?? Array.Empty<ProjectSceneDefinition>();
        }

        /// <summary>지정한 레벨 식별자를 가진 게임플레이 씬 정의를 만든다.</summary>
        /// <param name="levelId">레벨 식별자이다.</param>
        /// <returns>만든 씬 정의이다.</returns>
        public static ProjectSceneDefinition Gameplay(int levelId)
        {
            return new ProjectSceneDefinition(
                $"level-{levelId}",
                ProjectSceneCategory.Gameplay,
                SceneReference.Create($"Assets/Scenes/Level{levelId}.unity"),
                levelId);
        }

        /// <summary>게임플레이가 아닌 메인 메뉴 씬 정의를 만든다.</summary>
        /// <returns>만든 씬 정의이다.</returns>
        public static ProjectSceneDefinition MainMenu()
        {
            return new ProjectSceneDefinition(
                "main-menu",
                ProjectSceneCategory.MainMenu,
                SceneReference.Create("Assets/Scenes/MainMenu.unity"));
        }

        /// <inheritdoc />
        public IReadOnlyList<ProjectSceneDefinition> Scenes => _scenes;

        /// <inheritdoc />
        public int DefaultGameplayLevelId { get; }

        /// <inheritdoc />
        public SceneReference BootstrapScene { get; } = SceneReference.Create("Assets/Scenes/Bootstrap.unity");

        /// <inheritdoc />
        public SceneReference LoadingScene { get; } = SceneReference.Create("Assets/Scenes/Loading.unity");

        /// <inheritdoc />
        public SceneReference MainMenuScene { get; } = SceneReference.Create("Assets/Scenes/MainMenu.unity");

        /// <inheritdoc />
        public bool TryGetGameplayScene(int levelId, out SceneReference scene)
        {
            foreach (var definition in _scenes)
            {
                if (definition.Category == ProjectSceneCategory.Gameplay && definition.GameplayLevelId == levelId)
                {
                    scene = definition.Scene;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryGetGameplayLevelId(string scenePath, out int levelId)
        {
            foreach (var definition in _scenes)
            {
                if (definition.Category == ProjectSceneCategory.Gameplay &&
                    string.Equals(definition.Scene.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    levelId = definition.GameplayLevelId;
                    return true;
                }
            }

            levelId = 0;
            return false;
        }
    }
}
