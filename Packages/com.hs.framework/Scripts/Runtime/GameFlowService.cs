using System;
using Cysharp.Threading.Tasks;
using HS.Framework.ProjectManagement;
using HS.Framework.Scene;

namespace HS.Framework.Runtime
{
    /// <summary>
    /// 프로젝트 씬 카탈로그와 씬 전환 서비스를 조합해 게임 흐름 요청을 실제 씬 전환으로 변환한다.
    /// </summary>
    public sealed class GameFlowService : IGameFlowService
    {
        private readonly IProjectSceneCatalog _sceneCatalog;
        private readonly ISceneTransitionService _sceneTransitionService;

        /// <summary>지정한 씬 카탈로그와 씬 전환 서비스를 사용하는 게임 플로우 서비스를 생성한다.</summary>
        public GameFlowService(IProjectSceneCatalog sceneCatalog, ISceneTransitionService sceneTransitionService)
        {
            _sceneCatalog = sceneCatalog ?? throw new ArgumentNullException(nameof(sceneCatalog));
            _sceneTransitionService = sceneTransitionService ??
                throw new ArgumentNullException(nameof(sceneTransitionService));
        }

        /// <inheritdoc />
        public UniTask StartNewGameAsync()
        {
            return LoadGameplayLevelAsync(_sceneCatalog.DefaultGameplayLevelId);
        }

        /// <inheritdoc />
        public UniTask LoadGameplayLevelAsync(int levelId)
        {
            if (!_sceneCatalog.TryGetGameplayScene(levelId, out var destination))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levelId),
                    levelId,
                    $"레벨 ID {levelId}에 연결된 Gameplay 씬이 프로젝트 씬 카탈로그에 등록되어 있지 않습니다.");
            }

            return _sceneTransitionService.LoadSceneAfterLoadingSceneAsync(_sceneCatalog.LoadingScene, destination);
        }

        /// <inheritdoc />
        /// <remarks>로딩 씬을 경유하지 않으므로 목적지 로드 실패 시 현재 씬의 결과 창으로 돌아갈 수 있다.</remarks>
        public UniTask ReturnToMainMenuAsync()
        {
            // 목적지가 준비되기 전에 결과 창을 가진 씬을 버리지 않는다.
            // 메인 메뉴 로드가 실패하면 현재 창에서 다시 시도할 수 있어야 한다.
            return _sceneTransitionService.LoadSceneAsync(_sceneCatalog.MainMenuScene);
        }
    }
}
