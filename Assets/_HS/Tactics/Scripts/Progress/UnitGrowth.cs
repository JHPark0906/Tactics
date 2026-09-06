using System;

namespace HS.Tactics.Progress
{
    /// <summary>
    /// 유닛 종류 하나가 얼마나 자랐는지이다. 레벨과 다음 레벨을 향해 쌓인 경험치를 함께 든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>둘을 함께 두는 것이 요점이다.</b> 레벨과 경험치는 따로 바뀌지 않는다 — 경험치가 차면 레벨이
    /// 오르고 그만큼 경험치가 줄어든다. 나눠 두면 한쪽만 담기거나 한쪽만 저장되는 중간 상태가 생긴다.
    /// </para>
    /// <para>
    /// <b>자라지 않은 상태를 구별할 수 있어야 한다.</b> 담아 두지 않는 기준이 되기 때문이다.
    /// 레벨이 시작값이어도 경험치가 쌓여 있으면 자란 것이므로 담아야 한다.
    /// </para>
    /// </remarks>
    public readonly struct UnitGrowth : IEquatable<UnitGrowth>
    {
        /// <summary>아직 키우지 않은 종류의 상태이다.</summary>
        public static readonly UnitGrowth Start =
            new(UnitLevelProgress.StartingLevel, UnitLevelProgress.StartingExperience);

        /// <summary>육성 상태를 만든다.</summary>
        /// <param name="level">지금 레벨이며 시작 레벨보다 낮으면 시작 레벨로 올린다.</param>
        /// <param name="experience">쌓인 경험치이며 음수이면 0으로 올린다.</param>
        public UnitGrowth(int level, int experience)
        {
            Level = level < UnitLevelProgress.StartingLevel ? UnitLevelProgress.StartingLevel : level;
            Experience = experience < UnitLevelProgress.StartingExperience
                ? UnitLevelProgress.StartingExperience
                : experience;
        }

        /// <summary>지금 레벨이며 항상 시작 레벨 이상이다.</summary>
        public int Level { get; }

        /// <summary>다음 레벨을 향해 쌓인 경험치이며 항상 0 이상이다.</summary>
        public int Experience { get; }

        /// <summary>아직 아무것도 자라지 않은 상태인지 여부이다.</summary>
        public bool IsAtStart =>
            Level == UnitLevelProgress.StartingLevel && Experience == UnitLevelProgress.StartingExperience;

        /// <inheritdoc />
        public bool Equals(UnitGrowth other) => Level == other.Level && Experience == other.Experience;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is UnitGrowth other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Level * 397) ^ Experience;
    }
}
