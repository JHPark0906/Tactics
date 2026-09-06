using UnityEngine;

namespace HS.Framework.AI.BehaviourTree
{
    /// <summary>
    /// 관찰자가 대상의 시야각 안에 있고 장애물 없이 보이는지 판정한다.
    /// </summary>
    public sealed class FieldOfViewSensor
    {
        private readonly float _viewAngle;
        private readonly float _maxDistance;
        private readonly LayerMask _occlusionMask;

        /// <summary>
        /// 시야각, 최대 거리 및 가시성 Raycast 레이어를 지정한다.
        /// </summary>
        public FieldOfViewSensor(float viewAngle, float maxDistance, LayerMask occlusionMask)
        {
            _viewAngle = Mathf.Clamp(viewAngle, 0f, 360f);
            _maxDistance = Mathf.Max(0f, maxDistance);
            _occlusionMask = occlusionMask;
        }

        /// <summary>
        /// 관찰자가 대상의 위치를 시야각·거리·가림 안에서 볼 수 있는지 검사한다.
        /// </summary>
        public bool CanSee(Transform observer, Transform target)
        {
            if (observer == null || target == null)
            {
                return false;
            }

            var direction = target.position - observer.position;
            var distance = direction.magnitude;
            if (distance > _maxDistance)
            {
                return false;
            }

            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            var normalizedDirection = direction / distance;
            if (Vector3.Angle(observer.forward, normalizedDirection) > _viewAngle * 0.5f)
            {
                return false;
            }

            return !Physics.Raycast(
                observer.position,
                normalizedDirection,
                distance,
                _occlusionMask,
                QueryTriggerInteraction.Ignore);
        }
    }
}
