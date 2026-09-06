using System;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;
using UnityEngine.AI;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>지켜보는 것이 어느 자리에서 어느 쪽으로 벌어진 부채꼴 안에 있을 때만 아래를 실행하게 한다.</summary>
    /// <remarks>
    /// 꼭짓점과 방향, 지켜보는 것 셋을 모두 문맥에서 읽는다. 방향 키에 Transform이 담겨 있으면 그것이
    /// 바라보는 쪽이다. 지켜보는 것이 꼭짓점과 같은 자리에 있으면 안에 있는 것으로 본다.
    /// </remarks>
    public sealed class ConeCheckBehaviour : DecoratorBehaviour
    {
        private readonly string _originKey;
        private readonly string _directionKey;
        private readonly string _observedKey;
        private readonly float _halfAngleDegrees;

        /// <summary>부채꼴을 이루는 세 키와 벌어진 각을 지정한다.</summary>
        /// <param name="originKey">꼭짓점 자리가 담긴 문맥 키이다.</param>
        /// <param name="directionKey">부채꼴이 향하는 쪽이 담긴 문맥 키이다.</param>
        /// <param name="observedKey">안에 있는지 볼 것이 담긴 문맥 키이다.</param>
        /// <param name="halfAngleDegrees">가운데에서 한쪽으로 벌어진 각(도)이다.</param>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public ConeCheckBehaviour(string originKey, string directionKey, string observedKey, float halfAngleDegrees)
        {
            _originKey = SpatialKeys.Require(originKey, nameof(originKey));
            _directionKey = SpatialKeys.Require(directionKey, nameof(directionKey));
            _observedKey = SpatialKeys.Require(observedKey, nameof(observedKey));
            _halfAngleDegrees = Mathf.Clamp(halfAngleDegrees, 0f, 180f);
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context)
        {
            var values = context.Context;
            if (!ContextValueReader.TryGetPosition(values, _originKey, out var origin)
                || !ContextValueReader.TryGetDirection(values, _directionKey, out var direction)
                || !ContextValueReader.TryGetPosition(values, _observedKey, out var observed))
            {
                return false;
            }

            return SpatialKeys.IsWithinCone(origin, direction, observed, _halfAngleDegrees);
        }

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus;
    }

    /// <summary>들어올 때 지켜보던 쪽을 기억해 두고, 지켜보는 것이 그 부채꼴을 벗어나면 아래를 막는다.</summary>
    /// <remarks>
    /// <para>
    /// 부채꼴의 방향은 키로 받지 않는다. 아래가 시작되는 순간 꼭짓점에서 지켜보는 것을 향한 쪽이
    /// 방향이 되고, 그 뒤로 지켜보는 것이 그 쪽에서 벌어진 각 안에 머무는 동안만 아래가 돈다.
    /// </para>
    /// <para>
    /// 벗어나면 문을 닫는 것으로 아래를 되돌린다. 아래가 끝나거나 되돌려지면 기억한 방향을 지운다.
    /// </para>
    /// </remarks>
    public sealed class KeepInConeBehaviour : DecoratorBehaviour
    {
        private readonly string _originKey;
        private readonly string _observedKey;
        private readonly float _halfAngleDegrees;
        private Vector3? _initialDirection;

        /// <summary>꼭짓점과 지켜볼 것의 키, 벌어진 각을 지정한다.</summary>
        /// <param name="originKey">꼭짓점 자리가 담긴 문맥 키이다.</param>
        /// <param name="observedKey">머무는지 볼 것이 담긴 문맥 키이다.</param>
        /// <param name="halfAngleDegrees">처음 방향에서 한쪽으로 벌어진 각(도)이다.</param>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public KeepInConeBehaviour(string originKey, string observedKey, float halfAngleDegrees)
        {
            _originKey = SpatialKeys.Require(originKey, nameof(originKey));
            _observedKey = SpatialKeys.Require(observedKey, nameof(observedKey));
            _halfAngleDegrees = Mathf.Clamp(halfAngleDegrees, 0f, 180f);
        }

        /// <inheritdoc />
        /// <remarks>벗어나서 막을 때 기억한 방향도 지운다. 다음에 들어오면 그때의 방향이 새 기준이다.</remarks>
        protected override bool CanEnter(in BehaviourTickContext context)
        {
            if (TryGetCurrentDirection(context.Context, out var current)
                && (!_initialDirection.HasValue
                    || SpatialKeys.IsWithinCone(Vector3.zero, _initialDirection.Value, current, _halfAngleDegrees)))
            {
                return true;
            }

            _initialDirection = null;
            return false;
        }

        /// <inheritdoc />
        protected override void OnBeforeChildTick(in BehaviourTickContext context)
        {
            if (!_initialDirection.HasValue && TryGetCurrentDirection(context.Context, out var current))
            {
                _initialDirection = current;
            }
        }

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
        {
            if (childStatus != BehaviourStatus.Running)
            {
                _initialDirection = null;
            }

            return childStatus;
        }

        /// <inheritdoc />
        protected override void OnReset() => _initialDirection = null;

        private bool TryGetCurrentDirection(IBehaviourContext values, out Vector3 direction)
        {
            if (ContextValueReader.TryGetPosition(values, _originKey, out var origin)
                && ContextValueReader.TryGetPosition(values, _observedKey, out var observed))
            {
                direction = observed - origin;
                return true;
            }

            direction = Vector3.zero;
            return false;
        }
    }

    /// <summary>유닛이 문맥의 자리에 닿아 있을 때만 아래를 실행하게 한다.</summary>
    public sealed class IsAtLocationBehaviour : DecoratorBehaviour
    {
        private readonly Transform _self;
        private readonly string _locationKey;
        private readonly float _acceptableRadius;

        /// <summary>유닛과 자리 키, 닿은 것으로 볼 거리를 지정한다.</summary>
        /// <param name="self">유닛의 Transform이다.</param>
        /// <param name="locationKey">자리가 담긴 문맥 키이다.</param>
        /// <param name="acceptableRadius">이 거리 안이면 닿은 것으로 본다(미터).</param>
        /// <exception cref="ArgumentNullException">유닛이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public IsAtLocationBehaviour(Transform self, string locationKey, float acceptableRadius)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _locationKey = SpatialKeys.Require(locationKey, nameof(locationKey));
            _acceptableRadius = Mathf.Max(0f, acceptableRadius);
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context)
            => ContextValueReader.TryGetPosition(context.Context, _locationKey, out var location)
               && Vector3.Distance(_self.position, location) <= _acceptableRadius;

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus;
    }

    /// <summary>두 자리 사이에 갈 수 있는 길이 있을 때만 아래를 실행하게 한다.</summary>
    /// <remarks>
    /// 출발 키를 비우면 유닛 자신의 자리에서 출발한다. 길이 있는지는 기본으로 내비게이션 바닥에
    /// 묻지만, 다른 방법을 넘길 수 있다. 바닥이 없는 검사가 그 길을 쓴다.
    /// </remarks>
    public sealed class DoesPathExistBehaviour : DecoratorBehaviour
    {
        private readonly Transform _self;
        private readonly string _fromKey;
        private readonly string _toKey;
        private readonly Func<Vector3, Vector3, bool> _pathExists;

        /// <summary>출발과 도착을 읽을 키와 길을 묻는 방법을 지정한다.</summary>
        /// <param name="self">유닛의 Transform이며, 출발 키가 비어 있을 때 출발 자리가 된다.</param>
        /// <param name="fromKey">출발 자리가 담긴 문맥 키이며, 비우면 유닛 자신이다.</param>
        /// <param name="toKey">도착 자리가 담긴 문맥 키이다.</param>
        /// <param name="pathExists">두 자리 사이에 길이 있는지 답하는 것이며, 비우면 내비게이션 바닥에 묻는다.</param>
        /// <exception cref="ArgumentNullException">유닛이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">도착 키가 비어 있으면 발생한다.</exception>
        public DoesPathExistBehaviour(
            Transform self,
            string fromKey,
            string toKey,
            Func<Vector3, Vector3, bool> pathExists = null)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _fromKey = string.IsNullOrWhiteSpace(fromKey) ? null : fromKey;
            _toKey = SpatialKeys.Require(toKey, nameof(toKey));
            _pathExists = pathExists ?? NavMeshPathExists;
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context)
        {
            var values = context.Context;
            Vector3 from;
            if (_fromKey == null)
            {
                from = _self.position;
            }
            else if (!ContextValueReader.TryGetPosition(values, _fromKey, out from))
            {
                return false;
            }

            return ContextValueReader.TryGetPosition(values, _toKey, out var to) && _pathExists(from, to);
        }

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus;

        /// <summary>내비게이션 바닥 위에서 두 자리를 잇는 온전한 길이 있는지 본다.</summary>
        /// <param name="from">출발 자리이다.</param>
        /// <param name="to">도착 자리이다.</param>
        /// <returns>끊기지 않은 길이 있으면 참이다.</returns>
        private static bool NavMeshPathExists(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)
                   && path.status == NavMeshPathStatus.PathComplete;
        }
    }

    /// <summary>자리 조건들이 함께 쓰는 셈이다.</summary>
    internal static class SpatialKeys
    {
        /// <summary>키가 비어 있지 않은지 확인하고 그대로 돌려준다.</summary>
        internal static string Require(string key, string paramName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("키는 비어 있을 수 없습니다.", paramName);
            }

            return key;
        }

        /// <summary>지켜보는 것이 꼭짓점에서 그 쪽으로 벌어진 부채꼴 안에 있는지 본다.</summary>
        /// <param name="origin">꼭짓점이다.</param>
        /// <param name="direction">부채꼴이 향하는 쪽이다.</param>
        /// <param name="observed">안에 있는지 볼 자리이다.</param>
        /// <param name="halfAngleDegrees">한쪽으로 벌어진 각(도)이다.</param>
        /// <returns>안에 있으면 참이다. 꼭짓점과 같은 자리는 안이고, 방향이 없으면 밖이다.</returns>
        internal static bool IsWithinCone(Vector3 origin, Vector3 direction, Vector3 observed, float halfAngleDegrees)
        {
            var toObserved = observed - origin;
            if (toObserved.sqrMagnitude <= Mathf.Epsilon)
            {
                return true;
            }

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            return Vector3.Angle(direction, toObserved) <= halfAngleDegrees;
        }
    }
}
