using System;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>아무 일도 하지 않고 정해진 결과를 돌려주는 잎이다.</summary>
    /// <remarks>가지의 끝을 명시적으로 성공이나 실패로 막을 때, 또는 진행 중을 돌려 자리를 붙들어 둘 때 쓴다.</remarks>
    public sealed class FinishWithResultBehaviour : IBehaviour
    {
        private readonly BehaviourStatus _result;

        /// <summary>돌려줄 결과를 지정한다.</summary>
        /// <param name="result">언제나 돌려줄 결과이다.</param>
        public FinishWithResultBehaviour(BehaviourStatus result)
        {
            _result = result;
        }

        /// <summary>언제나 돌려주는 결과이다.</summary>
        public BehaviourStatus Result => _result;

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context) => _result;

        /// <inheritdoc />
        public void Reset()
        {
        }
    }

    /// <summary>유닛을 문맥의 자리 쪽으로 돌린다. 마주 볼 때까지 진행 중이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>바라보는 방향을 도맡는 계약이 있으면 그것을 통해서만 돈다.</b> 이 자리는 매 실행 바라볼 점을 두고,
    /// 계약이 제 각속도로 고정 스텝에서 돈다. 마주 봤으면 점을 지우고 성공한다. 가로채여 되돌려지거나 자리가
    /// 없어 실패할 때도 점을 지우므로 묵은 목표가 남지 않는다. 회전을 직접 쓰지 않으므로 이동기와 같은 스텝에
    /// 회전을 두고 겨루지 않는다.
    /// </para>
    /// <para>
    /// <b>계약이 없으면 직접 돌린다.</b> 바닥 위의 유닛이므로 수평으로만 돌린다. 도는 속도가 0 이하이면 한 번에
    /// 마주 본다. 이 길에서 시간은 <see cref="Time.deltaTime"/>에서 읽으며, 트리가 고정 주기에서 돌면 그 값은
    /// 고정 스텝의 길이다.
    /// </para>
    /// <para>
    /// 자리가 없거나 자기 자리와 같으면 돌릴 곳이 없으므로 실패한다.
    /// </para>
    /// </remarks>
    public sealed class RotateToFaceBehaviour : IBehaviour
    {
        private readonly Transform _self;
        private readonly string _targetKey;
        private readonly float _precisionDegrees;
        private readonly float _degreesPerSecond;
        private readonly Func<float> _deltaTimeProvider;
        private readonly ICharacterFacing _facing;

        /// <summary>돌릴 유닛과 마주 볼 자리의 키, 정밀도와 속도를 지정한다.</summary>
        /// <param name="self">돌릴 유닛의 Transform이다.</param>
        /// <param name="targetKey">마주 볼 자리가 담긴 문맥 키이다.</param>
        /// <param name="precisionDegrees">이 각(도) 안이면 마주 본 것으로 본다.</param>
        /// <param name="degreesPerSecond">계약이 없을 때 도는 속도(도/초)이며, 0 이하이면 한 번에 돈다.</param>
        /// <param name="deltaTimeProvider">계약이 없을 때 이번 실행이 덮는 시간(초)을 주는 것이며, 비우면 <see cref="Time.deltaTime"/>을 쓴다.</param>
        /// <param name="facing">바라보는 방향을 도맡는 계약이며, 있으면 이것을 통해서만 돈다.</param>
        /// <exception cref="ArgumentNullException">유닛이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public RotateToFaceBehaviour(
            Transform self,
            string targetKey,
            float precisionDegrees = 10f,
            float degreesPerSecond = 360f,
            Func<float> deltaTimeProvider = null,
            ICharacterFacing facing = null)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _targetKey = LeafTaskKeys.Require(targetKey, nameof(targetKey));
            _precisionDegrees = Mathf.Max(0f, precisionDegrees);
            _degreesPerSecond = degreesPerSecond;
            _deltaTimeProvider = deltaTimeProvider ?? (static () => Time.deltaTime);
            _facing = facing;
        }

        /// <summary>바라보는 방향을 도맡는 계약을 통해 도는지 여부이다.</summary>
        public bool UsesFacingContract => _facing != null;

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (!ContextValueReader.TryGetPosition(context.Context, _targetKey, out var target))
            {
                _facing?.ClearLookTarget();
                return BehaviourStatus.Failure;
            }

            var toTarget = target - _self.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                _facing?.ClearLookTarget();
                return BehaviourStatus.Failure;
            }

            return _facing != null ? TurnThroughContract(target) : TurnDirectly(toTarget);
        }

        /// <inheritdoc />
        /// <remarks>가로채여 되돌려지면 두었던 점을 지운다. 남겨 두면 다음 가지가 걷는 동안에도 그 점을 본다.</remarks>
        public void Reset() => _facing?.ClearLookTarget();

        /// <summary>계약에 바라볼 점을 두고, 마주 봤는지는 계약에 묻는다.</summary>
        /// <param name="target">마주 볼 세계 좌표이다.</param>
        /// <returns>마주 봤으면 성공, 아직이면 진행 중이다.</returns>
        private BehaviourStatus TurnThroughContract(Vector3 target)
        {
            if (_facing.AngleTo(target) <= _precisionDegrees)
            {
                _facing.ClearLookTarget();
                return BehaviourStatus.Success;
            }

            _facing.LookAt(target);
            return BehaviourStatus.Running;
        }

        /// <summary>계약이 없을 때 Transform을 직접 돌린다.</summary>
        /// <param name="toTarget">자기 자리에서 대상까지의 수평 벡터이다.</param>
        /// <returns>마주 봤으면 성공, 아직이면 진행 중이다.</returns>
        private BehaviourStatus TurnDirectly(Vector3 toTarget)
        {
            var desired = Quaternion.LookRotation(toTarget, Vector3.up);
            _self.rotation = _degreesPerSecond <= 0f
                ? desired
                : Quaternion.RotateTowards(_self.rotation, desired, _degreesPerSecond * Mathf.Max(0f, _deltaTimeProvider()));

            var forward = _self.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, toTarget) <= _precisionDegrees
                ? BehaviourStatus.Success
                : BehaviourStatus.Running;
        }
    }

    /// <summary>길을 찾지 않고 문맥의 자리를 향해 곧장 걸어간다.</summary>
    /// <remarks>
    /// 이동 수단을 거치지 않고 Transform을 직접 옮긴다. 벽이 있어도 뚫고 가므로, 길이 없는 곳을
    /// 잠깐 가로지르는 연출 같은 데에만 쓴다. 자리가 없으면 실패한다.
    /// </remarks>
    public sealed class MoveDirectlyTowardBehaviour : IBehaviour
    {
        private readonly Transform _self;
        private readonly string _targetKey;
        private readonly float _speed;
        private readonly float _acceptableRadius;
        private readonly Func<float> _deltaTimeProvider;

        /// <summary>옮길 유닛과 목표 키, 속도와 닿은 것으로 볼 거리를 지정한다.</summary>
        /// <param name="self">옮길 유닛의 Transform이다.</param>
        /// <param name="targetKey">목표 자리가 담긴 문맥 키이다.</param>
        /// <param name="speed">걷는 속도(미터/초)이다.</param>
        /// <param name="acceptableRadius">이 거리 안이면 닿은 것으로 본다(미터).</param>
        /// <param name="deltaTimeProvider">이번 실행이 덮는 시간(초)을 주는 것이며, 비우면 <see cref="Time.deltaTime"/>을 쓴다.</param>
        /// <exception cref="ArgumentNullException">유닛이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public MoveDirectlyTowardBehaviour(
            Transform self,
            string targetKey,
            float speed,
            float acceptableRadius = 0.1f,
            Func<float> deltaTimeProvider = null)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _targetKey = LeafTaskKeys.Require(targetKey, nameof(targetKey));
            _speed = Mathf.Max(0f, speed);
            _acceptableRadius = Mathf.Max(0f, acceptableRadius);
            _deltaTimeProvider = deltaTimeProvider ?? (static () => Time.deltaTime);
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (!ContextValueReader.TryGetPosition(context.Context, _targetKey, out var target))
            {
                return BehaviourStatus.Failure;
            }

            var offset = target - _self.position;
            var distance = offset.magnitude;
            if (distance <= _acceptableRadius)
            {
                return BehaviourStatus.Success;
            }

            var step = _speed * Mathf.Max(0f, _deltaTimeProvider());
            if (step >= distance)
            {
                _self.position = target;
                return BehaviourStatus.Success;
            }

            _self.position += offset / distance * step;
            return BehaviourStatus.Running;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }
    }

    /// <summary>문맥의 방향으로 한 걸음 나아간 좌표를 셈해 문맥에 적는 잎이다.</summary>
    /// <remarks>
    /// <para>
    /// 이동 자체는 하지 않으며, 뒤따르는 이동 자리가 이 좌표를 읽어 움직인다. 방향은 문맥의 키에서
    /// 읽고, 값이 없으면 유닛이 바라보는 쪽을 쓴다. 방향을 채우는 쪽이 바뀌어도 이 자리는 그대로다.
    /// </para>
    /// <para>
    /// 바닥 위의 유닛이므로 방향의 높이는 버린다. 방향이 영이면 갈 곳이 없으므로 실패한다.
    /// </para>
    /// </remarks>
    public sealed class StepAlongDirectionBehaviour : IBehaviour
    {
        /// <summary>정하지 않았을 때 한 걸음의 거리(미터)이다.</summary>
        public const float DefaultStepDistance = 5f;

        private readonly Transform _self;
        private readonly string _destinationKey;
        private readonly string _directionKey;
        private readonly float _stepDistance;

        /// <summary>기준 유닛과 읽고 쓸 키, 걸음의 거리를 지정한다.</summary>
        /// <param name="self">걸음의 기준이 되는 유닛의 Transform이다.</param>
        /// <param name="destinationKey">셈한 좌표를 적을 문맥 키이다.</param>
        /// <param name="directionKey">방향을 읽을 문맥 키이다.</param>
        /// <param name="stepDistance">한 걸음의 거리(미터)이다.</param>
        /// <exception cref="ArgumentNullException">유닛이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public StepAlongDirectionBehaviour(
            Transform self,
            string destinationKey,
            string directionKey,
            float stepDistance = DefaultStepDistance)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _destinationKey = LeafTaskKeys.Require(destinationKey, nameof(destinationKey));
            _directionKey = LeafTaskKeys.Require(directionKey, nameof(directionKey));
            _stepDistance = Mathf.Max(0.1f, stepDistance);
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (_self == null)
            {
                return BehaviourStatus.Failure;
            }

            var direction = ResolveDirection(context.Context);
            if (direction == Vector3.zero)
            {
                return BehaviourStatus.Failure;
            }

            context.Context.SetValue(_destinationKey, _self.position + direction * _stepDistance);
            return BehaviourStatus.Success;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }

        /// <summary>문맥의 방향을 읽고, 없으면 유닛의 정면을 쓴다.</summary>
        /// <param name="context">방향을 읽을 문맥이다.</param>
        /// <returns>높이를 버린 단위 방향이며, 정할 수 없으면 <see cref="Vector3.zero"/>이다.</returns>
        private Vector3 ResolveDirection(IBehaviourContext context)
        {
            var direction = context.TryGetValue<Vector3>(_directionKey, out var storedDirection)
                ? storedDirection
                : _self.forward;
            direction.y = 0f;
            return direction.sqrMagnitude <= Mathf.Epsilon ? Vector3.zero : direction.normalized;
        }
    }

    /// <summary>유닛의 자리에서 소리 파일을 한 번 튼다.</summary>
    /// <remarks>틀고 바로 성공한다. 소리가 끝날 때까지 기다리지 않는다.</remarks>
    public sealed class PlaySoundBehaviour : IBehaviour
    {
        private readonly AudioClip _clip;
        private readonly Transform _self;
        private readonly float _volume;

        /// <summary>틀 소리와 자리, 크기를 지정한다.</summary>
        /// <param name="clip">틀 소리이다.</param>
        /// <param name="self">소리가 날 자리인 유닛의 Transform이다.</param>
        /// <param name="volume">소리 크기(0~1)이다.</param>
        /// <exception cref="ArgumentNullException">소리나 유닛이 null이면 발생한다.</exception>
        public PlaySoundBehaviour(AudioClip clip, Transform self, float volume = 1f)
        {
            _clip = clip != null ? clip : throw new ArgumentNullException(nameof(clip));
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _volume = Mathf.Clamp01(volume);
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            AudioSource.PlayClipAtPoint(_clip, _self.position, _volume);
            return BehaviourStatus.Success;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }
    }

    /// <summary>잎들이 함께 쓰는 확인이다.</summary>
    internal static class LeafTaskKeys
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
    }
}
