using HS.Tactics.Foundation.Simulation;
using UnityEngine;

namespace HS.Tactics.Character.Movement
{
    /// <summary>
    /// 로직 스텝 사이를 화면에서 메워, 낮은 주기로 움직이는 유닛이 부드럽게 보이게 한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 시각 오브젝트를 따로 움직이는가.</b> 유닛의 루트는 <b>로직이 보는 위치</b>다.
    /// 사거리·시야·엄폐가 모두 그 위치를 읽으므로, 루트에 보간값을 써 버리면
    /// <b>화면이 만든 중간값이 판단으로 돌아온다.</b> 그 값은 프레임 시각에 달려 있어
    /// 같은 시드로 돌려도 기기에 따라 다른 전투가 되고, 이 어긋남은 아무 신호도 남기지 않는다.
    /// 그래서 루트는 스텝마다 정확한 자리에 두고, <b>눈에 보이는 자식만</b> 그 사이를 메운다.
    /// </para>
    /// <para>
    /// <b>시각 루트를 지정하지 않으면 아무것도 하지 않는다.</b> 그 경우 유닛은 로직 주기 그대로 움직여
    /// 조금 끊겨 보이지만 동작은 정확하다. 기본값을 "아무 일도 하지 않음"으로 둔 것은,
    /// 잘못 지정된 상태로 조용히 이상하게 움직이는 것보다 낫기 때문이다.
    /// </para>
    /// <para>
    /// 순간이동은 메우지 않는다. 스폰과 재배치는 <see cref="PlanarCharacterMover.Relocate"/>가 이전·현재
    /// 위치를 함께 맞추므로 거리와 무관하게 새 자리에 바로 붙는다. 그 외에 한 스텝의 변위가
    /// <see cref="teleportDistance"/>를 넘은 경우도 보간하지 않는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlanarCharacterMover))]
    public sealed class FixedStepMotionInterpolator : MonoBehaviour
    {
        [Tooltip("보간해서 움직일 시각 오브젝트이다. 유닛의 겉모습(메시)이 달린 자식을 지정한다.\n" +
                 "비워 두면 보간하지 않고 로직 주기 그대로 움직인다. 동작은 정확하되 조금 끊겨 보인다.\n" +
                 "유닛 루트를 지정하면 안 된다. 루트는 로직이 보는 위치라 화면 값이 판단으로 섞인다.")]
        [SerializeField]
        private Transform visualRoot;

        [Tooltip("한 스텝에 이만큼을 넘게 움직였으면 순간이동으로 보고 보간하지 않는다.\n" +
                 "스폰이나 재배치처럼 한 번에 멀리 옮겨 갔을 때 유닛이 화면을 가로질러 미끄러지는 것을 막는다.")]
        [SerializeField]
        [Min(0f)]
        private float teleportDistance = 5f;

        private PlanarCharacterMover _mover;
        private Vector3 _visualLocalPosition;
        private bool _hasVisualRoot;

        /// <summary>보간할 시각 오브젝트가 지정되어 있는지 여부이다.</summary>
        public bool HasVisualRoot => visualRoot != null;

        /// <summary>지난 프레임에 적용한 보간 비율이며 0 이상 1 이하이다.</summary>
        public float LastAlpha { get; private set; }
        
        private void Awake()
        {
            _mover = GetComponent<PlanarCharacterMover>();
            _hasVisualRoot = visualRoot != null;
            if (_hasVisualRoot)
            {
                _visualLocalPosition = visualRoot.localPosition;
            }
        }

        /// <summary>
        /// 화면 프레임마다 시각 오브젝트를 두 스텝 사이의 자리로 옮긴다.
        /// 로직은 이 값을 읽지 않으므로 여기서 프레임 시각을 써도 결과가 흔들리지 않는다.
        /// </summary>
        private void LateUpdate()
        {
            if (!_hasVisualRoot || _mover == null)
            {
                return;
            }

            var previous = _mover.PreviousLogicPosition;
            var current = _mover.CurrentLogicPosition;
            var pendingTime = (float)(Time.timeAsDouble - Time.fixedTimeAsDouble);
            LastAlpha = FixedStepInterpolation.ResolveAlpha(pendingTime, Time.fixedDeltaTime);

            var interpolated = (current - previous).sqrMagnitude > teleportDistance * teleportDistance
                ? current
                : Vector3.Lerp(previous, current, LastAlpha);

            visualRoot.position = interpolated + transform.rotation * _visualLocalPosition;
        }
    }
}
