using System;
using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Foundation.Collections;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>문맥의 키를 지켜보다가 값이 바뀌면 끊어 달라고 거는 조건 자리의 공통 부분이다.</summary>
    /// <remarks>
    /// <para>
    /// 조건 자리는 무엇을 보는지만 다르고, 언제 걸고 언제 푸는지는 같다. 부모가 실행 중인 동안
    /// 걸리고, 벗어나면 푼다. 그 부분을 여기 모아 두어 조건마다 되풀이하지 않는다.
    /// </para>
    /// <para>
    /// <b>끊을 것이 없으면 걸지 않는다.</b> 걸어 두고 아무것도 안 하면 값만 새는 셈이다.
    /// </para>
    /// </remarks>
    public abstract class ObservingConditionBehaviour : DecoratorBehaviour, IObservingBehaviour
    {
        private readonly string[] _observedKeys;
        private readonly List<IDisposable> _subscriptions = new();
        private BehaviourTreeInstance _tree;
        private TreeNode<IBehaviour> _node;
        private bool? _lastEvaluation;

        /// <summary>지켜볼 키들과 끊을 범위를 지정한다.</summary>
        /// <param name="abortScope">값이 바뀌었을 때 무엇을 끊을지이다.</param>
        /// <param name="observedKeys">지켜볼 문맥 키들이다.</param>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        protected ObservingConditionBehaviour(BehaviourAbortScope abortScope, params string[] observedKeys)
        {
            _observedKeys = observedKeys ?? Array.Empty<string>();
            foreach (var key in _observedKeys)
            {
                RequireKey(key, nameof(observedKeys));
            }

            AbortScope = abortScope;
        }

        /// <summary>값이 바뀌었을 때 무엇을 끊을지이다.</summary>
        public BehaviourAbortScope AbortScope { get; }

        /// <summary>키가 비어 있지 않은지 확인하고 그대로 돌려준다.</summary>
        /// <param name="key">확인할 키이다.</param>
        /// <param name="paramName">예외에 적을 인자 이름이다.</param>
        /// <returns>넘겨받은 키 그대로이다.</returns>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        protected static string RequireKey(string key, string paramName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("키는 비어 있을 수 없습니다.", paramName);
            }

            return key;
        }

        /// <inheritdoc />
        /// <remarks>조건은 문이다. 자식의 결과를 바꾸지 않는다.</remarks>
        protected sealed override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus;

        /// <inheritdoc />
        /// <remarks>문을 본 결과를 기억해 둔다. 지켜보기 시작할 때 그 사이 값이 바뀌었는지 견주는 데 쓴다.</remarks>
        protected sealed override bool CanEnter(in BehaviourTickContext context)
        {
            var passes = Evaluate(context);
            _lastEvaluation = passes;
            return passes;
        }

        /// <summary>지금 값이 아래를 통과시키는지 본다.</summary>
        /// <remarks>부작용 없이 값만 읽어야 한다. 문을 지날 때 말고도 지켜보기 시작할 때 한 번 더 불린다.</remarks>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <returns>통과시키면 참이다.</returns>
        protected abstract bool Evaluate(in BehaviourTickContext context);

        /// <inheritdoc />
        protected override void OnReset() => _lastEvaluation = null;

        /// <inheritdoc />
        public void OnBecomeRelevant(in BehaviourTickContext context)
        {
            if (AbortScope == BehaviourAbortScope.None || _subscriptions.Count > 0)
            {
                return;
            }

            _tree = context.Tree;
            _node = context.Node;
            foreach (var key in _observedKeys)
            {
                _subscriptions.Add(context.Context.Observe(key, OnObservedKeyChanged));
            }

            RequestAbortIfTheValueChangedBeforeWatching(context);
        }

        /// <summary>문을 본 뒤 지켜보기 시작하기 전에 값이 바뀌었으면 끊어 달라고 건다.</summary>
        /// <remarks>
        /// <para>
        /// 지켜보기는 실행이 끝난 뒤에 시작되는데, 같은 실행 안에서 이 자리 뒤에 도는 자리가 값을 쓰면
        /// 그 변화는 알림으로 오지 않는다. 문이 막았던 값이 그 사이 통과시키는 값이 되었다면, 뒤엣
        /// 형제가 차례를 가진 채로 이 자리는 다시 불리지 않으므로 여기서 걸어 둔다.
        /// </para>
        /// <para>
        /// 이번 실행에서 문을 보지 않았으면 견줄 바탕이 없으므로 걸지 않는다. 순차 자리 아래에서
        /// 통과한 문의 뒤엣 형제가 도는 것은 그 자리가 차례를 빼앗긴 것이 아니라 제 몫을 마친 것이다.
        /// 반대 방향(통과시켰는데 막게 된 것)은 다음 실행에서 문이 다시 보고 그 자리에서 막으므로 걸 필요가 없다.
        /// </para>
        /// </remarks>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        private void RequestAbortIfTheValueChangedBeforeWatching(in BehaviourTickContext context)
        {
            var abortsLower = AbortScope == BehaviourAbortScope.LowerPriority || AbortScope == BehaviourAbortScope.Both;
            if (!abortsLower || _lastEvaluation != false || !Evaluate(context))
            {
                return;
            }

            _tree.RequestAbort(_node, BehaviourAbortScope.LowerPriority);
        }

        /// <inheritdoc />
        public void OnCeaseRelevant()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
            _tree = null;
            _node = null;
        }

        private void OnObservedKeyChanged(string key)
        {
            _tree?.RequestAbort(_node, AbortScope);
        }
    }
}
