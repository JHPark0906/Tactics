using System.Collections.Generic;
using HS.Tactics.Progress;

namespace HS.Tactics.Flow
{
    /// <summary>스테이지 하나를 깨고 나눠 준 경험치의 결과이다.</summary>
    public readonly struct StageRewardResult
    {
        /// <summary>보상 결과를 만든다.</summary>
        /// <param name="sharePerUnit">한 기가 받은 경험치이다.</param>
        /// <param name="awardedCount">경험치를 받은 배치 자리의 수이다.</param>
        /// <param name="gainedLevels">이번에 오른 레벨의 총합이다.</param>
        public StageRewardResult(int sharePerUnit, int awardedCount, int gainedLevels)
        {
            SharePerUnit = sharePerUnit;
            AwardedCount = awardedCount;
            GainedLevels = gainedLevels;
        }

        /// <summary>한 기가 받은 경험치이며, 줄 것이 없었으면 0이다.</summary>
        public int SharePerUnit { get; }

        /// <summary>경험치를 받은 배치 자리의 수이다.</summary>
        public int AwardedCount { get; }

        /// <summary>이번에 오른 레벨의 총합이며 아무도 오르지 않았으면 0이다.</summary>
        public int GainedLevels { get; }
    }

    /// <summary>
    /// 스테이지를 깼을 때 배치했던 유닛 종류들에 경험치를 나눠 주는 서비스이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>표와 곡선은 밖에서 받는다.</b> 둘 다 사용자가 값을 채우는 에셋이고, 여기서 찾아 쓰면
    /// 어느 에셋이 쓰였는지가 코드 안에 숨는다. 없으면 <b>아무것도 주지 않는다</b> — 기본값으로
    /// 대신 올리면 그 수가 어디서 왔는지 모르는 채로 레벨이 오르고, 에셋을 꽂는 순간 결과가 달라진다.
    /// </para>
    /// <para>
    /// <b>같은 종류를 둘 배치하면 그 종류가 두 몫을 받는다.</b> 몫은 배치한 자리마다 셈하고, 레벨은
    /// 종류에 붙기 때문이다. 나눠 준 총합은 스테이지가 준 몫을 넘지 않으므로 이득이 되지는 않는다.
    /// </para>
    /// <para>
    /// <b>GameObject를 다루지 않는다.</b> 무엇을 배치했는지는 식별자 목록으로만 받으므로, 잡는 시점과
    /// 나눠 주는 시점 사이에 유닛이 죽어도 목록은 그대로다. 죽은 유닛을 빼면 일부러 죽게 두어
    /// 몫을 키우는 길이 생긴다.
    /// </para>
    /// </remarks>
    public sealed class StageRewardService
    {
        /// <summary>경험치를 더할 육성 상태이다.</summary>
        private readonly UnitLevelProgress _levels;

        /// <summary>스테이지가 주는 경험치를 아는 표이며 없을 수 있다.</summary>
        private readonly StageExperienceTable _table;

        /// <summary>레벨이 오르는 데 드는 경험치를 아는 곡선이며 없을 수 있다.</summary>
        private readonly UnitLevelCurve _curve;

        /// <summary>보상 서비스를 만든다.</summary>
        /// <param name="levels">경험치를 더할 육성 상태이다.</param>
        /// <param name="table">스테이지가 주는 경험치 표이며, 없으면 아무것도 주지 않는다.</param>
        /// <param name="curve">레벨 곡선이며, 없으면 경험치를 쌓지 않는다.</param>
        public StageRewardService(UnitLevelProgress levels, StageExperienceTable table, UnitLevelCurve curve)
        {
            _levels = levels;
            _table = table;
            _curve = curve;
        }

        /// <summary>
        /// 깬 스테이지의 경험치를 배치했던 종류들에 나눠 준다.
        /// </summary>
        /// <remarks>
        /// 나누는 수는 <b>배치한 자리의 수</b>이며 살아남은 수가 아니다. 빈 식별자는 몫을 받지 못하지만
        /// 나누는 수에는 든다 — 자리를 채운 것은 사실이므로, 빼면 식별자가 빠진 유닛이 있을 때
        /// 나머지 인원의 몫이 슬그머니 커진다.
        /// </remarks>
        /// <param name="stageId">방금 깬 스테이지의 식별자이다.</param>
        /// <param name="partyDefinitionIds">그 스테이지에 배치했던 유닛 종류의 식별자들이며 죽은 유닛도 포함한다.</param>
        /// <returns>나눠 준 결과이며, 줄 것이 없었으면 전부 0이다.</returns>
        public StageRewardResult Award(int stageId, IReadOnlyList<string> partyDefinitionIds)
        {
            if (_levels == null || _table == null || partyDefinitionIds == null || partyDefinitionIds.Count == 0)
            {
                return default;
            }

            var total = _table.GetStageExperience(stageId);
            var share = StageExperienceReward.ResolveShare(total, partyDefinitionIds.Count);
            if (share <= 0)
            {
                return default;
            }

            var awarded = 0;
            var gainedLevels = 0;
            for (var index = 0; index < partyDefinitionIds.Count; index++)
            {
                var definitionId = partyDefinitionIds[index];
                if (string.IsNullOrWhiteSpace(definitionId))
                {
                    continue;
                }

                gainedLevels += _levels.AddExperience(definitionId, share, _curve);
                awarded++;
            }

            return new StageRewardResult(share, awarded, gainedLevels);
        }
    }
}
