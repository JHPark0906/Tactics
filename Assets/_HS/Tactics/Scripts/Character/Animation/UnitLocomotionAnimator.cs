using HS.Tactics.Character.Movement;
using UnityEngine;

namespace HS.Tactics.Character.Animation
{
    /// <summary>고정 스텝이 계산한 이동 속도를 자식 Animator의 Speed 파라미터에 전달한다.</summary>
    /// <remarks>
    /// 표시 전용 구성요소이며 이동기나 전투 판정에 값을 되돌려 쓰지 않는다.
    /// Animator와 컨트롤러의 파라미터를 읽을 수 있을 때 속도 파라미터 유무를 한 번 확인한다.
    /// 초기화 순서와 동적 외형 부착을 지원하기 위해 그전에는 프레임마다 준비 여부만 확인한다.
    /// Animator나 Speed 파라미터가 없으면 경고를 반복하지 않고 아무것도 하지 않는다.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlanarCharacterMover))]
    public sealed class UnitLocomotionAnimator : MonoBehaviour
    {
        /// <summary>컨트롤러가 걷기 여부를 읽는 실수 파라미터의 이름이다. 컨트롤러 에셋과 검사가 같은 이름을 본다.</summary>
        public const string SpeedParameterName = "Speed";

        private static readonly int SpeedParameterHash = Animator.StringToHash(SpeedParameterName);

        private PlanarCharacterMover _mover;
        private Animator _animator;
        private bool _isResolved;
        private bool _hasSpeedParameter;

        private void Awake()
        {
            _mover = GetComponent<PlanarCharacterMover>();
        }

        /// <summary>화면 프레임마다 이동기의 속도를 컨트롤러에 넣는다. 로직은 이 값을 읽지 않는다.</summary>
        private void Update()
        {
            if (!_isResolved && !TryResolveVisual())
            {
                return;
            }

            if (_hasSpeedParameter)
            {
                _animator.SetFloat(SpeedParameterHash, _mover.CurrentSpeed);
            }
        }

        /// <summary>
        /// 겉모습의 애니메이터를 찾고, 파라미터를 읽을 수 있게 된 프레임에 속도 파라미터 유무를 한 번 판정한다.
        /// </summary>
        /// <remarks>
        /// 파라미터 수가 0인 동안은 아직 컨트롤러 그래프가 서지 않은 것으로 보고 판정을 미룬다. 그 확인은 정수 하나를
        /// 읽는 것이라 매 프레임 해도 값이 싸고, 컨트롤러가 없는 겉모습에서는 조용히 계속 기다린다.
        /// </remarks>
        /// <returns>판정을 마쳤으면 true이다.</returns>
        private bool TryResolveVisual()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_animator == null || _animator.parameterCount == 0)
            {
                return false;
            }

            _hasSpeedParameter = HasSpeedParameter(_animator);
            _isResolved = true;
            return true;
        }

        /// <summary>붙은 컨트롤러가 속도 파라미터를 갖는지 본다.</summary>
        /// <param name="animator">확인할 애니메이터이다.</param>
        /// <returns>속도 파라미터가 있으면 true이다.</returns>
        private static bool HasSpeedParameter(Animator animator)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == SpeedParameterHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
