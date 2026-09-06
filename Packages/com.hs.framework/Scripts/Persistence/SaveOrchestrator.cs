using System;
using System.Collections.Generic;
using MessagePipe;

namespace HS.Framework.Persistence
{
    /// <summary>
    /// ISaveable 참여자를 등록받아 저장소 백엔드에 일괄 저장·복원하는 조정자이다.
    /// 자동 저장은 dirty 참여자만 기록하고, 수동 저장은 모든 참여자를 기록한다.
    /// </summary>
    public sealed class SaveOrchestrator
    {
        /// <summary>
        /// 참여자 상태를 기록하는 저장소 백엔드이다.
        /// </summary>
        private readonly ISaveDataStorage _storage;

        /// <summary>
        /// 저장·복원 완료 이벤트를 발행하는 발행자이며, null이면 이벤트를 발행하지 않는다.
        /// </summary>
        private readonly Action<SaveAllCompletedEvent> _publishSaveAllCompleted;
        private readonly Action<LoadAllCompletedEvent> _publishLoadAllCompleted;

        /// <summary>
        /// 등록된 저장 참여자 목록이다.
        /// </summary>
        private readonly List<ISaveable> _participants = new();

        /// <summary>MessagePipe 발행자를 사용하는 저장 조정자를 생성한다.</summary>
        public SaveOrchestrator(
            ISaveDataStorage storage,
            IPublisher<SaveAllCompletedEvent> saveCompletedPublisher,
            IPublisher<LoadAllCompletedEvent> loadCompletedPublisher)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _publishSaveAllCompleted = saveCompletedPublisher == null
                ? throw new ArgumentNullException(nameof(saveCompletedPublisher))
                : saveCompletedPublisher.Publish;
            _publishLoadAllCompleted = loadCompletedPublisher == null
                ? throw new ArgumentNullException(nameof(loadCompletedPublisher))
                : loadCompletedPublisher.Publish;
        }

        /// <summary>
        /// 등록된 저장 참여자 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<ISaveable> Participants => _participants;

        /// <summary>
        /// 저장 참여자를 등록한다. 같은 참여자나 같은 저장 키를 중복 등록할 수 없다.
        /// </summary>
        /// <param name="saveable">등록할 저장 참여자이다.</param>
        public void Register(ISaveable saveable)
        {
            if (saveable == null)
            {
                throw new ArgumentNullException(nameof(saveable));
            }

            if (string.IsNullOrWhiteSpace(saveable.SaveKey))
            {
                throw new ArgumentException("저장 참여자의 저장 키는 비어 있을 수 없습니다.", nameof(saveable));
            }

            if (_participants.Contains(saveable))
            {
                throw new ArgumentException("이미 등록된 저장 참여자입니다.", nameof(saveable));
            }

            foreach (var participant in _participants)
            {
                if (string.Equals(participant.SaveKey, saveable.SaveKey, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"저장 키 '{saveable.SaveKey}'가 이미 등록되어 있습니다.", nameof(saveable));
                }
            }

            _participants.Add(saveable);
        }

        /// <summary>
        /// 저장 참여자를 등록 해제한다.
        /// </summary>
        /// <param name="saveable">해제할 저장 참여자이다.</param>
        /// <returns>등록되어 있어 해제했으면 true를 반환한다.</returns>
        public bool Unregister(ISaveable saveable)
        {
            return saveable != null && _participants.Remove(saveable);
        }

        /// <summary>
        /// 등록된 참여자의 상태를 저장소에 기록한다.
        /// Automatic이면 IsDirty가 true인 참여자만, Manual이면 모든 참여자를 기록한다.
        /// 기록에 성공한 참여자는 AcknowledgeSaved로 dirty 해제 기회를 받는다.
        /// </summary>
        /// <param name="kind">저장을 실행한 원인이다.</param>
        /// <returns>실제로 기록한 참여자 수를 반환한다.</returns>
        public int SaveAll(SaveKind kind)
        {
            var savedCount = 0;
            foreach (var participant in _participants)
            {
                if (kind == SaveKind.Automatic && !participant.IsDirty)
                {
                    continue;
                }

                var serializedState = participant.CaptureState();
                if (serializedState == null)
                {
                    continue;
                }

                _storage.Write(participant.SaveKey, serializedState);
                participant.AcknowledgeSaved();
                savedCount++;
            }

            _publishSaveAllCompleted?.Invoke(new SaveAllCompletedEvent(kind, savedCount, DateTime.UtcNow));
            return savedCount;
        }

        /// <summary>
        /// 저장소에 기록된 상태가 있는 참여자를 모두 복원한다.
        /// </summary>
        /// <returns>실제로 복원한 참여자 수를 반환한다.</returns>
        public int LoadAll()
        {
            var restoredCount = 0;
            foreach (var participant in _participants)
            {
                if (!_storage.TryRead(participant.SaveKey, out var serializedState))
                {
                    continue;
                }

                participant.RestoreState(serializedState);
                restoredCount++;
            }

            _publishLoadAllCompleted?.Invoke(new LoadAllCompletedEvent(restoredCount));
            return restoredCount;
        }

        /// <summary>
        /// 지정한 이벤트가 발행될 때마다 SaveAll(SaveKind.Automatic)을 실행하는 자동 저장 훅을 붙인다.
        /// 어떤 이벤트를 트리거로 쓸지는 호출자가 정책으로 주입하며,
        /// 반환된 IDisposable을 해제하면 훅이 제거되므로 호출자가 수명을 소유해야 한다.
        /// 씬 전환 완료를 트리거로 쓰는 가장 흔한 배선은 프레임워크가 SceneLoadAutoSaveBinder로 제공하므로,
        /// 그 경우에는 이 메서드를 직접 호출하지 않고 해당 컴포넌트를 씬에 배치하면 된다.
        /// </summary>
        /// <typeparam name="TEvent">자동 저장을 트리거할 이벤트 타입이다.</typeparam>
        /// <param name="eventSubscriber">이벤트를 구독할 구독자이다.</param>
        /// <returns>해제하면 자동 저장 훅이 제거되는 구독 핸들을 반환한다.</returns>
        /// <summary>MessagePipe 구독자로 자동 저장 훅을 연결한다.</summary>
        public IDisposable AttachAutoSaveTrigger<TEvent>(ISubscriber<TEvent> eventSubscriber)
        {
            if (eventSubscriber == null)
            {
                throw new ArgumentNullException(nameof(eventSubscriber));
            }

            return eventSubscriber.Subscribe(_ => SaveAll(SaveKind.Automatic));
        }
    }
}
