using HS.Framework.AI.BehaviourTree;
using System;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>자식을 순서대로 실행하며, 하나라도 실패하면 그 자리에서 실패한다.</summary>
    /// <remarks>
    /// 어디까지 갔는지를 기억했다가 다음 실행에서 그 자리부터 이어 간다. 실행 중이던 자식이
    /// 끝나기 전에 앞으로 되돌아가면 이미 끝난 일을 다시 하게 된다.
    /// </remarks>
    public sealed class SequenceBehaviour : IBehaviour
    {
        private int _currentIndex;

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            var children = context.Children;
            while (_currentIndex < children.Count)
            {
                var status = context.TickChild(children[_currentIndex]);
                if (status == BehaviourStatus.Running)
                {
                    return BehaviourStatus.Running;
                }

                if (status == BehaviourStatus.Failure)
                {
                    _currentIndex = 0;
                    return BehaviourStatus.Failure;
                }

                _currentIndex++;
            }

            _currentIndex = 0;
            return BehaviourStatus.Success;
        }

        /// <inheritdoc />
        public void Reset() => _currentIndex = 0;
    }

    /// <summary>자식을 순서대로 시도하며, 하나라도 성공하면 그 자리에서 성공한다.</summary>
    /// <remarks>
    /// <para>앞에 놓인 자식이 곧 높은 우선순위이다.</para>
    /// <para>
    /// <b>진행 중인 자식을 기억하며, 그 자식이 끝날 때까지 앞의 자식을 다시 보지 않는다.</b>
    /// 그래서 한 번 아래쪽 자식이 돌기 시작하면 위쪽의 조건이 참이 되어도 저절로 넘어가지 않는다.
    /// 예를 들어 위에 "사거리 안이면 멈춘다"를 두고 아래에 "쫓아간다"를 두면, 추격이 시작된 뒤에는
    /// 대상이 사거리 안으로 들어와도 멈추지 못하고 그대로 달라붙는다.
    /// </para>
    /// <para>
    /// <b>중간에 끊으려면 끊을 것을 명시해야 한다.</b> 지켜볼 값을 문맥에 쓰는 서비스를 두고,
    /// 그 값을 보는 조건 데코레이터(<see cref="ContextValueConditionBehaviour"/>)에
    /// <see cref="BehaviourAbortScope"/>를 지정한다. 그러면 값이 바뀌는 순간 진행 중인 가지가
    /// 끊기고 위쪽이 다시 평가된다. 우선순위만 매겨 두고 끊을 것을 지정하지 않으면 끊기지 않는다.
    /// </para>
    /// </remarks>
    public sealed class SelectorBehaviour : IBehaviour
    {
        private int _currentIndex;

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            var children = context.Children;
            while (_currentIndex < children.Count)
            {
                var status = context.TickChild(children[_currentIndex]);
                if (status == BehaviourStatus.Running)
                {
                    return BehaviourStatus.Running;
                }

                if (status == BehaviourStatus.Success)
                {
                    _currentIndex = 0;
                    return BehaviourStatus.Success;
                }

                _currentIndex++;
            }

            _currentIndex = 0;
            return BehaviourStatus.Failure;
        }

        /// <inheritdoc />
        public void Reset() => _currentIndex = 0;
    }

    /// <summary>주어진 일을 하고 그 결과를 그대로 돌려주는 잎이다.</summary>
    /// <remarks>자식을 두지 않는다. 붙여도 실행되지 않는다.</remarks>
    public sealed class ActionBehaviour : IBehaviour
    {
        private readonly Func<IBehaviourContext, BehaviourStatus> _action;

        /// <summary>실행할 일을 지정한다.</summary>
        /// <param name="action">문맥를 받아 결과를 돌려주는 일이다.</param>
        public ActionBehaviour(Func<IBehaviourContext, BehaviourStatus> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context) => _action(context.Context);

        /// <inheritdoc />
        public void Reset()
        {
        }
    }

}
