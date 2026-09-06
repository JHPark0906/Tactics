using System;
using Cysharp.Threading.Tasks;
using R3;

namespace HS.Framework.Scene
{
    /// <summary>클라이언트 씬 전환을 요청하는 기능 계층 계약이다.</summary>
    public interface ISceneTransitionService
    {
        /// <summary>현재 씬 전환이 진행 중인지 여부이다.</summary>
        bool IsLoading { get; }

        /// <summary>가장 최근 씬 전환 상태를 즉시 전달하는 스트림이다.</summary>
        Observable<SceneTransitionState> State { get; }

        /// <summary>지정한 씬을 단독으로 로드한다.</summary>
        UniTask LoadSceneAsync(SceneReference scene);

        /// <summary>로딩 씬을 거쳐 목적지 씬을 로드한다.</summary>
        UniTask LoadSceneAfterLoadingSceneAsync(SceneReference loadingScene, SceneReference destination);

        /// <summary>
        /// 목적지 씬 스스로 진행 중인 초기화의 진행률을 보고한다.
        /// </summary>
        /// <remarks>
        /// 오브젝트 그래프가 다 만들어진 뒤에도 더 걸리는 초기화(예: 런타임 생성)가 있는 씬만 부르면 된다.
        /// 전환이 진행 중이 아닐 때의 호출은 조용히 무시된다. 한 번이라도 호출되면 전환은 그 값이
        /// 1 이상이 될 때까지 완료를 미룬다 — 아무도 부르지 않는 씬은 이 지연이 전혀 없다.
        /// </remarks>
        /// <param name="progress">0에서 1 사이의 진행률이다. 1 이상이면 초기화가 끝났다는 뜻이다.</param>
        void ReportInitializationProgress(float progress);
    }

    /// <summary>Unity SceneManager를 사용해 단일 씬을 로드하는 낮은 수준의 계약이다.</summary>
    public interface ISceneLoader
    {
        /// <summary>현재 단일 씬 로드가 진행 중인지 여부이다.</summary>
        bool IsLoading { get; }

        /// <summary>지정한 씬을 로드하고 진행률을 보고한다.</summary>
        UniTask LoadSceneAsync(SceneReference scene, Action<float> progress = null);
    }
}
