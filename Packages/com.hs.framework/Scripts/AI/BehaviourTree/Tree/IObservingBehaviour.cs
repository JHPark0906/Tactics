namespace HS.Framework.AI.Behaviour
{
    /// <summary>가지에 들어오고 나갈 때를 알아야 하는 자리이다.</summary>
    /// <remarks>
    /// <para>
    /// 값이 바뀌는 것을 지켜보려면 언제부터 언제까지 지켜볼지가 있어야 한다. 트리 어디에나 걸어
    /// 두면 실행 중이지도 않은 자리가 트리를 끊는다.
    /// </para>
    /// <para>
    /// <b>부모가 실행 중인 동안 걸린다.</b> 자기가 도는 중이거나(자기를 끊는 경우), 형제가 도는
    /// 중이거나(뒤엣것을 끊는 경우) 둘 다 부모가 실행 중일 때 일어나기 때문이다.
    /// </para>
    /// <para>
    /// 이 계약을 구현하지 않는 자리는 그냥 지켜보지 않는다. 그것이 결함이 아니라 대부분의 자리가
    /// 지켜볼 것이 없기 때문이다.
    /// </para>
    /// </remarks>
    public interface IObservingBehaviour
    {
        /// <summary>지켜보기 시작한다.</summary>
        /// <param name="context">자기 자리와 트리, 문맥을 담은 것이다.</param>
        void OnBecomeRelevant(in BehaviourTickContext context);

        /// <summary>지켜보기를 그만둔다.</summary>
        void OnCeaseRelevant();
    }
}
