namespace HS.Tactics.Placement
{
    /// <summary>
    /// 플레이어가 전투 개시를 선언해 배치가 끝났다는 사실을 알리는 게임 레이어 이벤트이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UnitPlacementController.TryStartBattle"/>가 배치 단계를 전투 단계로 옮긴 직후 정확히 한 번
    /// 발행한다. 데이터가 없는 것은 신호 자체가 전부이기 때문이다 — 어느 스테이지인지, 무엇을 배치했는지는
    /// 이 이벤트를 구독하는 쪽이 필요하면 다른 통로(배치 컨트롤러, 진행 상태)에서 직접 읽는다.
    /// </para>
    /// <para>
    /// <see cref="HS.Framework.Gameplay.GameState"/>가 이 이벤트를 구독해 자기 상태를 바꾼다.
    /// 구독하는 쪽이 <see cref="UnitPlacementController"/>를 직접 참조하지 않게 하려는 것이 이 이벤트를
    /// 따로 둔 이유이다.
    /// </para>
    /// </remarks>
    public readonly struct PlacementCompletedEvent
    {
    }
}
