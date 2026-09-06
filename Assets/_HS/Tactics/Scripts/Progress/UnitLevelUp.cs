namespace HS.Tactics.Progress
{
    /// <summary>경험치를 더한 뒤의 레벨과 남은 경험치이다.</summary>
    public readonly struct UnitLevelUpResult
    {
        /// <summary>결과를 만든다.</summary>
        /// <param name="level">올라간 뒤의 레벨이다.</param>
        /// <param name="experience">다음 레벨을 향해 남은 경험치이다.</param>
        /// <param name="gainedLevels">이번에 오른 레벨 수이다.</param>
        public UnitLevelUpResult(int level, int experience, int gainedLevels)
        {
            Level = level;
            Experience = experience;
            GainedLevels = gainedLevels;
        }

        /// <summary>올라간 뒤의 레벨이다.</summary>
        public int Level { get; }

        /// <summary>다음 레벨을 향해 남은 경험치이다.</summary>
        public int Experience { get; }

        /// <summary>이번에 오른 레벨 수이며 오르지 않았으면 0이다.</summary>
        public int GainedLevels { get; }
    }

    /// <summary>
    /// 경험치를 더해 레벨이 오르는 규칙이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>상태를 갖지 않는 것이 이 형식의 요점이다.</b> 레벨이 오르는 입구는 지금 스테이지 클리어
    /// 하나뿐이지만 앞으로 늘 수 있다. 규칙이 상태를 들고 있는 쪽에 박혀 있으면 새 입구가 생길 때마다
    /// 규칙이 한 벌씩 복사되고, <b>한쪽만 고쳐진 채로 지나가도 아무 신호가 없다.</b>
    /// </para>
    /// <para>
    /// <b>한 번에 여러 레벨이 오를 수 있다.</b> 스테이지 하나로 두 단계를 넘길 만큼 받으면 그만큼 오른다.
    /// 한 번에 한 레벨만 올리면 남은 경험치가 쌓인 채 멈추고, 다음 클리어까지 오르지 않는다.
    /// </para>
    /// </remarks>
    public static class UnitLevelUp
    {
        /// <summary>
        /// 경험치를 더하고 오를 수 있는 만큼 레벨을 올린다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>최대 레벨에 닿으면 남은 경험치를 버린다.</b> 들고 있어도 쓸 곳이 없고, 남겨 두면
        /// 나중에 최대 레벨이 올라갔을 때 <b>플레이하지 않은 몫이 한꺼번에 들어온다.</b>
        /// </para>
        /// <para>
        /// 곡선이 없으면 아무것도 하지 않는다. 곡선 없이 기본값으로 올리면 그 값이 어디서 왔는지
        /// 아무도 모르는 채로 레벨이 오르고, 곡선을 꽂는 순간 결과가 달라진다.
        /// </para>
        /// </remarks>
        /// <param name="level">지금 레벨이다.</param>
        /// <param name="experience">지금 쌓인 경험치이다.</param>
        /// <param name="amount">더할 경험치이며 0 이하이면 더하지 않는다.</param>
        /// <param name="curve">레벨이 오르는 데 드는 경험치를 아는 곡선이다.</param>
        /// <returns>더한 뒤의 레벨과 남은 경험치, 그리고 오른 레벨 수이다.</returns>
        public static UnitLevelUpResult Apply(int level, int experience, int amount, UnitLevelCurve curve)
        {
            var currentLevel = level < UnitLevelProgress.StartingLevel ? UnitLevelProgress.StartingLevel : level;
            var currentExperience = experience < 0 ? 0 : experience;
            if (curve == null)
            {
                return new UnitLevelUpResult(currentLevel, currentExperience, 0);
            }

            if (amount > 0)
            {
                currentExperience += amount;
            }

            var gainedLevels = 0;
            while (currentLevel < curve.MaxLevel)
            {
                var required = curve.GetXpToNext(currentLevel);
                if (required <= 0 || currentExperience < required)
                {
                    break;
                }

                currentExperience -= required;
                currentLevel++;
                gainedLevels++;
            }

            if (currentLevel >= curve.MaxLevel)
            {
                // 더 오를 곳이 없으므로 남은 몫은 버린다. 들고 있으면 최대 레벨이 올라간 날 한꺼번에 들어온다.
                currentExperience = 0;
            }

            return new UnitLevelUpResult(currentLevel, currentExperience, gainedLevels);
        }
    }
}
