using HS.Framework.AI.BehaviourTree;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>주된 일이 끝났을 때 곁에서 돌던 가지를 어떻게 마무리할지이다.</summary>
    public enum SimpleParallelFinishMode
    {
        /// <summary>곁 가지가 돌고 있어도 바로 되돌리고 끝낸다.</summary>
        Immediate,

        /// <summary>곁 가지가 스스로 끝날 때까지 기다렸다가 끝낸다.</summary>
        Delayed
    }

    /// <summary>주된 일 하나와 곁에서 도는 가지 하나를 함께 돌린다.</summary>
    /// <remarks>
    /// <para>
    /// 첫 자식이 주된 일이고 둘째 자식이 곁 가지이다. 결과는 언제나 주된 일의 것이다. 곁 가지는
    /// 주된 일이 도는 동안 <b>되풀이</b>된다. 끝나면 되돌려 다음 실행에서 다시 돌므로, 주위를
    /// 살피거나 소리를 내는 일을 주된 일 곁에 붙일 수 있다.
    /// </para>
    /// <para>
    /// 주된 일이 끝났을 때 곁 가지가 아직 돌고 있으면 <see cref="SimpleParallelFinishMode"/>가
    /// 정한 대로 한다. 바로 끝내면 곁 가지를 되돌리고, 기다리면 곁 가지만 이어 돌리다가 그것이
    /// 끝났을 때 기억해 둔 주된 일의 결과를 돌려준다.
    /// </para>
    /// </remarks>
    public sealed class SimpleParallelBehaviour : IBehaviour
    {
        private readonly SimpleParallelFinishMode _finishMode;
        private BehaviourStatus? _mainResult;
        private bool _isBackgroundRunning;

        /// <summary>주된 일이 끝났을 때의 마무리 방법을 지정한다.</summary>
        /// <param name="finishMode">곁 가지를 어떻게 마무리할지이다.</param>
        public SimpleParallelBehaviour(SimpleParallelFinishMode finishMode = SimpleParallelFinishMode.Immediate)
        {
            _finishMode = finishMode;
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            var children = context.Children;
            if (children.Count == 0)
            {
                return BehaviourStatus.Failure;
            }

            var background = children.Count > 1 ? children[1] : null;
            if (!_mainResult.HasValue)
            {
                var mainStatus = context.TickChild(children[0]);
                if (mainStatus == BehaviourStatus.Running)
                {
                    RepeatBackground(context, background);
                    return BehaviourStatus.Running;
                }

                if (_finishMode == SimpleParallelFinishMode.Immediate || background == null || !_isBackgroundRunning)
                {
                    return Finish(context, background, mainStatus);
                }

                _mainResult = mainStatus;
            }

            if (context.TickChild(background) == BehaviourStatus.Running)
            {
                return BehaviourStatus.Running;
            }

            _isBackgroundRunning = false;
            return Finish(context, background, _mainResult.Value);
        }

        /// <summary>곁 가지를 한 번 돌리고, 끝났으면 되돌려 다음 실행에서 다시 돌게 한다.</summary>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <param name="background">곁 가지이며 없으면 null이다.</param>
        private void RepeatBackground(in BehaviourTickContext context, HS.Framework.Foundation.Collections.TreeNode<IBehaviour> background)
        {
            if (background == null)
            {
                return;
            }

            _isBackgroundRunning = context.TickChild(background) == BehaviourStatus.Running;
            if (!_isBackgroundRunning)
            {
                context.Tree.Reset(background);
            }
        }

        /// <summary>돌고 있던 곁 가지를 되돌리고 기억을 비운 뒤 결과를 돌려준다.</summary>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <param name="background">곁 가지이며 없으면 null이다.</param>
        /// <param name="result">이 자리의 결과이다.</param>
        /// <returns>넘겨받은 결과 그대로이다.</returns>
        private BehaviourStatus Finish(in BehaviourTickContext context, HS.Framework.Foundation.Collections.TreeNode<IBehaviour> background, BehaviourStatus result)
        {
            if (background != null && _isBackgroundRunning)
            {
                context.Tree.Reset(background);
            }

            _isBackgroundRunning = false;
            _mainResult = null;
            return result;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _mainResult = null;
            _isBackgroundRunning = false;
        }
    }
}
