namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// 부여된 어빌리티 하나의 실행 단위이다. 게임은 이 클래스를 상속해 실제로 할 일을 적는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>기본값은 곧바로 끝나는 것이다.</b> <see cref="OnTick"/>의 기본 구현이
    /// <see cref="GameplayAbilityTickResult.Finished"/>를 돌려주므로, 아무것도 재정의하지 않은 어빌리티는
    /// 활성화된 그 틱에 끝난다. 활성 상태로 남으려면 매 틱 호출되는 <see cref="OnTick"/>에서
    /// <see cref="GameplayAbilityTickResult.Running"/>을 직접 돌려주어야 하는데,
    /// 그러자면 "언제 그만둘지"를 바로 그 자리에서 판단할 수밖에 없다.
    /// 판단 조건과 재평가 주기가 어긋나 활성 상태가 영영 끝나지 않는 사고를 구조로 막으려는 설계이다.
    /// </para>
    /// <para>
    /// <b>바깥 자원은 <see cref="OnEnd"/>에서 놓아야 한다.</b> 엄폐 지점 예약처럼 어빌리티가 세계의 자원을 잡았다면
    /// 반드시 여기서 되돌린다. 스스로 끝나든, 취소당하든, 부여가 취소되든, 시스템이 통째로 정리되든
    /// <see cref="OnEnd"/>는 활성화 한 번에 정확히 한 번만 호출되므로 이 자리에서 놓으면 새지 않는다.
    /// 준비 단계에서 취소되어 OnActivate가 호출되지 않았다면 OnEnd도 호출되지 않는다.
    /// OnActivate 안에서 취소하면 콜백이 반환된 뒤 OnEnd가 호출되어 콜백의 나머지가 잡은 자원도 정리한다.
    /// </para>
    /// <para>
    /// 어빌리티는 부여될 때 한 번 만들어져 계속 재사용되므로, 활성화 사이에 남으면 안 되는 상태는
    /// <see cref="OnActivate"/>에서 초기화한다.
    /// </para>
    /// <para>
    /// <b>재정의할 때의 접근 한정자에 주의한다.</b> 수명주기 메서드는 시스템이 호출할 수 있도록
    /// <c>protected internal</c>로 선언되어 있으나, 다른 어셈블리에서 상속할 때는 <c>internal</c> 부분이 닿지 않으므로
    /// 재정의 선언은 <c>protected override</c>로 적어야 한다. 게임 코드는 대부분 다른 어셈블리에 있으므로 이쪽이 보통이다.
    /// </para>
    /// </remarks>
    public abstract class GameplayAbility
    {
        /// <summary>이 어빌리티의 정의이며 부여될 때 연결된다.</summary>
        public GameplayAbilityDefinition Definition { get; internal set; }

        /// <summary>이 어빌리티를 보유한 시스템이며 부여될 때 연결된다.</summary>
        public GameplayAbilitySystem System { get; internal set; }

        /// <summary>같은 인스턴스를 거두었다 다시 부여한 경우도 구분하는 이번 부여의 표식이다.</summary>
        internal object GrantToken { get; set; }

        /// <summary>지금 활성 중인지 여부이다.</summary>
        public bool IsActive { get; internal set; }

        /// <summary>이번 활성화가 시작된 뒤 흐른 시간(초)이다.</summary>
        public float ActiveTime { get; internal set; }

        /// <summary>
        /// 지금까지 활성화된 횟수이다.
        /// 활성화 하나를 지켜보는 쪽이 "내가 시작한 그 활성화가 맞는지"를 확인하는 데 쓴다.
        /// 끝난 뒤 다른 곳에서 다시 활성화되면 이 값이 달라지므로, 남의 활성화를 자기 것으로 착각하지 않는다.
        /// </summary>
        public int ActivationCount { get; internal set; }

        /// <summary>
        /// 마지막으로 종료된 사유이며, 한 번도 활성화된 적이 없으면 <see cref="GameplayAbilityEndReason.Completed"/>이다.
        /// 스스로 끝난 것과 취소당한 것을 나중에 구분해야 하는 관찰자를 위해 남겨 둔다.
        /// </summary>
        public GameplayAbilityEndReason LastEndReason { get; internal set; }

        /// <summary>
        /// 이번 활성화를 일으킨 게임플레이 이벤트이며, 이벤트로 시작되지 않았으면 <see cref="GameplayEventData.None"/>이다.
        /// 사망 이벤트의 가해자처럼 이벤트가 실어 온 것을 <see cref="OnActivate"/>에서 읽는 자리이다.
        /// </summary>
        public GameplayEventData TriggeringEvent { get; internal set; }

        /// <summary>
        /// 활성화 조건을 어빌리티 스스로 확인한다.
        /// 태그와 코스트, 쿨다운은 시스템이 이미 검사하므로 그 밖의 조건만 여기서 본다.
        /// </summary>
        /// <returns>활성화해도 되면 true이다.</returns>
        public virtual bool CanActivate()
        {
            return true;
        }

        /// <summary>
        /// 활성화된 직후 한 번 호출된다. 활성화 사이에 남으면 안 되는 상태를 여기서 초기화한다.
        /// </summary>
        protected internal virtual void OnActivate()
        {
        }

        /// <summary>
        /// 활성 중인 동안 매 틱 호출된다.
        /// </summary>
        /// <remarks>
        /// 계속 진행하려면 <see cref="GameplayAbilityTickResult.Running"/>을 돌려주되,
        /// 그때는 언제 그만둘지를 반드시 이 메서드 안에서 판단해야 한다.
        /// 기본 구현은 곧바로 끝내므로, 재정의하지 않으면 활성 상태가 남지 않는다.
        /// </remarks>
        /// <param name="deltaTime">흐른 시간(초)이다.</param>
        /// <returns>계속할지 끝낼지를 나타내는 결과이다.</returns>
        protected internal virtual GameplayAbilityTickResult OnTick(float deltaTime)
        {
            return GameplayAbilityTickResult.Finished;
        }

        /// <summary>
        /// 종료될 때 한 번 호출된다. 잡아 둔 바깥 자원을 여기서 놓는다.
        /// </summary>
        /// <param name="endReason">스스로 끝났는지 취소당했는지이다.</param>
        protected internal virtual void OnEnd(GameplayAbilityEndReason endReason)
        {
        }

        /// <summary>
        /// 활성 중인 자신을 끝낸다. 어빌리티가 자기 판단으로 즉시 종료할 때 사용한다.
        /// </summary>
        /// <returns>실제로 종료했으면 true이다.</returns>
        protected bool EndSelf()
        {
            return IsActive && System != null && System.EndAbility(this, GameplayAbilityEndReason.Completed);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var tagText = Definition != null ? Definition.AbilityTag.Name : "None";
            return IsActive ? $"{tagText} (활성 {ActiveTime:0.##}초)" : $"{tagText} (대기)";
        }
    }
}
