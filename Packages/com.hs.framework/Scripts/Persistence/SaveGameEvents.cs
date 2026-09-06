using System;

namespace HS.Framework.Persistence
{
    /// <summary>
    /// 저장소 백엔드에 저장된 게임 데이터를 성공적으로 불러온 뒤 발행된다.
    /// </summary>
    public readonly struct SaveGameLoadedEvent
    {
        /// <summary>
        /// 데이터를 불러온 저장소 키를 가져온다.
        /// </summary>
        public string SaveKey { get; }

        /// <summary>
        /// 불러온 저장 게임의 개수를 가져온다.
        /// </summary>
        public int SaveGameCount { get; }

        /// <summary>
        /// 저장 게임 불러오기 완료 이벤트를 생성한다.
        /// </summary>
        public SaveGameLoadedEvent(string saveKey, int saveGameCount)
        {
            SaveKey = saveKey;
            SaveGameCount = saveGameCount;
        }
    }

    /// <summary>
    /// 메모리의 저장 게임 데이터를 저장소 백엔드에 성공적으로 기록한 뒤 발행된다.
    /// </summary>
    public readonly struct SaveGameSavedEvent
    {
        /// <summary>
        /// 데이터를 기록한 저장소 키를 가져온다.
        /// </summary>
        public string SaveKey { get; }

        /// <summary>
        /// 기록한 저장 게임의 개수를 가져온다.
        /// </summary>
        public int SaveGameCount { get; }

        /// <summary>
        /// 저장을 완료한 UTC 시각을 가져온다.
        /// </summary>
        public DateTime SavedAtUtc { get; }

        /// <summary>
        /// 저장 게임 기록 완료 이벤트를 생성한다.
        /// </summary>
        public SaveGameSavedEvent(string saveKey, int saveGameCount, DateTime savedAtUtc)
        {
            SaveKey = saveKey;
            SaveGameCount = saveGameCount;
            SavedAtUtc = savedAtUtc;
        }
    }

    /// <summary>
    /// SaveOrchestrator가 등록된 참여자의 일괄 저장을 마친 뒤 발행된다.
    /// </summary>
    public readonly struct SaveAllCompletedEvent
    {
        /// <summary>
        /// 저장을 실행한 원인을 가져온다.
        /// </summary>
        public SaveKind Kind { get; }

        /// <summary>
        /// 실제로 기록한 참여자 수를 가져온다.
        /// </summary>
        public int SavedCount { get; }

        /// <summary>
        /// 저장을 완료한 UTC 시각을 가져온다.
        /// </summary>
        public DateTime SavedAtUtc { get; }

        /// <summary>
        /// 일괄 저장 완료 이벤트를 생성한다.
        /// </summary>
        public SaveAllCompletedEvent(SaveKind kind, int savedCount, DateTime savedAtUtc)
        {
            Kind = kind;
            SavedCount = savedCount;
            SavedAtUtc = savedAtUtc;
        }
    }

    /// <summary>
    /// SaveOrchestrator가 등록된 참여자의 일괄 복원을 마친 뒤 발행된다.
    /// </summary>
    public readonly struct LoadAllCompletedEvent
    {
        /// <summary>
        /// 실제로 복원한 참여자 수를 가져온다.
        /// </summary>
        public int RestoredCount { get; }

        /// <summary>
        /// 일괄 복원 완료 이벤트를 생성한다.
        /// </summary>
        public LoadAllCompletedEvent(int restoredCount)
        {
            RestoredCount = restoredCount;
        }
    }
}
