using Cysharp.Threading.Tasks;

namespace HS.Framework.Runtime
{
    /// <summary>
    /// 프로젝트 씬 카탈로그에 정의된 게임 흐름을 실행하는 고수준 계약이다.
    /// 컨텐츠 코드가 씬 카탈로그와 씬 전환 서비스를 직접 조합하지 않고 게임 플로우를 요청하게 한다.
    /// </summary>
    public interface IGameFlowService
    {
        /// <summary>기본 Gameplay 레벨 씬으로 로딩 씬을 경유해 새 게임을 시작한다.</summary>
        UniTask StartNewGameAsync();

        /// <summary>지정한 레벨 ID의 Gameplay 씬으로 로딩 씬을 경유해 이동한다.</summary>
        UniTask LoadGameplayLevelAsync(int levelId);

        /// <summary>MainMenu 씬으로 직접 복귀한다. 목적지 활성화 전까지 현재 씬을 유지해 실패 시 재시도 UI를 보존한다.</summary>
        UniTask ReturnToMainMenuAsync();
    }
}
