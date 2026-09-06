namespace HS.Framework.Persistence
{
    /// <summary>
    /// SaveOrchestrator에 등록되어 저장·복원에 참여하는 대상의 계약이다.
    /// 구현체는 자기 상태를 문자열로 직렬화·역직렬화하는 방법과 저장 키를 제공한다.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// 저장소에서 이 참여자의 상태를 식별하는 고유 키를 가져온다.
        /// </summary>
        string SaveKey { get; }

        /// <summary>
        /// 마지막 저장 이후 상태가 변경되어 저장이 필요한지 가져온다.
        /// 자동 저장은 이 값이 true인 참여자만 기록한다.
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// 현재 상태를 직렬화한 문자열로 캡처한다.
        /// 구현체는 캡처 시점의 revision 등을 함께 기록해 두었다가 AcknowledgeSaved에서 사용해야 한다.
        /// </summary>
        /// <returns>직렬화된 상태 문자열이며, null이면 이번 저장을 건너뛴다.</returns>
        string CaptureState();

        /// <summary>
        /// 직렬화된 상태 문자열로 상태를 복원한다.
        /// 복원 직후 상태는 저장소와 일치하므로 구현체는 dirty 상태를 해제해야 한다.
        /// </summary>
        /// <param name="serializedState">복원할 직렬화된 상태 문자열이다.</param>
        void RestoreState(string serializedState);

        /// <summary>
        /// 마지막 CaptureState가 반환한 상태가 저장소에 성공적으로 기록된 뒤 호출된다.
        /// 구현체는 캡처 이후 상태가 다시 변경되지 않았을 때만 dirty 상태를 해제해야 한다.
        /// </summary>
        void AcknowledgeSaved();
    }
}
