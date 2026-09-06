namespace HS.Framework.Ability
{
    /// <summary>
    /// 어빌리티 시스템과 효과 실행기에 시간을 흘려 주는 자리를 정한다. 행동 트리 실행기의 틱 방식과 같은 모양이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 쿨다운·지속 시간·주기는 <b>실시간 초</b>를 센다. 어느 자리에서 돌든 그 자리에 맞는 델타(고정 스텝이면
    /// <c>Time.fixedDeltaTime</c>, 프레임이면 <c>Time.deltaTime</c>)를 넘기므로 「3초」는 두 방식에서 같은 3초다.
    /// </para>
    /// <para>
    /// <b><see cref="OnUpdate"/>를 고르면 프레임률이 결과에 영향을 준다.</b> 한 프레임이 덮는 시간이 기기마다 다르므로
    /// 같은 3초가 몇 번의 판단으로 나뉘는지, 만료가 어느 프레임 경계에 걸리는지가 달라진다. 결과가 기기의 성능에
    /// 따라 달라지면 안 되는 게임은 <see cref="OnFixedUpdate"/>를 쓴다. 입력에 곧바로 반응해야 하거나 고정 주기를
    /// 쓰지 않는 프로젝트를 위해 <see cref="OnUpdate"/>를 남겨 둔다.
    /// </para>
    /// </remarks>
    public enum GameplayTickMode
    {
        /// <summary>매 프레임 <c>Update</c>에서 <c>Time.deltaTime</c>만큼 흘린다. 판단 횟수가 프레임률을 따라간다.</summary>
        OnUpdate,

        /// <summary>고정 주기 <c>FixedUpdate</c>에서 <c>Time.fixedDeltaTime</c>만큼 흘린다. 기본이며, 프레임률이 결과를 바꾸지 않는다.</summary>
        OnFixedUpdate,
    }

    /// <summary>
    /// 어빌리티 시스템·효과 실행기·행동 트리 실행기가 시간을 흘릴 자리를 어디서 받아 올지 정하는 계약이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 인터페이스가 별도로 있는 이유는 참조 방향 때문이다.</b> 틱 방식을 프로젝트 전역에서 하나로
    /// 정하는 값은 <c>HS.Framework.ProjectManagement</c>의 <c>IProjectConfiguration</c>이 들고 있는데,
    /// 어빌리티·효과·행동 트리 세 컴포넌트가 그 인터페이스를 직접 참조하면 ProjectManagement가
    /// <see cref="GameplayTickMode"/> 때문에 이 모듈을 참조하는 것과 맞물려 <b>두 모듈이 서로를 참조하는
    /// 순환</b>이 생긴다. 세 컴포넌트는 이 작은 계약만 알고, <c>IProjectConfiguration</c>이 이것을 상속해
    /// 구현을 대신 채우므로 참조는 언제나 ProjectManagement → Ability 한 방향으로만 흐른다.
    /// </para>
    /// <para>
    /// 등록하는 쪽(Runtime)은 같은 프로젝트 설정 인스턴스를 <c>IProjectConfiguration</c>과 이 인터페이스
    /// 둘 다로 컨테이너에 올려 두므로, 값은 여전히 한 곳(GameMode)에서만 나온다.
    /// </para>
    /// </remarks>
    public interface IGameplayTickModeSource
    {
        /// <summary>어빌리티 시스템·효과 실행기·행동 트리 실행기가 시간을 흘릴 자리이다.</summary>
        GameplayTickMode GameplayTickMode { get; }
    }
}
