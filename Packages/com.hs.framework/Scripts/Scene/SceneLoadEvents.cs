using System;

namespace HS.Framework.Scene
{
    /// <summary>
    /// 씬 로딩이 시작되었음을 알리는 메시지이다.
    /// </summary>
    public readonly struct SceneLoadStartedEvent
    {
        /// <summary>
        /// 최종으로 로드할 대상 씬이다.
        /// </summary>
        public SceneReference Destination { get; }

        /// <summary>
        /// 로딩 씬을 경유하는지 여부이다.
        /// </summary>
        public bool UsesLoadingScene { get; }

        /// <summary>
        /// 씬 로딩 시작 메시지를 생성한다.
        /// </summary>
        public SceneLoadStartedEvent(SceneReference destination, bool usesLoadingScene)
        {
            Destination = destination;
            UsesLoadingScene = usesLoadingScene;
        }
    }

    /// <summary>
    /// 씬 로딩이 완료되었음을 알리는 메시지이다.
    /// </summary>
    public readonly struct SceneLoadCompletedEvent
    {
        /// <summary>
        /// 로드가 완료된 대상 씬이다.
        /// </summary>
        public SceneReference Destination { get; }

        /// <summary>
        /// 씬 로딩 완료 메시지를 생성한다.
        /// </summary>
        public SceneLoadCompletedEvent(SceneReference destination)
        {
            Destination = destination;
        }
    }

    /// <summary>
    /// 씬 로딩이 실패했음을 알리는 메시지이다.
    /// </summary>
    public readonly struct SceneLoadFailedEvent
    {
        /// <summary>
        /// 로드하려던 대상 씬이다.
        /// </summary>
        public SceneReference Destination { get; }

        /// <summary>
        /// 발생한 예외이다.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>실패 후 복귀를 시도한 안전 씬이다.</summary>
        public SceneReference RecoveryScene { get; }

        /// <summary>안전 씬 복귀가 완료되었는지 여부이다.</summary>
        public bool WasRecovered { get; }

        /// <summary>안전 씬 복귀도 실패한 경우의 예외이다.</summary>
        public Exception RecoveryException { get; }

        /// <summary>
        /// 씬 로딩 실패 메시지를 생성한다.
        /// </summary>
        public SceneLoadFailedEvent(
            SceneReference destination,
            Exception exception,
            SceneReference recoveryScene = null,
            bool wasRecovered = false,
            Exception recoveryException = null)
        {
            Destination = destination;
            Exception = exception;
            RecoveryScene = recoveryScene;
            WasRecovered = wasRecovered;
            RecoveryException = recoveryException;
        }
    }
}
