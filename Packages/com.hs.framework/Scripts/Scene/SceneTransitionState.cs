using System;

namespace HS.Framework.Scene
{
    /// <summary>
    /// 씬 로딩 파이프라인의 단계를 나타낸다.
    /// </summary>
    public enum SceneLoadPhase
    {
        Idle,
        LoadingScreen,
        LoadingTarget,

        /// <summary>
        /// 목적지 씬의 오브젝트 그래프는 다 만들어졌지만, 씬 스스로 알린 초기화가 아직 끝나지 않은 구간이다.
        /// 아무도 초기화를 알리지 않으면 이 단계를 거치지 않고 곧바로 <see cref="Completed"/>로 넘어간다.
        /// </summary>
        Initializing,

        Recovering,
        Recovered,
        Completed,
        Failed
    }

    /// <summary>
    /// 씬 로딩의 현재 진행 상태이다.
    /// </summary>
    public readonly struct SceneTransitionState
    {
        /// <summary>클라이언트 수명주기 안에서 증가하는 전환 식별자이다.</summary>
        public long OperationId { get; }

        /// <summary>전환이 시작된 UTC 시각이다.</summary>
        public DateTime StartedAtUtc { get; }

        /// <summary>
        /// 현재 로딩 단계이다.
        /// </summary>
        public SceneLoadPhase Phase { get; }

        /// <summary>
        /// 0부터 1 사이의 진행률이다.
        /// </summary>
        public float Value { get; }

        /// <summary>
        /// 최종 대상 씬이다.
        /// </summary>
        public SceneReference Destination { get; }

        /// <summary>실패 후 안전하게 복귀할 씬이며 복구하지 않는 상태에서는 null이다.</summary>
        public SceneReference RecoveryScene { get; }

        /// <summary>실패한 전환의 예외이며 다른 단계에서는 null이다.</summary>
        public Exception Exception { get; }

        /// <summary>
        /// 진행 상태를 생성한다.
        /// </summary>
        public SceneTransitionState(
            long operationId,
            DateTime startedAtUtc,
            SceneLoadPhase phase,
            float value,
            SceneReference destination,
            Exception exception = null,
            SceneReference recoveryScene = null)
        {
            OperationId = operationId;
            StartedAtUtc = startedAtUtc;
            Phase = phase;
            Value = value;
            Destination = destination;
            Exception = exception;
            RecoveryScene = recoveryScene;
        }

        /// <summary>아직 전환이 시작되지 않은 초기 상태를 생성한다.</summary>
        public static SceneTransitionState Idle => new(0, default, SceneLoadPhase.Idle, 0f, null);
    }
}
