namespace HS.Tactics.Combat
{
    /// <summary>
    /// 적 주위에 자리를 어떻게 나눌지에 대한 설정이다.
    /// </summary>
    /// <remarks>
    /// <b>여기에 유닛의 수를 넣지 마라.</b> "이 적을 노리는 아군 몇 명 중 몇 번째"로 자리를 정하면
    /// 한 명이 죽는 순간 남은 전원이 자리를 바꿔 우르르 이동한다. 자리 수는 미리 정해 두고 유닛의 번호로만 고른다.
    /// </remarks>
    public readonly struct ApproachSpreadSettings
    {
        /// <summary>자리를 나누지 않는 설정이다.</summary>
        public static readonly ApproachSpreadSettings Disabled = new(0f, 1, 180f);

        /// <summary>자리를 나눌 설정을 만든다.</summary>
        /// <param name="radius">적에서 얼마나 떨어져 설지이다.</param>
        /// <param name="slotCount">나눌 자리의 수이며 1 이하이면 나누지 않는다.</param>
        /// <param name="arcDegrees">자리를 펼칠 각도의 폭이다.</param>
        public ApproachSpreadSettings(float radius, int slotCount, float arcDegrees)
        {
            Radius = radius;
            SlotCount = slotCount;
            ArcDegrees = arcDegrees;
        }

        /// <summary>적에서 얼마나 떨어져 설지이다.</summary>
        public float Radius { get; }

        /// <summary>나눌 자리의 수이다.</summary>
        public int SlotCount { get; }

        /// <summary>자리를 펼칠 각도의 폭이다.</summary>
        public float ArcDegrees { get; }

        /// <summary>자리를 실제로 나누는 설정인지 여부이다.</summary>
        public bool IsEnabled => SlotCount > 1 && Radius > 0f;
    }
}
