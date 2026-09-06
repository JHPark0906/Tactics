namespace HS.Tactics.Units
{
    /// <summary>
    /// 유닛 행동 트리 노드가 공유 컨텍스트에서 값을 주고받을 때 쓰는 키이다.
    /// 새 키가 필요하면 이 클래스에 상수로 추가해 키 문자열이 코드 곳곳에 흩어지지 않게 한다.
    /// </summary>
    public static class UnitBehaviourKeys
    {
        /// <summary>
        /// 교전하거나 접근할 대상의 Transform이다.
        /// 프레임워크의 ChaseTargetBehaviour가 이 키에서 대상을 읽는다.
        /// </summary>
        public const string Target = "Unit.Target";

        /// <summary>
        /// 이동할 목표 좌표(Vector3)이다.
        /// 프레임워크의 MoveToPositionBehaviour가 이 키에서 목표를 읽으며,
        /// 전진과 엄폐 이동이 같은 키를 공유하되 목표를 채우는 노드가 서로 다르다.
        /// </summary>
        public const string Destination = "Unit.Destination";

        /// <summary>
        /// 전진 방향(Vector3)이다. 맵이 일자형이므로 보통 아군 진영에서 적 진영을 향하는 방향이다.
        /// 값이 없으면 유닛이 바라보는 방향을 전진 방향으로 사용한다.
        /// </summary>
        public const string AdvanceDirection = "Unit.AdvanceDirection";

        /// <summary>
        /// 대상이 사거리 안에 있으면 담기고, 없으면 지워지는 표식이다. 값이 있는지만 본다.
        /// 사거리 안에서 멈추는 조건이 이 키를 지켜보다가, 대상이 들어오는 순간 추격을 끊는다.
        /// </summary>
        public const string TargetInRange = "Unit.TargetInRange";
    }
}
