using System.Collections.Generic;
using HS.Tactics.Cameras;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// Level1.unity에 아군 추적 카메라가 실제로 배선되어 있는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 아군을 따라가는 카메라는 컴포넌트 하나로 끝나지 않는다 — 실제 카메라의 Brain, 대상 그룹, 그 그룹을 추적하는 가상 카메라,
    /// 그룹 프레이밍, 그리고 멤버를 넣고 빼는 <see cref="AllyFollowCameraGroup"/>이 씬에서 서로 연결되어 있어야 움직인다.
    /// 그 연결은 씬 파일에만 있어 코드 검사로는 보이지 않으므로, 씬을 열어 직접 확인한다. 필수 씬 연결이 없으면 검증에 실패한다.
    /// </para>
    /// <para>
    /// 배치 단계의 카메라가 씬의 기준 자세를 유지하는지도 확인한다. 다만 <b>가상 카메라의 트랜스폼을 재는 것으로는 그것을 볼 수 없다</b> —
    /// Cinemachine은 매 갱신에 따라가기 오프셋과 바라보기로 자리와 회전을 다시 계산해 그 트랜스폼을 덮어쓴다. 그래서 실제로 화면을
    /// 정하는 값을 잰다: 「그룹 자리 + 따라가기 오프셋」이 실제 카메라 자리와 같은지, 카메라에서 그룹을 향한 방향이 실제 카메라가
    /// 보는 방향과 같은지, 그리고 프레이밍이 넓히는 쪽으로만 움직이도록 잠근 두 값(가까워지는 돌리 0, 시야각 하한이 지금 시야각)이
    /// 그대로인지.
    /// </para>
    /// <para>
    /// 씬은 덧붙여 열고 끝나면 닫아 열려 있던 씬을 그대로 둔다.
    /// </para>
    /// </remarks>
    public sealed class Level1AllyCameraWiringTests
    {
        private const string Level1ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string TargetGroupPropertyName = "targetGroup";
        private const float PoseTolerance = 1e-3f;

        /// <summary>바라보는 방향을 견주는 허용 오차(도)이다. 자리에서 방향을 다시 구하는 동안 쌓이는 부동소수 오차만 넘긴다.</summary>
        private const float DirectionToleranceDegrees = 0.05f;

        private readonly List<Scene> _openedScenes = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var scene in _openedScenes)
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            _openedScenes.Clear();
        }

        [Test]
        public void TheMainCameraCarriesACinemachineBrain()
        {
            var scene = OpenLevel1();
            var mainCamera = FindMainCamera(scene);

            Assert.That(mainCamera.GetComponent<CinemachineBrain>(), Is.Not.Null,
                $"'{mainCamera.name}'에 CinemachineBrain이 없다. Brain이 없으면 가상 카메라가 실제 카메라를 움직이지 못한다.");
        }

        [Test]
        public void ExactlyOneVirtualCameraTracksTheTargetGroupWithGroupFraming()
        {
            var scene = OpenLevel1();
            var virtualCamera = RequireSingleVirtualCamera(scene);
            var trackingTarget = virtualCamera.Target.TrackingTarget;
            Assert.That(trackingTarget, Is.Not.Null, $"'{virtualCamera.name}'의 추적 대상이 비어 있다.");
            Assert.That(trackingTarget.GetComponent<CinemachineTargetGroup>(), Is.Not.Null,
                $"'{virtualCamera.name}'의 추적 대상 '{trackingTarget.name}'이 CinemachineTargetGroup이 아니다.");
            Assert.That(virtualCamera.GetComponent<CinemachineGroupFraming>(), Is.Not.Null,
                $"'{virtualCamera.name}'에 CinemachineGroupFraming이 없다.");
            Assert.That(virtualCamera.GetCinemachineComponent(CinemachineCore.Stage.Body), Is.Not.Null,
                $"'{virtualCamera.name}'에 몸통(따라가기) 컴포넌트가 없다.");
            Assert.That(virtualCamera.GetCinemachineComponent(CinemachineCore.Stage.Aim), Is.Not.Null,
                $"'{virtualCamera.name}'에 조준(바라보기) 컴포넌트가 없다.");
        }

        [Test]
        public void TheAllyGroupComponentIsWiredToTheTrackedGroup()
        {
            var scene = OpenLevel1();
            var virtualCamera = RequireSingleVirtualCamera(scene);
            var allyGroups = FindAllInScene<AllyFollowCameraGroup>(scene);
            Assert.That(allyGroups.Count, Is.EqualTo(1),
                $"AllyFollowCameraGroup이 {allyGroups.Count}개다. 하나여야 멤버를 넣고 빼는 주체가 하나다.");

            var wiredGroup = new SerializedObject(allyGroups[0]).FindProperty(TargetGroupPropertyName);
            Assert.That(wiredGroup, Is.Not.Null,
                $"AllyFollowCameraGroup에 {TargetGroupPropertyName} 필드가 있어야 한다. 이름이 바뀌었으면 이 검사와 에디터 메뉴를 함께 고쳐야 한다.");
            Assert.That(wiredGroup.objectReferenceValue, Is.Not.Null, "AllyFollowCameraGroup의 대상 그룹이 비어 있다.");
            Assert.That(wiredGroup.objectReferenceValue, Is.SameAs(virtualCamera.Target.TrackingTarget.GetComponent<CinemachineTargetGroup>()),
                "AllyFollowCameraGroup이 연결한 그룹과 가상 카메라가 추적하는 그룹이 다르다. 멤버를 넣어도 카메라가 보지 않는다.");
        }

        /// <summary>
        /// 그룹이 비어 있는 배치 단계에서 Cinemachine이 실제 카메라를 지금 자리에 그대로 두는지, 그것을 정하는 값들로 확인한다.
        /// </summary>
        /// <remarks>
        /// 따라가기는 「그룹 자리 + 오프셋」을 카메라 자리로 삼고, 바라보기는 카메라에서 그룹을 향하게 회전을 세운다. 그래서 이 둘이
        /// 실제 카메라의 자리·방향과 같으면 그룹이 제자리에 있는 동안 화면이 지금과 같다. 프레이밍은 그 위에서 카메라를 밀고 당기는데,
        /// 가까워지는 돌리와 좁아지는 시야각이 막혀 있어야 「넓히는 쪽으로만」이 성립한다.
        /// </remarks>
        [Test]
        public void TheFollowRigReproducesTheMainCameraViewWhileTheGroupIsEmpty()
        {
            var scene = OpenLevel1();
            var mainCamera = FindMainCamera(scene);
            var virtualCamera = RequireSingleVirtualCamera(scene);

            var group = virtualCamera.Target.TrackingTarget;
            Assert.That(group, Is.Not.Null, $"'{virtualCamera.name}'의 추적 대상이 비어 있어 따라가기 자리를 셀 수 없다.");
            var follow = virtualCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachineFollow;
            Assert.That(follow, Is.Not.Null, $"'{virtualCamera.name}'의 몸통이 CinemachineFollow가 아니라 따라가기 오프셋을 읽을 수 없다.");

            var followedPosition = group.position + follow.FollowOffset;
            Assert.That(Vector3.Distance(followedPosition, mainCamera.transform.position), Is.LessThan(PoseTolerance),
                $"따라가기가 세울 자리는 {followedPosition}이고 실제 카메라 자리는 {mainCamera.transform.position}이다.");

            var towardGroup = Quaternion.LookRotation(group.position - mainCamera.transform.position, Vector3.up);
            Assert.That(Quaternion.Angle(towardGroup, mainCamera.transform.rotation), Is.LessThan(DirectionToleranceDegrees),
                $"그룹을 향한 방향은 {towardGroup.eulerAngles}이고 실제 카메라가 보는 방향은 {mainCamera.transform.rotation.eulerAngles}이다.");

            Assert.That(virtualCamera.Lens.FieldOfView, Is.EqualTo(mainCamera.fieldOfView).Within(PoseTolerance),
                $"가상 카메라 시야각 {virtualCamera.Lens.FieldOfView}, 실제 카메라 시야각 {mainCamera.fieldOfView}.");

            var framing = virtualCamera.GetComponent<CinemachineGroupFraming>();
            Assert.That(framing.DollyRange.y, Is.EqualTo(0f).Within(PoseTolerance),
                $"돌리 범위의 위가 {framing.DollyRange.y}이다. 0이 아니면 프레이밍이 아군 쪽으로 다가와 배치 단계 화면이 달라진다.");
            Assert.That(framing.FovRange.x, Is.EqualTo(mainCamera.fieldOfView).Within(PoseTolerance),
                $"시야각 범위의 아래가 {framing.FovRange.x}이고 실제 카메라 시야각은 {mainCamera.fieldOfView}이다. 더 낮으면 프레이밍이 화면을 좁힌다.");
        }

        /// <summary>가상 카메라가 하나뿐인지 확인하고 그것을 돌려준다. 하나가 아니면 어느 것이 화면을 잡는지 정해지지 않는다.</summary>
        private static CinemachineCamera RequireSingleVirtualCamera(Scene scene)
        {
            var virtualCameras = FindAllInScene<CinemachineCamera>(scene);
            Assert.That(virtualCameras.Count, Is.EqualTo(1),
                $"가상 카메라가 {virtualCameras.Count}개다. 아군 추적 카메라 하나만 있어야 어느 것이 화면을 잡는지 정해진다.");
            return virtualCameras[0];
        }

        private Scene OpenLevel1()
        {
            var scene = EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Additive);
            _openedScenes.Add(scene);
            return scene;
        }

        /// <summary>MainCamera 태그가 붙은 카메라를 찾고, 없으면 첫 카메라를 쓴다. 카메라가 하나도 없으면 무대가 잘못된 것이다.</summary>
        private static Camera FindMainCamera(Scene scene)
        {
            var cameras = FindAllInScene<Camera>(scene);
            Assert.That(cameras.Count, Is.GreaterThan(0), $"무대 확인: {Level1ScenePath}에 카메라가 없다.");
            var tagged = cameras.Find(camera => camera.CompareTag("MainCamera"));
            return tagged != null ? tagged : cameras[0];
        }

        private static List<T> FindAllInScene<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return found;
        }
    }
}
