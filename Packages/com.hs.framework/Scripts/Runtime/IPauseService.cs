namespace HS.Framework.Runtime
{
    /// <summary>
    /// 클라이언트 일시정지 상태를 제어하는 계약이다.
    /// 시간 배율과 입력 활성화 상태를 함께 저장하고 복원한다.
    /// </summary>
    public interface IPauseService
    {
        /// <summary>현재 일시정지 상태인지 여부이다.</summary>
        bool IsPaused { get; }

        /// <summary>시간 배율과 입력 상태를 저장한 뒤 클라이언트를 일시정지한다.</summary>
        void Pause();

        /// <summary>저장한 시간 배율과 입력 상태를 복원해 일시정지를 해제한다.</summary>
        void Resume();
    }

    /// <summary>
    /// 일시정지 상태가 변경되었음을 알리는 메시지이다.
    /// </summary>
    public readonly struct PauseChangedEvent
    {
        /// <summary>변경된 일시정지 상태이다.</summary>
        public bool IsPaused { get; }

        /// <summary>일시정지 상태 변경 메시지를 생성한다.</summary>
        public PauseChangedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }
}
