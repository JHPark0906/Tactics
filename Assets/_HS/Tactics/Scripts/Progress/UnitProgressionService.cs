namespace HS.Tactics.Progress
{
    /// <summary>
    /// 씬 전환에도 유지되어야 하는 육성 상태와 그 저장 참여자를 함께 보관하는 프로젝트 서비스이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 스테이지를 오갈 때마다 전투 씬의 컴포넌트는 새로 만들어지므로, 육성 레벨을 그 컴포넌트가 들고 있으면
    /// 이동할 때마다 초기화된다. 그래서 육성 상태는 이 서비스가 소유하고 씬 수명과 분리한다.
    /// 스테이지 진행도를 <c>StageProgressionService</c>가 같은 이유로 소유하는 것과 같은 형태이다.
    /// </para>
    /// <para>
    /// <b>진행도와 나눠 둔 까닭.</b> 둘 다 씬을 넘어 살아남지만 바뀌는 때가 다르다. 진행도는 전투가 끝날 때
    /// 바뀌고 육성 레벨은 전투 밖에서 바뀐다. 하나로 묶으면 한쪽이 바뀔 때마다 다른 쪽까지 저장 대상이 되고,
    /// 저장 키도 하나가 되어 나중에 한쪽만 지우거나 옮길 수 없다.
    /// </para>
    /// </remarks>
    public sealed class UnitProgressionService
    {
        /// <summary>육성 서비스를 생성한다.</summary>
        public UnitProgressionService()
        {
            Levels = new UnitLevelProgress();
            Saveable = new UnitLevelSaveable(Levels);
        }

        /// <summary>유닛 종류별 육성 레벨이다.</summary>
        public UnitLevelProgress Levels { get; }

        /// <summary>육성 레벨을 저장 시스템에 참여시키는 어댑터이다.</summary>
        public UnitLevelSaveable Saveable { get; }
    }
}
