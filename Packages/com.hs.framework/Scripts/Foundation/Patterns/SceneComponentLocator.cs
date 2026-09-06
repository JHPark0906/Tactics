using UnityEngine;

namespace HS.Framework.Foundation.Patterns
{
    /// <summary>
    /// 씬에 놓이는 구성요소를 필요할 때 찾아 넘겨주고, 그 씬 안에서는 찾은 것을 다시 쓰는 확인자이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 인스턴스가 아니라 확인자를 등록하는가.</b> 씬에 놓이는 구성요소는 컨테이너를 구성하는 시점에 아직
    /// 존재하지 않는다. 그렇다고 그것이 깨어난 뒤에 등록하면, 같은 씬 로드에서 그보다 먼저 주입된 쪽은 대상을
    /// 찾지 못한다. 주입 순서는 씬의 최상위 오브젝트 순서에 달려 있어 보장할 수 없고, 확인자를 등록하면
    /// <b>연결 자체는 컨테이너 구성 시점에 이미 성립</b>하고 실제 대상은 주입되는 순간에 해석되므로
    /// 순서 문제가 구조적으로 사라진다.
    /// </para>
    /// <para>
    /// 🔴 <b>등록은 Transient여야 한다.</b> 컨테이너가 <see cref="Resolve"/>를 팩토리로 부르되 결과를 붙들지 않아야
    /// 주입할 때마다 여기가 불린다. 결과를 Singleton으로 잡으면 이 확인자는 한 번만 불리고 아래의 재탐색이 영영
    /// 일어나지 않아, <b>다음 씬의 사용자가 파괴된 옛 대상을 받는다.</b>
    /// </para>
    /// <para>
    /// <b>씬이 바뀌어도 이전 대상이 남지 않는다.</b> 확인자는 대상을 직접 붙들지 않고 캐시만 하며, 캐시된 것이
    /// 파괴되면 Unity의 null 비교로 걸러져 다음 요청에서 다시 찾는다. 그래서 별도의 해제 경로 없이도 언제나
    /// 현재 씬의 것만 넘어간다. 같은 씬 안에서는 탐색이 첫 요청 한 번뿐이므로 사용자 수만큼 씬을 뒤지지 않는다.
    /// </para>
    /// <para>
    /// <b>없으면 진짜 null을 돌려준다.</b> 파괴된 Unity 객체를 그대로 넘기면 받는 쪽이 그것을 인터페이스나
    /// object로 담는 순간 <c>== null</c> 이 거짓이 되어 <b>이미 없는 것을 살아 있는 것으로 다룬다.</b>
    /// </para>
    /// <para>
    /// <b>비활성인 것을 찾을지는 쓰는 쪽이 정한다.</b> 꺼져 있는 동안 자기 일을 하지 않는 대상이면 찾지 않는 편이
    /// 옳고(그것을 받아 봐야 아무 일도 일어나지 않는다), 꺼져 있어도 그 씬에서 그것 하나뿐인 출처면 찾아야 한다.
    /// 기본값을 두지 않는 것은 그 판단을 조용히 넘기지 않기 위해서이다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TComponent">씬에서 찾을 구성요소의 형식이다.</typeparam>
    public sealed class SceneComponentLocator<TComponent> where TComponent : Component
    {
        /// <summary>비활성 오브젝트까지 찾을지 여부이다.</summary>
        private readonly FindObjectsInactive _inactiveSearch;

        /// <summary>지난번에 찾은 것이며, 파괴되면 Unity의 null 비교로 걸러진다.</summary>
        private TComponent _cached;

        /// <summary>비활성 오브젝트를 찾을지 정해 확인자를 만든다.</summary>
        /// <param name="inactiveSearch">
        /// 비활성 오브젝트까지 찾을지 여부이다. 기본값을 두지 않으므로 쓰는 쪽이 매번 정해야 한다.
        /// </param>
        public SceneComponentLocator(FindObjectsInactive inactiveSearch)
        {
            _inactiveSearch = inactiveSearch;
        }

        /// <summary>현재 씬의 대상을 돌려준다.</summary>
        /// <remarks>
        /// 캐시된 것이 아직 살아 있으면 그것을 그대로 주고, 없거나 파괴되었으면 다시 찾는다.
        /// 그래도 없으면 진짜 null이다.
        /// </remarks>
        /// <returns>현재 씬의 대상이며, 없으면 null이다.</returns>
        public TComponent Resolve()
        {
            if (_cached == null)
            {
                _cached = Object.FindFirstObjectByType<TComponent>(_inactiveSearch);
            }

            return _cached != null ? _cached : null;
        }
    }
}
