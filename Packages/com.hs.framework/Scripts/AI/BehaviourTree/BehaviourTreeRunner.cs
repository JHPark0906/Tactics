using HS.Framework.AI.Behaviour;
using HS.Framework.Ability;
using System;
using UnityEngine;
using VContainer;

namespace HS.Framework.AI.BehaviourTree
{
    /// <summary>
    /// 행동 트리의 루트 노드를 일정 주기로 실행하는 Unity 컴포넌트의 확장 지점이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>기본은 <c>FixedUpdate</c>이다.</b> 로직은 고정 주기에서 돌고 그림은 매 프레임 그린다는 것이
    /// 이 프로젝트의 규칙이므로, 트리도 그 주기를 따른다. 프레임률이 높아져도 판단 횟수가 늘지 않는다.
    /// </para>
    /// <para>
    /// <b><c>Update</c>로도 돌릴 수 있다.</b> 입력에 곧바로 반응해야 하는 트리나, 고정 주기를 쓰지 않는
    /// 프로젝트를 위해 남겨 두었다. 그 경로에서는 판단 횟수가 프레임률을 따라가므로 <b>같은 상황이 기기마다
    /// 다른 결과로 갈릴 수 있다.</b> 결과가 기기의 성능에 따라 달라지면 안 되는 게임은 고르지 않는다.
    /// 어느 자리로 돌릴지는 어빌리티 시스템의 틱 방식과 같은 모양(<see cref="GameplayTickMode"/>)이며,
    /// <b>프로젝트 설정(GameMode)이 정한다.</b> 인스펙터 칸이 없고, 「비어 있으면 기본값」 같은 우선순위도
    /// 없다. <see cref="InjectGameplayTickMode"/>가 주입 시점에 받아 <see cref="ConfigureTickMode"/>로
    /// 반영하며, 주입되지 않는 자리(검사 등)는 코드에 적힌 기본값 <see cref="GameplayTickMode.OnFixedUpdate"/>
    /// 그대로 안전하게 돈다.
    /// </para>
    /// <para>
    /// <b>틱 간격의 뜻은 그대로다.</b> <c>Tick Interval</c>은 "틱 사이의 최소 간격(초)"이며
    /// 0이면 매 번 실행한다.
    /// </para>
    /// <para>
    /// <b>언제 돌려도 되는지는 바깥이 정한다.</b> 배치 단계, 연출, 멈춤처럼 트리를 잠시 돌리지 말아야 할
    /// 까닭은 트리가 아니라 그것을 둔 쪽이 안다. 그래서 <see cref="SetTickGate"/>로 물음을 걸어 두면
    /// 틱마다 그것에 묻는다. 값을 받아 두지 않고 물음을 받는 것은, 값을 받아 두면 그것을 갱신하는 자리가
    /// 또 하나 생기고 그 자리가 빠지면 문이 실제와 어긋나기 때문이다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BehaviourTreeRunner : MonoBehaviour
    {
        [Tooltip("틱 사이의 최소 간격(초)이다. 0이면 매 스텝 실행한다.")]
        [SerializeField]
        [Min(0f)]
        private float tickInterval;

        /// <summary>
        /// 트리를 돌릴 자리이다. 인스펙터 칸이 아니다 — 값은 프로젝트 설정에서 주입받거나
        /// <see cref="ConfigureTickMode"/>로만 바뀐다. 아무것도 주입되지 않으면 이 기본값 그대로 안전하게 돈다.
        /// </summary>
        private GameplayTickMode tickMode = GameplayTickMode.OnFixedUpdate;

        private IBehaviourContext _context;
        private BehaviourTreeInstance _tree;
        private float _elapsedSinceLastTick;
        private Func<bool> _tickGate;

        /// <summary>
        /// 루트 노드와 컨텍스트가 설정되어 실행 가능한지 여부이다.
        /// </summary>
        public bool IsInitialized => _tree != null && _context != null;

        /// <summary>지금 틱을 해도 되는지 여부이다. 문을 걸지 않았으면 언제나 참이다.</summary>
        public bool IsTickAllowed => _tickGate == null || _tickGate();

        /// <summary>
        /// 마지막 틱의 실행 결과이다. 아직 틱이 실행되지 않았으면 Failure이다.
        /// </summary>
        public BehaviourStatus LastStatus { get; private set; } = BehaviourStatus.Failure;

        /// <inheritdoc />
        /// <remarks>
        /// 루프가 정해 준 시간만 쌓아 간격을 재므로 프레임 시간이 끼어들지 않는다.
        /// 문이 닫혀 있는 동안은 간격도 쌓지 않으므로, 문이 열리는 스텝에 곧바로 한 번 돈다.
        /// </remarks>
        public void Tick(float deltaTime)
        {
            if (!IsInitialized || !IsTickAllowed)
            {
                return;
            }

            _elapsedSinceLastTick += deltaTime;
            if (_elapsedSinceLastTick < tickInterval)
            {
                return;
            }

            _elapsedSinceLastTick = 0f;
            Tick();
        }

        /// <summary>
        /// 실행할 루트 노드와 공유 컨텍스트를 설정한다.
        /// </summary>
        public void Initialize(BehaviourTreeInstance tree, IBehaviourContext context)
        {
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tree.Reset();
            _elapsedSinceLastTick = tickInterval;
            LastStatus = BehaviourStatus.Failure;
        }

        /// <summary>틱마다 물을 문을 건다.</summary>
        /// <remarks>
        /// <para>
        /// 문은 값이 아니라 <b>물을 때마다 셈하는 물음</b>이다. 트리를 돌려도 되는지는 그것을 둔 쪽의
        /// 상태(배치 단계, 연출, 멈춤)에서 나오므로 여기서 따로 들고 있지 않는다.
        /// </para>
        /// <para>
        /// 문이 닫혀 있는 동안은 주기 틱도 명시적 <see cref="Tick()"/>도 트리를 돌리지 않고, 돈 것이 없으니
        /// 마지막 결과는 그대로다. 닫혀 있던 동안의 진행을 지우고 처음부터 돌릴지는 문을 건 쪽이
        /// <see cref="ResetTree"/>로 정한다.
        /// </para>
        /// </remarks>
        /// <param name="tickGate">틱해도 되면 참을 돌려주는 물음이며, null이면 문을 뗀다.</param>
        public void SetTickGate(Func<bool> tickGate) => _tickGate = tickGate;

        /// <summary>트리를 돌릴 자리이다.</summary>
        public GameplayTickMode TickMode => tickMode;

        /// <summary>트리를 돌릴 자리를 코드에서 정한다.</summary>
        /// <param name="mode">돌릴 자리이다.</param>
        public void ConfigureTickMode(GameplayTickMode mode) => tickMode = mode;

        /// <summary>
        /// 프로젝트 설정(GameMode)에서 틱 방식을 받아 반영한다.
        /// </summary>
        /// <remarks>
        /// <b>Awake가 아니라 여기서 반영한다.</b> 씬에 놓인 오브젝트는 <c>FrameworkInitializer</c>가 씬
        /// 로드 뒤에 주입하므로 이 메서드가 Awake보다 늦게 불리지만, Start와 첫 틱보다는 이르다. 실행 중
        /// 스폰된 오브젝트는 비활성 상태에서 먼저 주입을 받으므로 이 메서드가 Awake보다 먼저 불린다.
        /// 어느 경우든 이 컴포넌트에는 Awake가 따로 없으므로(초기화는 <see cref="Initialize"/>가 한다)
        /// 두 순서 모두 첫 틱 전에 값이 반영된다.
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
        /// 행동 트리의 다음 틱을 실행한다. 문이 닫혀 있으면 돌리지 않고 마지막 결과를 그대로 돌려준다.
        /// </summary>
        public BehaviourStatus Tick()
        {
            if (!IsInitialized)
            {
                LastStatus = BehaviourStatus.Failure;
                return LastStatus;
            }

            if (!IsTickAllowed)
            {
                return LastStatus;
            }

            LastStatus = _tree.Tick(_context);
            return LastStatus;
        }

        /// <summary>
        /// 루트 노드부터 트리 전체의 진행 상태를 초기화한다. 되돌린 뒤 첫 틱은 간격을 기다리지 않는다.
        /// </summary>
        public void ResetTree()
        {
            _tree?.Reset();
            _elapsedSinceLastTick = tickInterval;
            LastStatus = BehaviourStatus.Failure;
        }

        private void Update()
        {
            if (tickMode == GameplayTickMode.OnUpdate)
            {
                Tick(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (tickMode == GameplayTickMode.OnFixedUpdate)
            {
                Tick(Time.fixedDeltaTime);
            }
        }
    }
}
