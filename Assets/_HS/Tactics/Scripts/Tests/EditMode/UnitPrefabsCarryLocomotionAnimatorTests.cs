using HS.Tactics.Character.Animation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 유닛 프리팹이 UnitLocomotionAnimator와 Speed 파라미터를 가진 Animator 컨트롤러를 갖추는지 검증한다.
    /// 컴포넌트 단독 검사는 프리팹 연결을 보장하지 않으므로 실제 에셋을 연다.
    /// 특정 컨트롤러 에셋의 이름 대신 속도 파라미터 계약을 확인한다.
    /// </summary>
    public sealed class UnitPrefabsCarryLocomotionAnimatorTests
    {
        private const string GruntPrefabPath = "Assets/_HS/Tactics/Prefabs/Grunt.prefab";
        private const string RiflemanPrefabPath = "Assets/_HS/Tactics/Prefabs/Rifleman.prefab";

        /// <summary>꺼져 있어야 하는 데모 스크립트의 형식 이름이다. 서드파티 어셈블리를 참조하지 않으므로 이름으로 찾는다.</summary>
        private const string DemoControllerTypeName = "KevinIglesias.HumanSoldierController";

        [TestCase(GruntPrefabPath)]
        [TestCase(RiflemanPrefabPath)]
        public void TheUnitPrefabCarriesTheLocomotionBridge(string prefabPath)
        {
            var prefab = LoadPrefab(prefabPath);

            Assert.That(
                prefab.GetComponent<UnitLocomotionAnimator>(),
                Is.Not.Null,
                $"{prefabPath}의 뿌리에 UnitLocomotionAnimator가 없다. 다리가 없으면 유닛이 걸어도 겉모습은 서 있다.");
        }

        [TestCase(GruntPrefabPath)]
        [TestCase(RiflemanPrefabPath)]
        public void TheUnitPrefabVisualHasAControllerThatReadsTheSpeedParameter(string prefabPath)
        {
            var prefab = LoadPrefab(prefabPath);

            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, $"{prefabPath}의 겉모습에 Animator가 없다.");

            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null, $"{prefabPath}의 Animator에 컨트롤러가 없거나 에디터에서 열 수 있는 컨트롤러가 아니다.");
            Assert.That(
                controller.parameters,
                Has.Some.Matches<AnimatorControllerParameter>(parameter =>
                    parameter.name == UnitLocomotionAnimator.SpeedParameterName &&
                    parameter.type == AnimatorControllerParameterType.Float),
                $"{prefabPath}의 컨트롤러에 실수 파라미터 '{UnitLocomotionAnimator.SpeedParameterName}'이 없다. 다리가 넣는 속도를 아무도 읽지 않는다.");
        }

        [TestCase(GruntPrefabPath)]
        [TestCase(RiflemanPrefabPath)]
        public void TheUnitPrefabVisualDoesNotDriveItsOwnAnimator(string prefabPath)
        {
            // 데모 스크립트는 전용 트리거를 발행하므로 Speed 기반 컨트롤러와 함께 실행하지 않도록 꺼져 있어야 한다.
            var prefab = LoadPrefab(prefabPath);

            foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                Assert.That(
                    behaviour.GetType().FullName == DemoControllerTypeName && behaviour.enabled,
                    Is.False,
                    $"{prefabPath}의 겉모습에서 {DemoControllerTypeName}이 켜져 있다. 이동기 대신 이 스크립트가 애니메이터를 몬다.");
            }
        }

        [TestCase(GruntPrefabPath)]
        [TestCase(RiflemanPrefabPath)]
        public void TheUnitPrefabVisualDoesNotApplyRootMotion(string prefabPath)
        {
            // 위치를 옮기는 것은 이동기다. 루트 모션이 켜져 있으면 클립이 유닛을 따로 밀어 로직 위치와 어긋난다.
            var prefab = LoadPrefab(prefabPath);

            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, $"{prefabPath}의 겉모습에 Animator가 없다.");
            Assert.That(
                animator.applyRootMotion,
                Is.False,
                $"{prefabPath}의 Animator가 루트 모션을 적용한다. 위치는 이동기가 정한다.");
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"유닛 프리팹이 {prefabPath}에 있어야 한다.");
            return prefab;
        }
    }
}
