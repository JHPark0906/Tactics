using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using UnityEngine;
using VContainer;

namespace HS.Framework.Ability.Abilities
{
    /// <summary>
    /// GameObject에 붙어 어빌리티 시스템을 노출하고 매 프레임 시간을 흘려 준다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 규칙은 <see cref="GameplayAbilitySystem"/>에 있고 이 컴포넌트는 연결과 시간 공급만 한다.
    /// 효과 실행기를 통해 어트리뷰트와 태그가 이미 이어져 있으므로 여기서 따로 묶을 것은 없다.
    /// </para>
    /// <para>
    /// 어빌리티 집합을 지정해 두면 시작할 때 부여한다. 스폰하는 쪽이 시점을 통제하고 싶으면
    /// 자동 부여를 끄고 <see cref="GrantAbilitySet"/>을 직접 호출한다.
    /// 인스펙터 플래그를 코드에서 돌리는 통로는 <see cref="ConfigureManualControl"/>이며,
    /// 스폰 직후 정의를 받아 조립하는 액터가 부여 시점과 시계를 스스로 정할 때 쓴다.
    /// </para>
    /// <para>
    /// <b>시간을 흘리는 자리는 고를 수 있다.</b> 행동 트리 실행기와 같은 모양으로 <see cref="GameplayTickMode"/>를
    /// 가지며 기본은 <c>FixedUpdate</c>다. 프레임률이 결과를 바꾸지 않는 쪽이 기본이어야 모르고 두어도 안전하기
    /// 때문이다. <c>Update</c>를 고르면 판단 횟수와 만료 시점이 프레임률을 따라가 기기마다 결과가 달라질 수 있다.
    /// 같은 오브젝트의 효과 실행기 컴포넌트는 이 컴포넌트의 방식을 따른다 — 어빌리티와 효과가 다른 시계를 보면
    /// 사격 간격처럼 효과가 세는 시간과 어빌리티가 판단하는 시간이 어긋난다.
    /// </para>
    /// <para>
    /// <b>값은 오직 프로젝트 설정(GameMode)에서만 온다.</b> 인스펙터 칸이 없으므로 프리팹마다 다르게 고를 수
    /// 없고, 「비어 있으면 기본값」 같은 우선순위도 없다. <see cref="InjectGameplayTickMode"/>가 주입 시점에
    /// 받아 <see cref="ConfigureTickMode"/>로 반영하며, 주입되지 않는 자리(검사 등)는 코드에 적힌 기본값
    /// <see cref="GameplayTickMode.OnFixedUpdate"/> 그대로 안전하게 동작한다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayEffectComponent))]
    public sealed class GameplayAbilitySystemComponent : MonoBehaviour
    {
        [Tooltip("시작할 때 부여할 어빌리티 집합이다. 비워 두면 아무것도 부여하지 않는다.")]
        [SerializeField]
        private GameplayAbilitySet abilitySet;

        [Tooltip("시작할 때 위 집합을 자동으로 부여할지 여부이다. 끄면 스폰한 쪽이 직접 부여해야 한다.")]
        [SerializeField]
        private bool grantOnAwake = true;

        [Tooltip("자동으로 시스템에 시간을 흘려 줄지 여부이다. 끄면 다른 코드가 System.Tick을 직접 호출해야 한다.")]
        [SerializeField]
        private bool tickAutomatically = true;

        /// <summary>
        /// 시간을 흘려 줄 자리이다. 인스펙터 칸이 아니다 — 값은 프로젝트 설정에서 주입받거나
        /// <see cref="ConfigureTickMode"/>로만 바뀐다. 아무것도 주입되지 않으면 이 기본값 그대로 안전하게 돈다.
        /// </summary>
        private GameplayTickMode tickMode = GameplayTickMode.OnFixedUpdate;

        private GameplayAbilitySystem _system;
        private bool _hasGrantedAbilitySet;

        /// <summary>이 대상의 어빌리티 시스템이며 처음 접근할 때 만들어진다.</summary>
        public GameplayAbilitySystem System => _system ??= CreateSystem();

        /// <summary>지정된 어빌리티 집합을 이미 부여했는지 여부이다.</summary>
        public bool HasGrantedAbilitySet => _hasGrantedAbilitySet;

        /// <summary>시작할 때 지정된 집합을 자동으로 부여하는지 여부이다.</summary>
        public bool GrantOnAwake => grantOnAwake;

        /// <summary>자동으로 시간을 흘리는지 여부이다.</summary>
        public bool TickAutomatically => tickAutomatically;

        /// <summary>자동으로 시간을 흘리는 자리이다.</summary>
        public GameplayTickMode TickMode => tickMode;

        /// <summary>
        /// 시간을 흘리는 자리를 코드에서 정한다. 같은 오브젝트의 효과 실행기 컴포넌트도 같은 자리로 맞춘다.
        /// </summary>
        /// <param name="mode">시간을 흘릴 자리이다.</param>
        public void ConfigureTickMode(GameplayTickMode mode)
        {
            tickMode = mode;
            AlignEffectComponentTickMode();
        }

        /// <summary>
        /// 프로젝트 설정(GameMode)에서 틱 방식을 받아 반영한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Awake가 아니라 여기서 반영하는 이유는 주입 시점이 씬 오브젝트와 스폰된 오브젝트에서 다르기
        /// 때문이다.</b> 씬에 놓인 오브젝트는 <c>FrameworkInitializer</c>가 씬 로드 뒤에 주입하므로 이 메서드가
        /// Awake보다 <b>늦게</b> 불린다 — 그래도 Start와 첫 틱보다는 이르다. 실행 중 스폰된 오브젝트는
        /// 비활성 상태에서 먼저 주입을 받으므로(<c>UnitSpawner</c> 참고) 이 메서드가 Awake보다 <b>먼저</b> 불린다.
        /// Awake에서 한 번 읽고 끝내면 앞의 경우 낡은 기본값이 첫 틱까지 남는다.
        /// </para>
        /// <para>
        /// 프로젝트 설정이 등록되지 않은 자리(검사, 컨테이너 없는 조립)에서는 이 메서드 자체가 불리지 않으므로
        /// 코드에 적힌 기본값 그대로 남는다.
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
        /// 부여와 시간 공급을 바깥 코드가 맡도록 돌린다.
        /// 시작 시 자동 부여와 매 프레임 자동 틱을 끄고, 같은 오브젝트의 효과 실행기 컴포넌트의 자동 틱도 함께 끈다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 두 플래그는 인스펙터 값이라 프리팹마다 따로 맞춰야 하고, 빠뜨리면 부여가 두 번 되거나 시간이 두 시계로 흐른다.
        /// 조립 코드가 이 한 호출로 돌리면 프리팹 설정과 무관하게 같은 결과가 난다.
        /// </para>
        /// <para>
        /// 효과 실행기를 함께 돌리는 것은 어빌리티와 효과가 같은 시계를 봐야 하기 때문이다.
        /// 한쪽만 고정 스텝으로 옮기면 사격 간격처럼 효과가 세는 시간과 어빌리티가 판단하는 시간이 어긋난다.
        /// </para>
        /// <para>
        /// 이미 Awake가 지나 자동 부여가 일어난 뒤라면 되돌리지 않는다. 그 경우는 <see cref="HasGrantedAbilitySet"/>가 참이다.
        /// </para>
        /// </remarks>
        public void ConfigureManualControl()
        {
            grantOnAwake = false;
            tickAutomatically = false;
            if (TryGetComponent<GameplayEffectComponent>(out var effectComponent))
            {
                effectComponent.ConfigureManualTick();
            }
        }

        /// <summary>
        /// 지정된 어빌리티 집합을 부여한다. 여러 번 호출해도 처음 한 번만 부여한다.
        /// </summary>
        /// <returns>실제로 부여한 어빌리티 수이다.</returns>
        public int GrantAbilitySet()
        {
            if (_hasGrantedAbilitySet || abilitySet == null)
            {
                return 0;
            }

            _hasGrantedAbilitySet = true;
            return abilitySet.GrantTo(System);
        }

        /// <summary>
        /// 태그로 어빌리티 활성화를 시도한다.
        /// </summary>
        /// <param name="abilityTag">활성화할 어빌리티의 식별 태그이다.</param>
        /// <returns>활성화 결과이며, 부여되지 않은 어빌리티는 그 사실을 구분해 답한다.</returns>
        public GameplayAbilityActivationResult TryActivate(GameplayTag abilityTag)
        {
            return System.TryActivate(abilityTag);
        }

        /// <summary>
        /// 게임플레이 이벤트를 이 대상의 어빌리티 시스템에 보낸다.
        /// </summary>
        /// <param name="eventTag">이벤트 종류를 나타내는 태그이다.</param>
        /// <param name="payload">이벤트가 실어 나르는 것이며 없으면 null이다.</param>
        /// <param name="magnitude">이벤트에 딸린 크기이며 없으면 0이다.</param>
        /// <returns>이 이벤트로 실제로 활성화된 어빌리티 수이다.</returns>
        public int SendGameplayEvent(GameplayTag eventTag, object payload = null, float magnitude = 0f)
        {
            return System.SendGameplayEvent(eventTag, payload, magnitude);
        }

        private void Awake()
        {
            AlignEffectComponentTickMode();
            if (grantOnAwake)
            {
                GrantAbilitySet();
            }
        }

        private void Update()
        {
            if (tickAutomatically && tickMode == GameplayTickMode.OnUpdate)
            {
                System.Tick(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (tickAutomatically && tickMode == GameplayTickMode.OnFixedUpdate)
            {
                System.Tick(Time.fixedDeltaTime);
            }
        }

        /// <summary>같은 오브젝트의 효과 실행기 컴포넌트가 이 컴포넌트와 같은 자리에서 시간을 흘리게 맞춘다.</summary>
        private void AlignEffectComponentTickMode()
        {
            if (TryGetComponent<GameplayEffectComponent>(out var effectComponent))
            {
                effectComponent.ConfigureTickMode(tickMode);
            }
        }

        private void OnDestroy()
        {
            // 활성 중인 어빌리티가 잡고 있던 바깥 자원을 파괴 시점에도 반드시 놓게 하고, 태그 구독도 함께 놓는다.
            // 참조는 지우지 않는다. 지우면 파괴된 뒤의 접근이 새 시스템을 만들어 사라진 실행기를 찾게 된다.
            _system?.Dispose();
        }

        /// <summary>같은 오브젝트의 효과 실행기를 묶어 어빌리티 시스템을 만든다.</summary>
        /// <returns>만든 어빌리티 시스템이다.</returns>
        private GameplayAbilitySystem CreateSystem()
        {
            return new GameplayAbilitySystem(GetComponent<GameplayEffectComponent>().Runner, gameObject);
        }
    }
}
