using HS.Framework.AI.BehaviourTree;
namespace HS.Framework.AI.Behaviour
{
    /// <summary>행동 트리에서 한 자리가 무엇을 하는지를 정한다.</summary>
    /// <remarks>
    /// <para>
    /// <b>자식을 알지 못한다.</b> 누가 아래에 붙어 있는지는 트리가 알고, 이것은 자기 자리에서
    /// 무엇을 할지만 안다. 합성 노드도 자식 목록을 들고 있지 않으며, 필요할 때
    /// <see cref="BehaviourTickContext"/>를 통해 트리에 묻는다.
    /// </para>
    /// <para>
    /// 이렇게 나눈 것은 구조가 두 곳에 있으면 어긋나기 때문이다. 노드가 자식 배열을 들고 트리도
    /// 자식을 들면, 편집기가 한쪽만 고쳐도 컴파일이 되고 아무 신호가 나지 않는다.
    /// </para>
    /// </remarks>
    public interface IBehaviour
    {
        /// <summary>이 자리를 한 번 실행하고 결과를 돌려준다.</summary>
        /// <param name="context">자기 자리와 문맥, 자식을 물을 곳을 담은 것이다.</param>
        /// <returns>실행 결과이다.</returns>
        BehaviourStatus Tick(in BehaviourTickContext context);

        /// <summary>진행 중이던 것을 처음으로 되돌린다.</summary>
        /// <remarks>
        /// <para>
        /// 아래 가지까지 되돌리는 일은 트리가 한다. 여기서는 자기 자리의 진행만 지운다.
        /// </para>
        /// <para>
        /// <b>바깥에 남는 것을 잡았으면 여기서 푼다.</b> 실행 중인 가지는 앞선 자리에 언제든
        /// 가로채여 처음으로 되돌려진다. 그때 이 자리가 점유나 예약처럼 자기 바깥에 남는 상태를
        /// 잡아 두었다면 반드시 풀어야 한다. 풀지 않으면 아무도 쓰지 않는 자리가 영구히 잡혀
        /// 다른 유닛이 그것을 쓰지 못한다.
        /// </para>
        /// <para>
        /// 다만 유닛이 그것을 실제로 쓰고 있는 중이라면 풀지 않아야 한다. 그래서 푸는 조건은
        /// "잡았지만 아직 쓰고 있지 않은 상태"로 좁힌다. 그런 구현의 예가
        /// SelectCoverDestinationBehaviour.Reset 이다.
        /// </para>
        /// </remarks>
        void Reset();
    }
}
