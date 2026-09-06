using System.Collections.Generic;
using HS.Framework.Character;
using HS.Tactics.Character.Animation;
using HS.Tactics.Character.Movement;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 걷기 애니메이션 다리(<see cref="UnitLocomotionAnimator"/>)가 이동기의 속도를 컨트롤러의 속도 파라미터에
    /// 그대로 흘리고, 겉모습이 없거나 파라미터가 없으면 조용히 손을 떼는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 다리는 프레임마다 읽기만 하므로, 검사는 이동기를 한 스텝 굴려 속도를 만든 뒤 다리의 Update를 직접 구동하고
    /// 애니메이터에서 파라미터 값을 되읽는다. 컨트롤러는 에디터 API로 메모리에 만들어 저장하지 않는다.
    /// </para>
    /// <para>
    /// 에디터는 보통의 스크립트를 깨우지 않으므로 Awake·Update를 <see cref="MonoBehaviourLifecycle"/>로 구동한다.
    /// </para>
    /// </remarks>
    public sealed class UnitLocomotionAnimatorTests
    {
        private const float StepDuration = 1f / 30f;

        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TheMoverSpeedIsWrittenToTheSpeedParameter()
        {
            var (mover, bridge) = CreateUnit();
            var animator = AttachVisual(mover.gameObject, withSpeedParameter: true);
            MonoBehaviourLifecycle.InvokeAwake(bridge);
            StartWalking(mover);
            Assert.That(mover.CurrentSpeed, Is.GreaterThan(0f), "무대 확인: 한 스텝 굴린 뒤에는 움직이고 있어야 한다.");

            MonoBehaviourLifecycle.InvokeUpdate(bridge);

            Assert.That(
                animator.GetFloat(UnitLocomotionAnimator.SpeedParameterName),
                Is.EqualTo(mover.CurrentSpeed).Within(0.0001f),
                "이동기의 속도가 그대로 속도 파라미터에 적혀야 한다.");
        }

        [Test]
        public void StoppingBringsTheSpeedParameterBackToZero()
        {
            var (mover, bridge) = CreateUnit();
            var animator = AttachVisual(mover.gameObject, withSpeedParameter: true);
            MonoBehaviourLifecycle.InvokeAwake(bridge);
            StartWalking(mover);
            MonoBehaviourLifecycle.InvokeUpdate(bridge);
            Assert.That(animator.GetFloat(UnitLocomotionAnimator.SpeedParameterName), Is.GreaterThan(0f), "무대 확인: 걷는 동안은 0이 아니어야 한다.");

            // 멈추라는 요청 뒤에도 속도는 가속도가 허락하는 만큼씩만 줄어들므로, 0에 닿을 때까지 스텝을 굴린다.
            mover.Stop();
            for (var step = 0; step < 100 && mover.CurrentSpeed > 0f; step++)
            {
                mover.Tick(StepDuration);
            }

            MonoBehaviourLifecycle.InvokeUpdate(bridge);

            Assert.That(mover.CurrentSpeed, Is.Zero, "무대 확인: 충분히 굴리면 속도는 0에 닿는다.");
            Assert.That(
                animator.GetFloat(UnitLocomotionAnimator.SpeedParameterName),
                Is.Zero,
                "멈추면 속도 파라미터도 0이어야 걷기에서 서 있기로 돌아온다.");
        }

        [Test]
        public void AVisualThatAppearsAfterAwakeIsStillPickedUp()
        {
            // 유닛은 비활성으로 조립되어 나중에 깨어나고, 자식 애니메이터의 컨트롤러 그래프는 그 애니메이터가 켜질 때에야
            // 선다. 뿌리의 Awake에서 파라미터를 읽어 판정을 굳히면 "속도 파라미터가 없다"로 굳어 경고 한 줄 없이 영원히
            // 아무것도 넣지 않는다. 여기서는 겉모습이 Awake보다 늦게 붙는 순서를 그대로 재현한다.
            var (mover, bridge) = CreateUnit();
            MonoBehaviourLifecycle.InvokeAwake(bridge);
            StartWalking(mover);
            MonoBehaviourLifecycle.InvokeUpdate(bridge);

            var animator = AttachVisual(mover.gameObject, withSpeedParameter: true);
            MonoBehaviourLifecycle.InvokeUpdate(bridge);

            Assert.That(
                animator.GetFloat(UnitLocomotionAnimator.SpeedParameterName),
                Is.EqualTo(mover.CurrentSpeed).Within(0.0001f),
                "Awake 뒤에 붙은 겉모습도 찾아야 한다. 판정을 Awake에서 굳히면 여기가 0으로 남는다.");
        }

        [Test]
        public void WithoutAnAnimatorTheBridgeDoesNothingAndStaysQuiet()
        {
            var (mover, bridge) = CreateUnit();
            MonoBehaviourLifecycle.InvokeAwake(bridge);
            StartWalking(mover);

            Assert.DoesNotThrow(() => MonoBehaviourLifecycle.InvokeUpdate(bridge), "겉모습이 없는 유닛에서도 다리는 조용히 아무것도 하지 않아야 한다.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AControllerWithoutTheSpeedParameterIsLeftAlone()
        {
            var (mover, bridge) = CreateUnit();
            AttachVisual(mover.gameObject, withSpeedParameter: false);
            MonoBehaviourLifecycle.InvokeAwake(bridge);
            StartWalking(mover);

            Assert.DoesNotThrow(() => MonoBehaviourLifecycle.InvokeUpdate(bridge), "속도 파라미터가 없는 컨트롤러에는 값을 넣지 않아야 매 프레임 경고가 나지 않는다.");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>이동기와 다리를 갖춘 유닛을 만들고 캐릭터 초기화로 이동기를 깨운다.</summary>
        private (PlanarCharacterMover mover, UnitLocomotionAnimator bridge) CreateUnit()
        {
            var unitObject = Track(new GameObject("Unit"));
            var character = unitObject.AddComponent<TestCharacter>();
            var mover = unitObject.AddComponent<PlanarCharacterMover>();
            var bridge = unitObject.AddComponent<UnitLocomotionAnimator>();
            MonoBehaviourLifecycle.InvokeAwake(character);
            return (mover, bridge);
        }

        /// <summary>
        /// 유닛의 자식으로 겉모습(애니메이터)을 붙인다. 컨트롤러는 메모리에 만들며, 속도 파라미터를 넣을지 고를 수 있다.
        /// </summary>
        private Animator AttachVisual(GameObject unitObject, bool withSpeedParameter)
        {
            var visual = Track(new GameObject("Visual"));
            visual.transform.SetParent(unitObject.transform, false);
            var animator = visual.AddComponent<Animator>();

            var controller = Track(new AnimatorController());
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle");
            if (withSpeedParameter)
            {
                controller.AddParameter(UnitLocomotionAnimator.SpeedParameterName, AnimatorControllerParameterType.Float);
            }

            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            return animator;
        }

        /// <summary>직선 경로를 끼우고 멀리 목적지를 준 뒤 한 스텝 굴려 이동기가 실제로 움직이게 한다.</summary>
        private static void StartWalking(PlanarCharacterMover mover)
        {
            mover.SetPathPlanner((from, to, corners) =>
            {
                corners.Add(from);
                corners.Add(to);
                return true;
            });
            Assert.That(mover.MoveTo(new Vector3(0f, 0f, 20f)), Is.True, "무대 확인: 목적지가 받아들여져야 한다.");
            mover.Tick(StepDuration);
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>이동기 초기화를 구동하기 위한 최소 캐릭터 오케스트레이터이다.</summary>
        private sealed class TestCharacter : CharacterBase
        {
        }
    }
}
