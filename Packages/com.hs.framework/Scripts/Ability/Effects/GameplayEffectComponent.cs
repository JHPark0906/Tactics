using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Tags;
using UnityEngine;
using VContainer;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// GameObject에 붙어 효과 실행기를 노출하고 매 프레임 시간을 흘려 준다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 규칙은 모두 <see cref="GameplayEffectRunner"/>에 있고 이 컴포넌트는 두 가지만 한다.
    /// 같은 오브젝트의 어트리뷰트와 태그를 실행기에 연결하는 것, 그리고 시간을 넣어 주는 것이다.
    /// 시간을 읽는 곳을 여기 한 군데로 몰아 두었기 때문에 규칙 계층은 시계 없이 검증할 수 있다.
    /// </para>
    /// <para>
    /// <b>시간을 흘리는 자리는 고를 수 있다.</b> 행동 트리 실행기와 같은 모양으로 <see cref="GameplayTickMode"/>를
    /// 가지며 기본은 <c>FixedUpdate</c>다. 쿨다운·지속·주기는 실시간 초를 세므로 어느 자리든 그 자리에 맞는 델타를
    /// 넣으면 같은 초가 흐른다. <c>Update</c>를 고르면 만료가 어느 프레임 경계에 걸리는지가 프레임률을 따라가
    /// 기기마다 결과가 달라질 수 있다. 어빌리티 시스템 컴포넌트가 같은 오브젝트에 있으면 그쪽 방식이 여기에도 적용된다.
    /// </para>
    /// <para>
    /// <b>값은 오직 프로젝트 설정(GameMode)에서만 온다.</b> 인스펙터 칸이 없고 「비어 있으면 기본값」 같은
    /// 우선순위도 없다. <see cref="InjectGameplayTickMode"/>가 주입 시점에 받아 반영하며, 주입되지 않는
    /// 자리는 코드에 적힌 기본값 <see cref="GameplayTickMode.OnFixedUpdate"/> 그대로 안전하게 동작한다.
    /// </para>
    /// <para>
    /// 태그 컴포넌트는 없어도 된다. 없으면 실행기가 자체 컨테이너를 만들어 쓰므로 어트리뷰트만 바꾸는 효과는
    /// 그대로 동작한다. 다만 태그를 부여하는 효과를 쓰려면 다른 시스템도 같은 컨테이너를 봐야 하므로
    /// <see cref="GameplayTagComponent"/>를 함께 붙이는 편이 좋다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AttributeSetComponent))]
    public sealed class GameplayEffectComponent : MonoBehaviour
    {
        [Tooltip("효과가 태그를 부여할 대상이다. 비워 두면 같은 오브젝트에서 찾고, 그래도 없으면 실행기가 자체 컨테이너를 쓴다.")]
        [SerializeField]
        private GameplayTagComponent tagComponent;

        [Tooltip("자동으로 실행기에 시간을 흘려 줄지 여부이다. 끄면 다른 코드가 Runner.Tick을 직접 호출해야 한다.")]
        [SerializeField]
        private bool tickAutomatically = true;

        /// <summary>
        /// 시간을 흘려 줄 자리이다. 인스펙터 칸이 아니다 — 값은 프로젝트 설정에서 주입받거나
        /// 같은 오브젝트의 어빌리티 시스템 컴포넌트가 <see cref="ConfigureTickMode"/>로 맞춘다.
        /// </summary>
        private GameplayTickMode tickMode = GameplayTickMode.OnFixedUpdate;

        private GameplayEffectRunner _runner;

        /// <summary>이 대상의 효과 실행기이며 처음 접근할 때 만들어진다.</summary>
        public GameplayEffectRunner Runner => _runner ??= CreateRunner();

        /// <summary>자동으로 시간을 흘리는지 여부이다.</summary>
        public bool TickAutomatically => tickAutomatically;

        /// <summary>자동으로 시간을 흘리는 자리이다.</summary>
        public GameplayTickMode TickMode => tickMode;

        /// <summary>시간을 흘리는 자리를 코드에서 정한다.</summary>
        /// <param name="mode">시간을 흘릴 자리이다.</param>
        public void ConfigureTickMode(GameplayTickMode mode)
        {
            tickMode = mode;
        }

        /// <summary>
        /// 프로젝트 설정(GameMode)에서 틱 방식을 받아 반영한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>어빌리티 시스템 컴포넌트가 없는 오브젝트를 위해 따로 받는다.</b> 이 컴포넌트는 어트리뷰트만
        /// 요구하고 어빌리티 시스템을 요구하지 않으므로, 체력만 있고 어빌리티가 없는 액터도 존재한다.
        /// 그런 오브젝트는 <c>GameplayAbilitySystemComponent.AlignEffectComponentTickMode</c>가 맞춰 줄
        /// 상대가 없으므로 이 메서드로 스스로 받아야 한다. 어빌리티 시스템이 함께 있으면 둘 다 같은 프로젝트
        /// 설정에서 같은 값을 받으므로 어느 쪽이 먼저 불려도 결과는 같다.
        /// </para>
        /// </remarks>
        /// <param name="tickModeSource">틱 방식을 아는 프로젝트 설정이다.</param>
        [Inject]
        public void InjectGameplayTickMode(IGameplayTickModeSource tickModeSource)
        {
            if (tickModeSource != null)
            {
                ConfigureTickMode(tickModeSource.GameplayTickMode);
            }
        }

        /// <summary>
        /// 매 프레임 자동 틱을 끈다. 이후에는 바깥 코드가 <see cref="GameplayEffectRunner.Tick"/>을 직접 불러야 시간이 흐른다.
        /// 고정 스텝 위에서 도는 액터가 효과의 시계를 자기 스텝에 맞출 때 쓴다.
        /// </summary>
        public void ConfigureManualTick()
        {
            tickAutomatically = false;
        }

        /// <summary>
        /// 효과를 이 대상에게 적용한다.
        /// </summary>
        /// <param name="definition">적용할 효과 정의이다.</param>
        /// <param name="source">이 효과를 적용한 원인이다.</param>
        /// <returns>유지되기 시작한 효과 기록이며, 즉시 효과이거나 적용하지 못했으면 null이다.</returns>
        public ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition definition, object source = null)
        {
            return Runner.Apply(definition, source);
        }

        /// <summary>
        /// 적용하는 순간의 사정을 밝혀 효과를 이 대상에게 적용한다.
        /// </summary>
        /// <param name="definition">적용할 효과 정의이다.</param>
        /// <param name="source">이 효과를 적용한 원인이다.</param>
        /// <param name="context">적용하는 순간의 사정이며 없으면 null이다.</param>
        /// <returns>유지되기 시작한 효과 기록이며, 즉시 효과이거나 적용하지 못했으면 null이다.</returns>
        public ActiveGameplayEffect ApplyEffect(
            GameplayEffectDefinition definition,
            object source,
            GameplayEffectContext context)
        {
            return Runner.Apply(definition, source, context);
        }

        private void Update()
        {
            if (tickAutomatically && tickMode == GameplayTickMode.OnUpdate)
            {
                Runner.Tick(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (tickAutomatically && tickMode == GameplayTickMode.OnFixedUpdate)
            {
                Runner.Tick(Time.fixedDeltaTime);
            }
        }

        private void OnDestroy()
        {
            // 유지 중인 효과를 되돌리고 태그 구독도 함께 놓는다. 참조는 지우지 않는다.
            _runner?.Dispose();
        }

        /// <summary>
        /// 연출 계층으로 큐를 보낼 디스패처를 연결한다. 조립하는 쪽이 액터마다 부르며, 연결하지 않으면 큐를 보내지 않는다.
        /// </summary>
        /// <param name="dispatcher">연결할 디스패처이며 null이면 연결을 푼다.</param>
        public void ConfigureCueDispatcher(GameplayCueDispatcher dispatcher)
        {
            Runner.CueDispatcher = dispatcher;
        }

        /// <summary>같은 오브젝트의 어트리뷰트와 태그를 묶어 실행기를 만든다.</summary>
        /// <returns>만든 효과 실행기이다.</returns>
        private GameplayEffectRunner CreateRunner()
        {
            var attributeSetComponent = GetComponent<AttributeSetComponent>();
            if (tagComponent == null)
            {
                tagComponent = GetComponent<GameplayTagComponent>();
            }

            return new GameplayEffectRunner(
                attributeSetComponent.Attributes,
                tagComponent != null ? tagComponent.Container : null)
            {
                CueTarget = gameObject
            };
        }
    }
}
