using HS.Framework.Character;
using HS.Tactics.Foundation.Geometry;
using UnityEngine;

namespace HS.Tactics.Character.Movement
{
    /// <summary>
    /// 캐릭터가 바라보는 방향을 고정 스텝에서 각속도만큼씩 돌리는 구성요소이며, 회전을 쓰는 유일한 자리이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이동과 따로 적분한다.</b> 위치는 이동기가 제 스텝에서 정하고, 방향은 여기서 같은 고정 스텝 크기로
    /// 각속도×스텝만큼만 돈다. 두 적분은 서로의 값을 읽지 않으므로 각속도가 경로에 닿지 않고, 이동이 회전을
    /// 기다리지 않는다. 회전량이 스텝 크기에만 달려 있어 기기가 빠르든 느리든 같은 시간에 같은 각을 돈다.
    /// </para>
    /// <para>
    /// <b>무엇을 볼지.</b> 바라볼 점이 있으면 그 점, 없으면 이동기가 알린 이동 방향, 그것도 없으면 지금 방향을
    /// 유지한다. 바라볼 점은 둔 쪽이 지운다. 이동 방향은 마지막으로 알린 것을 그대로 두는데, 멈춘 뒤에도
    /// 그쪽을 이미 보고 있으므로 해가 없고 다시 걸으면 새 방향이 온다.
    /// </para>
    /// <para>
    /// <b>계약은 프레임워크, 이 구현은 Tactics다.</b> <see cref="ICharacterFacing"/>은 바닥면을 전제하지 않는
    /// 일반 계약이라 <see cref="ICharacterMover"/>와 같은 자리(<c>HS.Framework.Character</c>)에 남지만, 여기서
    /// 쓰는 각도 계산(<see cref="PlanarFacing"/>)은 Tactics가 결정한 평면 바닥을 전제한다. 결정론에 얽힌 결정은
    /// 전부 Tactics에 두는 규칙에 따라 구현만 이곳에 있다.
    /// </para>
    /// <para>
    /// 각속도는 인스펙터 값이 기본이며, 캐릭터를 조립하는 쪽이 <see cref="SetTurnSpeed"/>로 덮어쓴다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterFacing : MonoBehaviour, ICharacterFacing, ICharacterComponent
    {
        /// <summary>이보다 짧은 방향 벡터는 방향이 없는 것으로 본다.</summary>
        private const float MinimumSqrMagnitude = 1e-8f;

        [Tooltip("바라보는 방향이 도는 속도(초당 도)이다. 캐릭터를 조립하는 쪽이 유닛 데이터로 덮어쓴다.")]
        [SerializeField]
        [Min(1f)]
        private float turnSpeed = 120f;

        private Vector3? _lookTarget;
        private Vector3 _movementDirection;
        private bool _hasMovementDirection;

        /// <inheritdoc />
        public float TurnSpeed => Mathf.Max(1f, turnSpeed);

        /// <inheritdoc />
        public bool HasLookTarget => _lookTarget.HasValue;

        /// <summary>각속도를 지정한다. 1 미만은 1로 올린다. 0이면 영영 돌지 못해 조준이 끝나지 않는다.</summary>
        /// <param name="degreesPerSecond">초당 도이다.</param>
        public void SetTurnSpeed(float degreesPerSecond)
        {
            turnSpeed = Mathf.Max(1f, degreesPerSecond);
        }

        /// <inheritdoc />
        public void Initialize(CharacterBase characterBase)
        {
        }

        /// <inheritdoc />
        public void LookAt(Vector3 worldPoint)
        {
            _lookTarget = worldPoint;
        }

        /// <inheritdoc />
        public void ClearLookTarget()
        {
            _lookTarget = null;
        }

        /// <inheritdoc />
        public float AngleTo(Vector3 worldPoint)
        {
            var toPoint = PlanarPosition.FromWorld(worldPoint - transform.position);
            if (toPoint.SqrMagnitude <= MinimumSqrMagnitude)
            {
                return 0f;
            }

            var forward = PlanarPosition.FromWorld(transform.forward);
            if (forward.SqrMagnitude <= MinimumSqrMagnitude)
            {
                return 0f;
            }

            return Mathf.Abs(PlanarFacing.SignedAngleDegrees(forward.Normalized, toPoint.Normalized));
        }

        /// <inheritdoc />
        public void ReportMovementDirection(Vector3 worldDirection)
        {
            var planar = PlanarPosition.FromWorld(worldDirection);
            if (planar.SqrMagnitude <= MinimumSqrMagnitude)
            {
                return;
            }

            _movementDirection = planar.ToWorld();
            _hasMovementDirection = true;
        }

        /// <summary>고정 주기마다 바라보는 방향을 한 걸음 돌린다.</summary>
        private void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>받은 시간만큼 바라보려는 쪽으로 돈다.</summary>
        /// <param name="deltaTime">이번 걸음이 나타내는 시간(초)이다.</param>
        /// <remarks>받은 시간만큼만 돈다. 프레임 시간이 아니므로 기기가 느려도 한 번에 더 돌지 않는다.</remarks>
        public void Tick(float deltaTime)
        {
            Vector3 desired;
            if (_lookTarget.HasValue)
            {
                desired = _lookTarget.Value - transform.position;
            }
            else if (_hasMovementDirection)
            {
                desired = _movementDirection;
            }
            else
            {
                return;
            }

            var facing = PlanarFacing.Advance(
                PlanarPosition.FromWorld(transform.forward),
                PlanarPosition.FromWorld(desired),
                TurnSpeed * Mathf.Max(0f, deltaTime));

            var forward = facing.ToWorld();
            if (forward.sqrMagnitude <= MinimumSqrMagnitude)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
