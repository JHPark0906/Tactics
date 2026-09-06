using System;
using HS.Framework.Persistence;
using UnityEngine;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 스테이지 진행도를 저장 시스템에 참여시키는 어댑터이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 저장 대상은 도전 중인 스테이지와 클리어한 최고 스테이지뿐이다. 전투 중 상태는 재도전으로 항상 처음부터
    /// 다시 만들어지므로 저장하지 않는다.
    /// </para>
    /// <para>
    /// dirty 여부는 마지막으로 저장에 성공한 값과 현재 값을 비교해 판정한다. 따라서 캡처와 기록 사이에
    /// 진행도가 다시 바뀌면 저장 후에도 dirty가 유지되어 다음 자동 저장에서 다시 기록된다.
    /// </para>
    /// </remarks>
    public sealed class StageProgressSaveable : ISaveable
    {
        /// <summary>저장소에서 스테이지 진행도를 식별하는 기본 키이다.</summary>
        public const string DefaultSaveKey = "Tactics.StageProgress";

        /// <summary>아직 한 번도 저장하지 않았음을 나타내는 표식이다.</summary>
        private const int NotSavedYet = int.MinValue;

        /// <summary>저장·복원 대상 진행 상태이다.</summary>
        private readonly StageProgression _progression;

        /// <summary>저장소에서 이 진행도를 식별하는 키이다.</summary>
        private readonly string _saveKey;

        private int _savedStageId = NotSavedYet;
        private int _savedClearedStageId = NotSavedYet;
        private int _capturedStageId;
        private int _capturedClearedStageId;

        /// <summary>스테이지 진행도 저장 참여자를 생성한다.</summary>
        /// <param name="progression">저장·복원할 진행 상태이다.</param>
        /// <param name="saveKey">저장소 키이며, 지정하지 않으면 <see cref="DefaultSaveKey"/>를 사용한다.</param>
        /// <exception cref="ArgumentNullException">진행 상태가 null이면 발생한다.</exception>
        public StageProgressSaveable(StageProgression progression, string saveKey = DefaultSaveKey)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _saveKey = string.IsNullOrWhiteSpace(saveKey) ? DefaultSaveKey : saveKey;
        }

        /// <inheritdoc />
        public string SaveKey => _saveKey;

        /// <inheritdoc />
        public bool IsDirty =>
            _savedStageId != _progression.CurrentStageId ||
            _savedClearedStageId != _progression.HighestClearedStageId;

        /// <inheritdoc />
        public string CaptureState()
        {
            _capturedStageId = _progression.CurrentStageId;
            _capturedClearedStageId = _progression.HighestClearedStageId;
            return JsonUtility.ToJson(new StageProgressSaveData
            {
                currentStageId = _capturedStageId,
                highestClearedStageId = _capturedClearedStageId
            });
        }

        /// <inheritdoc />
        public void RestoreState(string serializedState)
        {
            if (string.IsNullOrWhiteSpace(serializedState))
            {
                return;
            }

            var data = JsonUtility.FromJson<StageProgressSaveData>(serializedState);
            if (data == null)
            {
                return;
            }

            _progression.SetHighestClearedStage(data.highestClearedStageId);
            _progression.SetCurrentStage(data.currentStageId);

            // 복원 직후 상태는 저장소와 일치하므로 저장된 값 기준을 현재 값으로 맞춰 dirty를 해제한다.
            _savedStageId = _progression.CurrentStageId;
            _savedClearedStageId = _progression.HighestClearedStageId;
        }

        /// <inheritdoc />
        public void AcknowledgeSaved()
        {
            _savedStageId = _capturedStageId;
            _savedClearedStageId = _capturedClearedStageId;
        }

        /// <summary>
        /// 저장소에 기록하는 스테이지 진행도 데이터이다.
        /// </summary>
        [Serializable]
        private sealed class StageProgressSaveData
        {
            /// <summary>도전 중인 스테이지 식별자이다.</summary>
            public int currentStageId;

            /// <summary>클리어한 가장 높은 스테이지 식별자이다.</summary>
            public int highestClearedStageId;
        }
    }
}
