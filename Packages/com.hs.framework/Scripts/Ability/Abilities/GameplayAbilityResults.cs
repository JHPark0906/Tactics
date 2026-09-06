namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// 어빌리티 활성화를 시도한 결과이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>실패 사유를 뭉뚱그리지 않는 이유가 있다.</b> 이 결과를 읽는 쪽은 사람이 아니라 상위 계층의 판단 코드이며,
    /// 특히 <see cref="NotGranted"/>는 다른 실패와 성격이 완전히 다르다. 자원이 모자라거나 쿨다운이 도는 것은
    /// "지금은 안 되지만 나중에 될 수 있다"이고, 부여되지 않은 것은 "이 액터에게는 애초에 없는 능력"이다.
    /// 뒤엣것을 앞엣것처럼 다루면 그 능력을 영영 기다리며 멈춰 있게 된다.
    /// </para>
    /// <para>
    /// 그래서 시도가 실패했을 때 예외를 던지거나 진행 중을 답하지 않고 반드시 이 결과를 돌려준다.
    /// 상위 계층은 <see cref="NotGranted"/>를 보면 그 선택지를 건너뛰고 다음으로 넘어가면 된다.
    /// </para>
    /// </remarks>
    public enum GameplayAbilityActivationResult
    {
        /// <summary>활성화했다.</summary>
        Success = 0,

        /// <summary>
        /// 이 액터에게 부여되지 않은 어빌리티라 시도할 수 없다.
        /// 기다린다고 달라지지 않으므로 호출한 쪽은 즉시 다른 선택지로 넘어가야 한다.
        /// </summary>
        NotGranted = 1,

        /// <summary>같은 어빌리티가 활성 중이거나 활성화·종료 콜백을 처리하고 있다.</summary>
        AlreadyActive = 2,

        /// <summary>대상이 필요한 태그를 갖추지 못했다.</summary>
        MissingRequiredTags = 3,

        /// <summary>대상이 이 어빌리티를 막는 태그를 가지고 있다.</summary>
        BlockedByTags = 4,

        /// <summary>쿨다운 효과가 부여한 태그가 아직 남아 있다.</summary>
        OnCooldown = 5,

        /// <summary>코스트를 낼 자원이 모자란다.</summary>
        CostNotAffordable = 6,

        /// <summary>어빌리티가 자기 조건으로 활성화를 거부했다.</summary>
        Rejected = 7,

        /// <summary>활성 중인 다른 어빌리티가 이 어빌리티의 태그를 차단하고 있다.</summary>
        BlockedByActiveAbility = 8,

        /// <summary>코스트 효과가 조건에 막혔거나 적용 중 취소되었다.</summary>
        CostApplicationFailed = 9,

        /// <summary>쿨다운 효과를 적용해 재사용을 막는 상태를 만들지 못했다.</summary>
        CooldownApplicationFailed = 10,

        /// <summary>준비 콜백에서 취소되어 사용자 어빌리티를 시작하지 않았다.</summary>
        Cancelled = 11
    }

    /// <summary>
    /// 활성 중인 어빌리티가 한 틱을 처리한 뒤의 상태이다.
    /// </summary>
    /// <remarks>
    /// <b>진행 중을 답하려면 그만둘 조건을 이 자리에서 평가해야 한다.</b>
    /// 이 결과를 돌려주는 곳이 매 틱 호출되는 자리이므로, 종료 조건을 여기서 판단하는 한
    /// 조건과 평가 주기가 저절로 짝을 이룬다. 기본 구현이 <see cref="Finished"/>를 돌려주는 것도 같은 이유로,
    /// 아무것도 하지 않은 어빌리티가 활성 상태로 남는 일이 없게 하기 위함이다.
    /// </remarks>
    public enum GameplayAbilityTickResult
    {
        /// <summary>아직 진행 중이며 다음 틱에 다시 판단한다.</summary>
        Running = 0,

        /// <summary>할 일이 끝났으므로 종료한다.</summary>
        Finished = 1
    }

    /// <summary>
    /// 어빌리티가 어떻게 끝났는지 나타낸다.
    /// </summary>
    public enum GameplayAbilityEndReason
    {
        /// <summary>어빌리티가 스스로 할 일을 마쳤다.</summary>
        Completed = 0,

        /// <summary>바깥에서 취소했다.</summary>
        Cancelled = 1
    }

    /// <summary>
    /// 어빌리티 시스템에서 일어난 변화의 종류이다.
    /// </summary>
    public enum GameplayAbilityChangeKind
    {
        /// <summary>어빌리티가 부여되었다.</summary>
        Granted = 0,

        /// <summary>어빌리티 부여가 취소되었다.</summary>
        Revoked = 1,

        /// <summary>어빌리티가 활성화되었다.</summary>
        Activated = 2,

        /// <summary>어빌리티가 종료되었다.</summary>
        Ended = 3
    }

    /// <summary>
    /// 어빌리티 시스템의 변화를 알리는 값이다.
    /// </summary>
    public readonly struct GameplayAbilityChange
    {
        /// <summary>변화가 일어난 어빌리티 정의이다.</summary>
        public GameplayAbilityDefinition Definition { get; }

        /// <summary>일어난 변화의 종류이다.</summary>
        public GameplayAbilityChangeKind ChangeKind { get; }

        /// <summary>종료 변화일 때 어떻게 끝났는지이며, 다른 변화에서는 의미가 없다.</summary>
        public GameplayAbilityEndReason EndReason { get; }

        /// <summary>어빌리티 변화 알림을 생성한다.</summary>
        /// <param name="definition">변화가 일어난 어빌리티 정의이다.</param>
        /// <param name="changeKind">일어난 변화의 종류이다.</param>
        /// <param name="endReason">종료 변화일 때의 종료 사유이다.</param>
        public GameplayAbilityChange(
            GameplayAbilityDefinition definition,
            GameplayAbilityChangeKind changeKind,
            GameplayAbilityEndReason endReason = GameplayAbilityEndReason.Completed)
        {
            Definition = definition;
            ChangeKind = changeKind;
            EndReason = endReason;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ChangeKind == GameplayAbilityChangeKind.Ended
                ? $"{(Definition != null ? Definition.name : "None")} {ChangeKind}({EndReason})"
                : $"{(Definition != null ? Definition.name : "None")} {ChangeKind}";
        }
    }
}
