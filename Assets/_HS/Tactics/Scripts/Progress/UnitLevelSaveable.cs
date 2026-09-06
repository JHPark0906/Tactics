using System;
using System.Collections.Generic;
using HS.Framework.Persistence;
using UnityEngine;

namespace HS.Tactics.Progress
{
    /// <summary>
    /// 육성한 유닛의 레벨과 경험치를 저장 시스템에 참여시키는 어댑터이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>저장하는 것은 플레이어가 육성하는 것뿐이다.</b> 적도 레벨을 갖지만 그것은 전투를 구성하는 쪽이
    /// 정하는 값이지 플레이어가 쌓은 것이 아니므로 저장하지 않는다. 스테이지가 적을 어떻게 꾸리는지와
    /// 무관하게 그렇다 — 근거는 데이터가 어디 있느냐가 아니라 <b>누가 키운 것이냐</b>이다.
    /// </para>
    /// <para>
    /// <b>아직 자라지 않은 종류는 적지 않는다.</b> 없는 종류를 물으면 시작 레벨과 시작 경험치로 답하므로
    /// 결과가 같고, 그래야 새 유닛 종류가 생겨도 저장 데이터를 손볼 것이 없다.
    /// </para>
    /// <para>
    /// <b>경험치가 없는 저장 파일도 읽을 수 있다.</b> 경험치가 없는 옛 파일을 읽으면 <see cref="JsonUtility"/>가
    /// 그 칸을 0으로 두는데, 그것이 곧 <see cref="UnitLevelProgress.StartingExperience"/>이므로
    /// <b>옛 파일이 뜻하던 상태와 정확히 같다.</b> 판을 올려 두 갈래로 읽으면 다르게 읽힐 일이 없는데도
    /// 갈래가 늘어난다. 뜻이 달라지는 변경이 오면 그때 판을 올린다.
    /// </para>
    /// <para>
    /// dirty 여부는 <see cref="UnitLevelProgress.Revision"/>을 마지막으로 저장한 값과 견주어 판정한다.
    /// 캡처와 기록 사이에 레벨이 다시 바뀌면 저장한 뒤에도 dirty가 남아 다음 자동 저장에서 다시 적힌다.
    /// </para>
    /// <para>
    /// <b>아무것도 키우지 않은 첫 상태도 dirty이다.</b> <see cref="HS.Tactics.Flow.StageProgressSaveable"/>와 같은 규약이며,
    /// 첫 저장이 건너뛰어지면 저장 파일 자체가 만들어지지 않아 <b>다음 실행이 복원할 것을 찾지 못한다.</b>
    /// 그때 적히는 것은 빈 목록이라 잃는 것은 없다.
    /// </para>
    /// </remarks>
    public sealed class UnitLevelSaveable : ISaveable
    {
        /// <summary>저장소에서 육성 레벨을 식별하는 기본 키이다.</summary>
        public const string DefaultSaveKey = "Tactics.UnitLevels";

        /// <summary>아직 한 번도 저장하지 않았음을 나타내는 표식이다.</summary>
        private const int NotSavedYet = -1;

        /// <summary>저장·복원 대상 육성 상태이다.</summary>
        private readonly UnitLevelProgress _progress;

        /// <summary>저장소에서 이 상태를 식별하는 키이다.</summary>
        private readonly string _saveKey;

        private int _savedRevision = NotSavedYet;
        private int _capturedRevision;

        /// <summary>육성 레벨 저장 참여자를 생성한다.</summary>
        /// <param name="progress">저장·복원할 육성 상태이다.</param>
        /// <param name="saveKey">저장소 키이며, 지정하지 않으면 <see cref="DefaultSaveKey"/>를 쓴다.</param>
        /// <exception cref="ArgumentNullException">육성 상태가 null이면 발생한다.</exception>
        public UnitLevelSaveable(UnitLevelProgress progress, string saveKey = DefaultSaveKey)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _saveKey = string.IsNullOrWhiteSpace(saveKey) ? DefaultSaveKey : saveKey;
        }

        /// <inheritdoc />
        public string SaveKey => _saveKey;

        /// <inheritdoc />
        public bool IsDirty => _savedRevision != _progress.Revision;

        /// <inheritdoc />
        public string CaptureState()
        {
            _capturedRevision = _progress.Revision;
            var data = new UnitLevelSaveData { levels = new List<UnitLevelEntry>() };
            foreach (var raised in _progress.EnumerateRaised())
            {
                data.levels.Add(new UnitLevelEntry
                {
                    id = raised.Key,
                    level = raised.Value.Level,
                    xp = raised.Value.Experience,
                });
            }

            return JsonUtility.ToJson(data);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 담긴 것을 먼저 비우고 적힌 것만 넣는다. 비우지 않으면 지난 판에서 키운 종류가
        /// 저장 파일에 없는데도 남는다.
        /// </remarks>
        public void RestoreState(string serializedState)
        {
            if (string.IsNullOrWhiteSpace(serializedState))
            {
                return;
            }

            var data = JsonUtility.FromJson<UnitLevelSaveData>(serializedState);
            if (data == null)
            {
                return;
            }

            _progress.Clear();
            if (data.levels != null)
            {
                foreach (var entry in data.levels)
                {
                    if (entry != null)
                    {
                        _progress.SetGrowth(entry.id, new UnitGrowth(entry.level, entry.xp));
                    }
                }
            }

            // 복원 직후 상태는 저장소와 일치하므로 기준을 지금 값으로 맞춰 dirty를 해제한다.
            _savedRevision = _progress.Revision;
        }

        /// <inheritdoc />
        public void AcknowledgeSaved() => _savedRevision = _capturedRevision;

        /// <summary>
        /// 저장소에 기록하는 육성 데이터이다.
        /// </summary>
        /// <remarks>
        /// 사전이 아니라 목록으로 담는 것은 <see cref="JsonUtility"/>가 사전을 다루지 못하기 때문이다.
        /// </remarks>
        [Serializable]
        private sealed class UnitLevelSaveData
        {
            /// <summary>시작 상태보다 자란 종류들이다.</summary>
            public List<UnitLevelEntry> levels;
        }

        /// <summary>
        /// 유닛 종류 하나의 육성 상태이다.
        /// </summary>
        /// <remarks>
        /// 경험치 칸이 없던 옛 파일에서는 <see cref="xp"/>가 0으로 읽히며, 그것이 곧 쌓은 적 없는 상태이다.
        /// </remarks>
        [Serializable]
        private sealed class UnitLevelEntry
        {
            /// <summary>유닛 종류의 식별자이다.</summary>
            public string id;

            /// <summary>그 종류의 레벨이다.</summary>
            public int level;

            /// <summary>다음 레벨을 향해 쌓인 경험치이다.</summary>
            public int xp;
        }
    }
}
