namespace HS.Tactics.Units
{
    /// <summary>
    /// 한 판에 데리고 나갈 파티의 크기와 배치 격자를 정하는 규칙이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>소수정예가 이득이 되게 하려는 것이다.</b> 데려갈 수 있는 영웅이 적으면 한 기를 고르는 일이
    /// 곧 전략이 되고, 경험치도 그 인원수로 나뉘므로 <b>적게 데려간 쪽이 더 빨리 자란다.</b>
    /// 수를 늘려 밀어붙이는 선택과 소수를 키우는 선택이 서로 값을 치르게 하는 것이 목적이다.
    /// </para>
    /// <para>
    /// <b>이 수는 여러 곳에서 쓰이므로 각자 들고 있으면 안 된다.</b> 배치 상한과 경험치 분배가
    /// 같은 인원수를 봐야 하는데 상수가 두 벌이면 한쪽만 고쳐진 채로 지나갈 수 있고,
    /// 그때 <b>어긋난 것은 아무 신호도 내지 않는다</b> — 컴파일도 되고 검사도 통과하며,
    /// 화면에서는 그저 경험치가 이상하게 들어온다. 그래서 여기 하나만 둔다.
    /// </para>
    /// <para>
    /// 배치는 3×3 격자 안에서 이루어지므로 자리는 아홉이지만 데려갈 수 있는 영웅은 다섯이다.
    /// 자리가 남는 것은 의도한 것이며, 어디에 세우는지가 선택이 되게 한다.
    /// </para>
    /// </remarks>
    public static class PartyRules
    {
        /// <summary>한 판에 데려갈 수 있는 영웅 수의 상한이다.</summary>
        public const int MaxPartySize = 5;

        /// <summary>배치 격자의 열 수이다.</summary>
        public const int GridColumns = 3;

        /// <summary>배치 격자의 행 수이다.</summary>
        public const int GridRows = 3;

        /// <summary>배치 격자의 전체 칸 수이며, 데려갈 수 있는 영웅 수보다 많다.</summary>
        public static int GridCellCount => GridColumns * GridRows;
    }
}
