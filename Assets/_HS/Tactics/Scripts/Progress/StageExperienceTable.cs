using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Progress
{
    /// <summary>
    /// 스테이지를 깼을 때 파티가 받는 경험치를 스테이지마다 적어 둔 표이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>여기 적히는 수는 밸런스이며 사용자 몫이다.</b> 코드는 표를 읽는 방법만 정하고 값은 에셋에서 채운다.
    /// </para>
    /// <para>
    /// <b>적히지 않은 스테이지는 0이되 한 번 알린다.</b> 0을 조용히 돌려주면 사용자는 그 스테이지를 깨고도
    /// 아무도 자라지 않는 것을 보게 되는데, 화면에 남는 단서가 없어 표를 빠뜨렸다는 것을 알 수 없다.
    /// 매번 알리면 스테이지가 도는 동안 같은 줄이 쌓이므로 스테이지마다 한 번만 알린다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "StageExperienceTable", menuName = "Tactics/Stage Experience Table")]
    public sealed class StageExperienceTable : ScriptableObject
    {
        [Tooltip("스테이지를 깼을 때 파티가 받는 경험치이다. 값은 밸런스이며 임시로 비워 둔다.")]
        [SerializeField]
        private List<StageExperienceEntry> entries = new();

        /// <summary>이미 알린 스테이지이며 같은 경고를 되풀이하지 않으려고 기억해 둔다.</summary>
        private readonly HashSet<int> _warnedStages = new();

        /// <summary>
        /// 그 스테이지를 깼을 때 파티가 받는 경험치를 읽는다.
        /// </summary>
        /// <param name="stageId">깬 스테이지의 식별자이다.</param>
        /// <returns>파티가 받는 경험치이며, 적혀 있지 않으면 0이다.</returns>
        public int GetStageExperience(int stageId)
        {
            if (entries != null)
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (entry != null && entry.StageId == stageId)
                    {
                        return Mathf.Max(0, entry.Experience);
                    }
                }
            }

            WarnMissingStageOnce(stageId);
            return 0;
        }

        /// <summary>그 스테이지가 표에 적혀 있는지 본다.</summary>
        /// <param name="stageId">확인할 스테이지 식별자이다.</param>
        /// <returns>적혀 있으면 true이다.</returns>
        public bool HasStage(int stageId)
        {
            if (entries == null)
            {
                return false;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && entry.StageId == stageId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>표에 없는 스테이지를 처음 물었을 때 한 번만 알린다.</summary>
        /// <param name="stageId">표에 없던 스테이지 식별자이다.</param>
        private void WarnMissingStageOnce(int stageId)
        {
            if (!_warnedStages.Add(stageId))
            {
                return;
            }

            Debug.LogWarning(
                $"[StageExperienceTable] {stageId}번 스테이지의 경험치가 적혀 있지 않아 0을 준다. " +
                "그 스테이지를 깨도 파티가 자라지 않는다. 표에 그 스테이지를 더해야 한다.",
                this);
        }

        /// <summary>스테이지 하나가 주는 경험치이다.</summary>
        [Serializable]
        public sealed class StageExperienceEntry
        {
            [Tooltip("스테이지 식별자이며 씬 카탈로그의 Gameplay Level Id와 같은 값이다.")]
            [SerializeField]
            private int stageId;

            [Tooltip("그 스테이지를 깼을 때 파티가 받는 경험치이다. 배치한 인원수로 나뉜다.")]
            [SerializeField]
            [Min(0)]
            private int experience;

            /// <summary>스테이지 식별자이다.</summary>
            public int StageId => stageId;

            /// <summary>그 스테이지가 주는 경험치이다.</summary>
            public int Experience => experience;
        }
    }
}
