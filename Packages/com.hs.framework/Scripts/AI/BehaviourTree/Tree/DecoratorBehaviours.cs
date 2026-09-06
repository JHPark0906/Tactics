using System;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Foundation.Collections;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>자식 하나를 감싸 그 결과를 바꾸는 자리의 공통 부분이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>자식을 들고 있지 않다.</b> 감쌀 자식은 트리에서 자기 아래 첫 자식이며, 그것이 없으면
    /// 감쌀 것이 없으므로 실패한다. 편집기에서 자식을 떼어 낸 상태가 조용히 성공으로 읽히면
    /// 트리가 왜 그렇게 도는지 알 수 없다.
    /// </para>
    /// <para>
    /// <b>자식이 도는 중이었는지는 기억한다.</b> 문이 닫힐 때 아래가 돌고 있었다면 그것을 처음으로
    /// 되돌려야 한다. 되돌리지 않으면 이동 중이던 자리는 계속 걷고, 자리를 잡아 둔 자리는 그것을
    /// 영영 쥐고 있다. 트리는 문이 닫힌 것을 실패로만 보므로, 아래를 되돌리는 일은 문이 맡는다.
    /// </para>
    /// <para>
    /// 그래서 <see cref="Reset"/>은 덮어쓸 수 없고, 자기 진행을 지울 것이 있는 자리는
    /// <see cref="OnReset"/>을 덮어쓴다. 기억을 지우는 일이 빠지면 닫힌 문이 이미 되돌려진 자식을
    /// 한 번 더 되돌린다.
    /// </para>
    /// </remarks>
    public abstract class DecoratorBehaviour : IBehaviour
    {
        private bool _isChildRunning;

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            var children = context.Children;
            if (children.Count == 0)
            {
                return BehaviourStatus.Failure;
            }

            var child = children[0];
            if (!CanEnter(context))
            {
                StopRunningChild(context, child);
                return BehaviourStatus.Failure;
            }

            OnBeforeChildTick(context);

            var childStatus = context.TickChild(child);
            _isChildRunning = childStatus == BehaviourStatus.Running;
            return Decorate(childStatus, context);
        }

        /// <summary>문이 닫혔을 때 도는 중이던 자식을 처음으로 되돌린다.</summary>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <param name="child">감싼 자식이다.</param>
        private void StopRunningChild(in BehaviourTickContext context, TreeNode<IBehaviour> child)
        {
            if (!_isChildRunning)
            {
                return;
            }

            _isChildRunning = false;
            context.Tree.Reset(child);
        }

        /// <summary>자식을 실행해도 되는지 본다.</summary>
        /// <remarks>
        /// <b>거짓이면 자식을 아예 실행하지 않는다.</b> 조건이 막는다는 것은 그 아래를 시작조차
        /// 하지 않는다는 뜻이며, 실행한 뒤 결과만 바꾸는 것과 다르다. 아래가 돌고 있던 중이었다면
        /// 그것은 처음으로 되돌려진다.
        /// </remarks>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <returns>들어가도 되면 참이다.</returns>
        protected virtual bool CanEnter(in BehaviourTickContext context) => true;

        /// <summary>자식을 실행하기 직전에 할 일이 있으면 한다.</summary>
        /// <remarks>통과가 정해진 뒤에 부른다. 막힌 자리에서는 아무 일도 일어나지 않아야 한다.</remarks>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        protected virtual void OnBeforeChildTick(in BehaviourTickContext context)
        {
        }

        /// <summary>감싼 자식의 결과를 받아 이 자리의 결과로 바꾼다.</summary>
        /// <param name="childStatus">자식이 돌려준 결과이다.</param>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <returns>이 자리의 결과이다.</returns>
        protected abstract BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context);

        /// <inheritdoc />
        /// <remarks>자식이 돌고 있었다는 기억을 지운 뒤 <see cref="OnReset"/>을 부른다.</remarks>
        public void Reset()
        {
            _isChildRunning = false;
            OnReset();
        }

        /// <summary>자기 자리의 진행을 처음으로 되돌린다.</summary>
        /// <remarks>
        /// 되돌릴 진행이 없는 자리는 덮어쓰지 않아도 된다. 아래 가지는 트리가 되돌리므로 여기서
        /// 자식을 건드리지 않는다.
        /// </remarks>
        protected virtual void OnReset()
        {
        }
    }

    /// <summary>자식의 성공과 실패를 뒤집는다.</summary>
    /// <remarks>진행 중은 그대로 둔다. 아직 끝나지 않은 것에는 뒤집을 결과가 없다.</remarks>
    public sealed class InverterBehaviour : DecoratorBehaviour
    {
        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus switch
            {
                BehaviourStatus.Success => BehaviourStatus.Failure,
                BehaviourStatus.Failure => BehaviourStatus.Success,
                _ => BehaviourStatus.Running
            };
    }

    /// <summary>자식이 끝나기만 하면 성공으로 바꾼다.</summary>
    /// <remarks>실패해도 위로는 성공이 올라가므로, 실패가 형제의 차례를 앞당기지 않게 할 때 쓴다.</remarks>
    public sealed class SucceederBehaviour : DecoratorBehaviour
    {
        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus == BehaviourStatus.Running ? BehaviourStatus.Running : BehaviourStatus.Success;
    }

    /// <summary>자식을 정해진 횟수만큼, 또는 끝없이 되풀이한다.</summary>
    public sealed class RepeaterBehaviour : DecoratorBehaviour
    {
        /// <summary>끝없이 되풀이함을 뜻하는 횟수이다.</summary>
        public const int InfiniteRepeats = 0;

        private readonly int _repeatCount;
        private int _completedCount;

        /// <summary>되풀이 횟수를 지정한다.</summary>
        /// <param name="repeatCount">되풀이할 횟수이며, <see cref="InfiniteRepeats"/>이면 끝이 없다.</param>
        public RepeaterBehaviour(int repeatCount = InfiniteRepeats)
        {
            _repeatCount = Math.Max(InfiniteRepeats, repeatCount);
        }

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
        {
            if (childStatus == BehaviourStatus.Running)
            {
                return BehaviourStatus.Running;
            }

            context.Tree.Reset(context.Children[0]);

            if (_repeatCount == InfiniteRepeats)
            {
                return BehaviourStatus.Running;
            }

            _completedCount++;
            if (_completedCount < _repeatCount)
            {
                return BehaviourStatus.Running;
            }

            _completedCount = 0;
            return BehaviourStatus.Success;
        }

        /// <inheritdoc />
        protected override void OnReset() => _completedCount = 0;
    }

    /// <summary>조건이 참일 때만 아래를 실행하게 막아 서는 자리이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>조건은 잎이 아니라 문이다.</b> 조건을 하나의 자리로 두어 <c>Sequence(조건, 행동)</c>
    /// 처럼 늘어놓으면 조건과 행동이 형제가 되어 어느 조건이 어느 행동을 막는지 트리
    /// 모양만으로는 알 수 없다. 막는 자리를 위에 두면 그 관계가 보이고 트리도 한 단 얕아진다.
    /// </para>
    /// <para>
    /// 조건이 거짓이면 아래를 <b>실행하지 않고</b> 실패를 돌려준다. 실행한 뒤 결과만 뒤집는 것과
    /// 다르며, 아래가 부작용을 남기는 자리라면 그 차이가 드러난다.
    /// </para>
    /// </remarks>
    public sealed class ConditionBehaviour : DecoratorBehaviour
    {
        private readonly Func<IBehaviourContext, bool> _predicate;

        /// <summary>확인할 조건을 지정한다.</summary>
        /// <param name="predicate">문맥를 받아 참·거짓을 돌려주는 조건이다.</param>
        public ConditionBehaviour(Func<IBehaviourContext, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context) => _predicate(context.Context);

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus;
    }

    /// <summary>문맥의 그 키에 값이 있을 때만 아래를 실행하게 한다.</summary>
    /// <remarks>
    /// 조건 가운데 가장 흔한 모양이며, 지켜볼 키가 무엇인지가 드러나 있다. 값이 생기거나 사라질 때
    /// 실행 중인 가지를 끊는 일은 <see cref="ObservingConditionBehaviour"/>가 맡는다.
    /// </remarks>
    public sealed class ContextValueConditionBehaviour : ObservingConditionBehaviour
    {
        /// <summary>지켜보는 키이다.</summary>
        public string Key { get; }

        /// <summary>값이 있어야 통과시킬지, 없어야 통과시킬지 여부이다.</summary>
        public bool RequiresValue { get; }

        /// <summary>지켜볼 키와 끊을 범위를 지정한다.</summary>
        /// <param name="key">확인할 문맥 키이다.</param>
        /// <param name="requiresValue">값이 있어야 통과시키면 참이고, 없어야 통과시키면 거짓이다.</param>
        /// <param name="abortScope">값이 바뀌었을 때 무엇을 끊을지이다.</param>
        public ContextValueConditionBehaviour(
            string key,
            bool requiresValue = true,
            BehaviourAbortScope abortScope = BehaviourAbortScope.None)
            : base(abortScope, RequireKey(key, nameof(key)))
        {
            Key = key;
            RequiresValue = requiresValue;
        }

        /// <inheritdoc />
        protected override bool Evaluate(in BehaviourTickContext context)
            => context.Context.TryGetValue<object>(Key, out _) == RequiresValue;
    }
}
