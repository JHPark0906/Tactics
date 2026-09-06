using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Units;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 주변에서 적대 유닛을 찾아 「누구를 찾았는가」에 답하는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>붙이는 곳.</b> 유닛 프리팹에 이 컴포넌트를 붙여야 행동 트리의 <c>DetectEnemyService</c>가
    /// 찾을 것을 갖는다. 이 컴포넌트가 없으면 대상이 영원히 비어 있어 유닛은 전진만 한다.
    /// </para>
    /// <para>
    /// 🔴 <b>문맥의 키를 쓰는 것은 이 컴포넌트가 아니라 그 서비스다.</b> 여기서는 찾아서 답하기까지가
    /// 몫이고, 답을 어느 키에 담을지는 트리에 그려진 서비스가 정한다. 양쪽이 쓰면 <b>서비스의 키를
    /// 인스펙터에서 바꾼 순간 갈라진다</b> — 서비스는 새 키에 쓰고 이 컴포넌트는 여전히
    /// <see cref="UnitBehaviourKeys.Target"/>에 써서 키 둘이 채워지고, 지우는 쪽은 한쪽만 지운다.
    /// </para>
    /// <para>
    /// <b>후보 수집 방식.</b> <see cref="IUnitSpatialRegistry"/>에 「내 위치에서 이 반경 안에 누가 있는가」를
    /// 묻는다. 판정은 평면 원-겹침이며, 등록된 유닛을 각자의 반지름으로 된 원으로 보고 이 유닛의
    /// 탐지 반경과 겹치는지 확인한다. 물리 콜라이더나 레이어는 관여하지 않으므로, 대상이 되기 위해
    /// 별도로 붙일 콜라이더나 레이어가 없다 — <see cref="TacticalUnit"/>이 스스로 레지스트리에 등록하기 때문이다.
    /// </para>
    /// <para>
    /// <b>스스로 다시 찾지 않는다.</b> 언제 다시 찾을지는 행동 트리에 놓인 <c>DetectEnemyService</c>가
    /// 정하며, 그 서비스가 <see cref="RefreshTarget"/>을 부른다. 이 컴포넌트가 <c>Update</c>에서도 찾으면
    /// 같은 키를 두 시계가 쓰게 되고, 그중 하나는 프레임 시간이라 <b>표적이 얼마나 신선한지가 기기 성능에
    /// 따라 달라진다.</b> 표적 선정은 접근·엄폐·사격이 모두 읽는 값이므로 결과를 바꾸며, 결과를 바꾸는
    /// 값은 고정 스텝에서 도는 트리 한 곳에서만 갱신한다.
    /// </para>
    /// <para>
    /// 다시 찾는 간격도 이 컴포넌트가 갖지 않는다. 그 값은 <c>DetectEnemyServiceDefinition</c>이
    /// 에셋에 담으므로, 트리를 그리는 사람이 그 자리에서 보고 정한다. 양쪽에 두면 어느 쪽이 실제로
    /// 쓰이는지 인스펙터만 보고는 알 수 없다.
    /// </para>
    /// <para>
    /// <b>표적은 반경만 본다.</b> 각도도 가림도 보지 않는다. 엄폐는 보이지 않게 하는 것이 아니라 대신 맞아 주는 것이므로,
    /// 반경 안의 적은 벽 뒤에 있든 등 뒤에 있든 엄폐 중이든 표적이 된다. 반경은 적을 알아채는 거리이고
    /// 공격 사거리는 유닛 정의가 따로 갖는다. 둘을 하나로 합치면 사거리 밖의 적을 알아채지 못해 추격 가지가 돌지 않는다.
    /// </para>
    /// <para>
    /// <b>대상 해제.</b> 대상이 파괴되거나 반경 밖으로 나가면 답을 비운다.
    /// 죽은 유닛이나 멀어진 유닛을 계속 겨누지 않게 하기 위함이며, 문맥의 키는 그것을 받은
    /// 서비스가 지운다.
    /// </para>
    /// <para>
    /// <b>어빌리티의 대상 공급자.</b> 사격과 엄폐 어빌리티는 자기 유닛에서 <see cref="ICombatTargetSource"/>를 찾아
    /// 대상을 묻는다. 이 컴포넌트가 그 계약의 기본 구현이다. 계약을 구현하지 않으면 찾는 것이 null이라
    /// 세 어빌리티가 한 번도 활성화되지 않는데, 오류도 경고도 없어 유닛이 적 앞에 가만히 서 있는 것으로만 보인다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TeamMember))]
    public sealed class EnemyDetector : MonoBehaviour, ICombatTargetSource
    {
        [Header("탐지")]
        [Tooltip("적을 알아채는 반경(미터)이다. 이 안의 적은 벽 뒤에 있든 등 뒤에 있든 표적이 되며, 후보를 모으는 반경으로도 쓴다.\n" +
                 "공격 사거리는 유닛 정의가 따로 가지므로, 사거리보다 넓게 두어야 사거리 밖의 적을 알아채고 다가간다.")]
        [SerializeField]
        [Min(0f)]
        private float viewDistance = 20f;

        private readonly List<TeamMember> _candidates = new();
        private IUnitSpatialRegistry _registry;
        private TeamMember _team;

        /// <inheritdoc />
        public Transform CurrentTarget { get; private set; }

        /// <summary>
        /// 진영 구성요소를 가져온다. Awake보다 먼저 호출되어도 동작하도록 필요할 때 찾는다.
        /// </summary>
        private TeamMember Team
        {
            get
            {
                if (_team == null)
                {
                    _team = GetComponent<TeamMember>();
                }

                return _team;
            }
        }

        private void Awake()
        {
            _team = GetComponent<TeamMember>();
        }

        /// <summary>공간 레지스트리를 주입받는다. 이것을 받지 못하면 후보를 하나도 못 모은다.</summary>
        /// <param name="registry">평면 위 유닛 위치에 원-겹침으로 답하는 레지스트리이며, 씬에 없으면 null이 온다.</param>
        [Inject]
        public void InjectSpatialRegistry(IUnitSpatialRegistry registry)
        {
            _registry = registry;
        }

        private void OnDisable()
        {
            // 감지를 멈추는 동안 답이 남아 있으면 사격·엄폐 어빌리티가 오래된 대상을 계속 겨눈다.
            // 문맥의 키는 여기서 지우지 않는다 — 그 키의 임자는 서비스이며, 다음 실행에서 지운다.
            ClearTarget();
        }

        /// <summary>
        /// 주변을 다시 검사해 대상을 갱신한다.
        /// 조건에 맞는 적이 없으면 답을 비우고 null을 돌려준다.
        /// </summary>
        /// <remarks>
        /// 행동 트리의 <c>DetectEnemyService</c>가 자기 간격으로 부른다. 이 컴포넌트에는 이것을 부를
        /// 시계가 없으며, 즉시 반영이 필요한 쪽은 직접 부를 수 있다.
        /// </remarks>
        /// <returns>갱신 결과로 선정된 대상이며, 없으면 null이다.</returns>
        public Transform RefreshTarget()
        {
            CollectCandidates();
            var selected = EnemyTargetSelector.SelectNearestHostileInRange(Team, _candidates, viewDistance);

            if (selected == null)
            {
                ClearTarget();
                return null;
            }

            CurrentTarget = selected.transform;
            return CurrentTarget;
        }

        /// <summary>
        /// 찾아 둔 대상을 지운다.
        /// </summary>
        /// <remarks>
        /// 문맥의 키는 건드리지 않는다. 그 키의 임자는 <c>DetectEnemyService</c>이며, 이 메서드가
        /// 답을 비우면 그 서비스가 다음 실행에서 키를 지운다.
        /// </remarks>
        public void ClearTarget()
        {
            CurrentTarget = null;
        }

        /// <summary>
        /// 탐지 반경 안에서 겹치는 유닛의 진영 구성요소를 모은다.
        /// 결과 목록은 레지스트리 질의가 채우므로 이 자리에서 따로 비우지 않는다.
        /// </summary>
        private void CollectCandidates()
        {
            if (_registry == null)
            {
                _candidates.Clear();
                return;
            }

            var detectionCircle = new PlanarCircle(PlanarPosition.FromWorld(transform.position), viewDistance);
            _registry.CollectOverlapping(detectionCircle, _candidates);
            SortCandidatesDeterministically();
        }

        /// <summary>
        /// 후보를 언제나 같은 순서로 늘어놓는다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 레지스트리가 돌려주는 순서는 등록된 차례를 따르며, 그 차례가 실행마다 같다는 보장은 없다.
        /// 순서가 흔들리면 가장 가까운 적이 둘 이상 같은 거리에 있을 때 <b>다른 적을 고르게 된다.</b>
        /// 그러면 상황이 그대로인데도 유닛이 매 틱 다른 적을 겨눠 제자리에서 오락가락한다.
        /// </para>
        /// <para>
        /// 정렬 기준은 세계 상태에서만 뽑는다. 먼저 이쪽에서 가까운 순, 같으면 x, 그다음 z 좌표 순이다.
        /// 인스턴스 번호나 생성 차례 같은 엔진 내부 값을 쓰지 않으므로 실행 환경이 달라도 같은 순서가 나온다.
        /// </para>
        /// </remarks>
        private void SortCandidatesDeterministically()
        {
            EnemyCandidateOrdering.Sort(_candidates, transform.position);
        }

    }
}
