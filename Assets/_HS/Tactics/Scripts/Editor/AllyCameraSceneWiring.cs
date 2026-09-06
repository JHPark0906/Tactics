using HS.Tactics.Cameras;
using HS.Tactics.Placement;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Editor
{
    /// <summary>Level1.unity에 아군을 따라가는 Cinemachine 카메라를 실제로 배선하는 메뉴이다.</summary>
    /// <remarks>
    /// <para>
    /// 넷을 한다. ⑴ Main Camera에 <see cref="CinemachineBrain"/>을 붙인다 — 이것이 있어야 가상 카메라가 실제 카메라를 움직인다.
    /// ⑵ 아군 대상 그룹 "AllyTargetGroup"을 만든다. ⑶ 가상 카메라 "AllyFollowCamera"를 만들어 그 그룹을 추적 대상으로 물리고,
    /// 몸통(따라가기)·조준(바라보기)·그룹 프레이밍을 붙인다. ⑷ 같은 오브젝트에 <see cref="AllyFollowCameraGroup"/>을 붙여
    /// 그룹을 연결한다 — 배치·회수·사망에 맞춰 멤버를 넣고 빼는 것은 그 컴포넌트가 한다.
    /// </para>
    /// <para>
    /// <b>배치 단계의 화면은 지금 Main Camera가 보여 주는 것과 같아야 한다.</b> 그래서 가상 카메라의 자리·회전·렌즈를 Main Camera에서
    /// 그대로 읽고, 대상 그룹은 Main Camera가 지금 바라보는 바닥 위의 점에 세운다. 따라가기 오프셋은 「카메라 자리 − 그 점」이므로
    /// 그룹이 비어 그 점에 머무는 동안 가상 카메라는 지금 자리에 그대로 선다. 그룹의 위치 방식은 <c>GroupAverage</c>여야 한다 —
    /// <c>GroupCenter</c>는 멤버가 없을 때 경계 구의 기본값(원점)으로 그룹을 옮겨 카메라가 원점을 보게 된다.
    /// </para>
    /// <para>
    /// <b>프레이밍은 넓히는 쪽으로만 움직인다.</b> 그룹 프레이밍은 대상이 화면의 일정 비율을 차지하게 카메라를 밀고 당기며 시야각을
    /// 바꾸는데, 그룹이 비어 있거나 작으면 「채우려고」 카메라를 대상 위로 끌어당기고 시야각을 1도까지 좁힌다. 돌리 범위의 위(가까워지는
    /// 쪽)와 시야각 범위의 아래를 지금 값으로 막아 두면 그 계산은 「바꾸지 않음」으로 잘리고, 아군이 화면보다 넓게 퍼질 때만 물러나거나
    /// 넓힌다. 그래서 배치 단계 화면이 그대로이고, 아군을 담는 것은 지금 각도와 거리를 그대로 두고 따라가는 것으로 시작한다.
    /// </para>
    /// <para>
    /// <b>다만 그 「그대로」는 멤버가 한 번도 없었던 그룹에 한해 보장된다.</b> Cinemachine의 대상 그룹은 멤버가 모두 빠져도 마지막
    /// 자리와 마지막 경계 구를 그대로 들고 있어서(<c>CalculateAveragePosition</c>은 무게 합이 0이면 지금 자리를 돌려주고,
    /// <c>CalculateBoundingSphere</c>는 멤버가 없으면 이전 값을 돌려준다), 세웠던 아군이 모두 회수되거나 쓰러진 뒤에는 카메라가
    /// 마지막 자리를 보며 돌리 하한(10미터)까지 물러나고 시야각도 상한까지 넓어진다. 지금 흐름에서 그 상태에 닿는 것은 전멸뿐이고
    /// 그때는 곧 스테이지 선택으로 넘어가므로 여기서 다루지 않는다 — 멤버가 비는 순간을 되돌리려면 멤버를 넣고 빼는 쪽
    /// (<see cref="AllyFollowCameraGroup"/>)이 빈 상태를 알아채야 한다.
    /// </para>
    /// <para>
    /// <b>몇 번을 돌려도 같다.</b> 이미 있는 Brain·그룹·가상 카메라·연결 컴포넌트는 다시 만들지 않는다. 다만 그룹과 가상 카메라의
    /// 자리는 처음 만들 때의 Main Camera로 정하므로, 그 뒤에 Main Camera를 옮겼으면 이 둘을 지우고 다시 돌려야 새 자리를 따른다.
    /// </para>
    /// </remarks>
    public static class AllyCameraSceneWiring
    {
        private const string ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string TargetGroupObjectName = "AllyTargetGroup";
        private const string FollowCameraObjectName = "AllyFollowCamera";
        private const string TargetGroupPropertyName = "targetGroup";

        /// <summary>대상이 차지할 화면 비율이다. Cinemachine 기본값이며, 아군이 이보다 넓게 퍼질 때만 프레이밍이 움직인다.</summary>
        private const float FramingSize = 0.8f;

        /// <summary>아군이 퍼질 때 카메라가 물러날 수 있는 최대 거리(미터)이다. 지금 카메라 거리(약 7미터)의 한 배 반쯤이다.</summary>
        private const float MaxDollyOut = 10f;

        /// <summary>아군이 퍼질 때 시야각을 넓힐 수 있는 상한(도)이다. 지금 60도에서 20도까지만 넓힌다.</summary>
        private const float MaxFieldOfView = 80f;

        /// <summary>Level1.unity를 열어 아군 추적 카메라를 배선하고 저장한다. 이미 되어 있는 것은 그대로 둔다.</summary>
        [MenuItem("Tools/HS Tactics/Cameras/Wire Ally Follow Camera Into Level1")]
        public static void WireLevel1()
        {
            // 에디터에서 손으로 실행했을 때 열려 있던 씬의 미저장 변경을 묻지 않고 버리지 않도록 먼저 묻는다.
            // 배치모드에서는 물을 사람이 없어 그대로 지나간다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[AllyCameraSceneWiring] 열려 있던 씬의 저장을 취소해 Level1.unity는 손대지 않는다.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var mainCamera = FindMainCamera(scene);
            if (mainCamera == null)
            {
                Debug.LogError($"[AllyCameraSceneWiring] {ScenePath}에 카메라가 없어 배선을 멈춘다.");
                return;
            }

            var changed = EnsureBrain(mainCamera);
            var (targetGroup, createdGroup) = EnsureTargetGroup(scene, mainCamera);
            changed |= createdGroup;
            var (followCamera, createdCamera) = EnsureFollowCamera(scene, mainCamera, targetGroup);
            changed |= createdCamera;
            changed |= EnsureAllyGroupComponent(scene, followCamera, targetGroup);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AllyCameraSceneWiring] {ScenePath}를 저장했다.");
            }
            else
            {
                Debug.Log($"[AllyCameraSceneWiring] {ScenePath}에 바꿀 것이 없어 저장하지 않는다.");
            }
        }

        /// <summary>MainCamera 태그가 붙은 카메라를 먼저 찾고, 없으면 씬의 첫 카메라를 쓴다.</summary>
        private static Camera FindMainCamera(Scene scene)
        {
            Camera first = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.CompareTag("MainCamera"))
                    {
                        return camera;
                    }

                    first ??= camera;
                }
            }

            return first;
        }

        /// <summary>실제 카메라에 Brain이 없으면 붙인다.</summary>
        /// <returns>붙였으면 true이다.</returns>
        private static bool EnsureBrain(Camera mainCamera)
        {
            if (mainCamera.GetComponent<CinemachineBrain>() != null)
            {
                Debug.Log($"[AllyCameraSceneWiring] '{mainCamera.name}'에는 CinemachineBrain이 이미 있다.");
                return false;
            }

            mainCamera.gameObject.AddComponent<CinemachineBrain>();
            Debug.Log($"[AllyCameraSceneWiring] '{mainCamera.name}'에 CinemachineBrain을 붙였다.");
            return true;
        }

        /// <summary>
        /// 대상 그룹이 없으면 Main Camera가 지금 바라보는 바닥 위의 점에 만든다. 그룹이 비어 있는 동안 그 점에 머물러야
        /// 카메라가 지금 자리에 그대로 서므로, 위치 방식은 멤버가 없을 때 제자리를 지키는 <c>GroupAverage</c>로 둔다.
        /// </summary>
        /// <returns>그룹과, 새로 만들었는지 여부이다.</returns>
        private static (CinemachineTargetGroup, bool) EnsureTargetGroup(Scene scene, Camera mainCamera)
        {
            var existing = FindInScene<CinemachineTargetGroup>(scene);
            if (existing != null)
            {
                Debug.Log($"[AllyCameraSceneWiring] 대상 그룹 '{existing.name}'이 이미 있다.");
                return (existing, false);
            }

            var lookPoint = ResolveLookPoint(scene, mainCamera);
            var groupObject = new GameObject(TargetGroupObjectName);
            SceneManager.MoveGameObjectToScene(groupObject, scene);
            groupObject.transform.SetPositionAndRotation(lookPoint, Quaternion.identity);

            var group = groupObject.AddComponent<CinemachineTargetGroup>();
            group.PositionMode = CinemachineTargetGroup.PositionModes.GroupAverage;
            group.RotationMode = CinemachineTargetGroup.RotationModes.Manual;
            group.UpdateMethod = CinemachineTargetGroup.UpdateMethods.LateUpdate;

            Debug.Log($"[AllyCameraSceneWiring] 대상 그룹 '{TargetGroupObjectName}'을 {lookPoint}에 만들었다.");
            return (group, true);
        }

        /// <summary>
        /// 가상 카메라가 없으면 Main Camera의 자리·회전·렌즈를 그대로 물려받아 만들고, 그룹을 따라가며 바라보게 하고,
        /// 넓히는 쪽으로만 움직이는 그룹 프레이밍을 붙인다.
        /// </summary>
        /// <returns>가상 카메라와, 새로 만들었는지 여부이다.</returns>
        private static (CinemachineCamera, bool) EnsureFollowCamera(Scene scene, Camera mainCamera, CinemachineTargetGroup targetGroup)
        {
            var existing = FindInScene<CinemachineCamera>(scene);
            if (existing != null)
            {
                Debug.Log($"[AllyCameraSceneWiring] 가상 카메라 '{existing.name}'이 이미 있다.");
                return (existing, false);
            }

            var cameraTransform = mainCamera.transform;
            var cameraObject = new GameObject(FollowCameraObjectName);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.SetPositionAndRotation(cameraTransform.position, cameraTransform.rotation);

            var followCamera = cameraObject.AddComponent<CinemachineCamera>();
            followCamera.Target.TrackingTarget = targetGroup.transform;
            var lens = LensSettings.Default;
            lens.FieldOfView = mainCamera.fieldOfView;
            lens.NearClipPlane = mainCamera.nearClipPlane;
            lens.FarClipPlane = mainCamera.farClipPlane;
            followCamera.Lens = lens;

            // 몸통: 그룹에서 「지금 카메라 자리 − 그룹 자리」만큼 떨어져 따라간다. 그룹이 비어 제자리에 있으면 카메라도 제자리다.
            // 감쇠는 컴포넌트 기본값(축마다 1초)을 그대로 쓴다.
            var follow = cameraObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = cameraTransform.position - targetGroup.transform.position;

            // 조준: 그룹을 정확히 바라본다. 그룹이 지금 카메라가 보는 점에 있으므로 회전도 지금과 같다.
            cameraObject.AddComponent<CinemachineHardLookAt>();

            // 프레이밍: 가까워지는 돌리와 좁아지는 시야각을 막아 두어, 아군이 화면보다 넓게 퍼질 때만 물러나거나 넓힌다.
            var framing = cameraObject.AddComponent<CinemachineGroupFraming>();
            framing.FramingMode = CinemachineGroupFraming.FramingModes.HorizontalAndVertical;
            framing.FramingSize = FramingSize;
            framing.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.DollyThenZoom;
            framing.LateralAdjustment = CinemachineGroupFraming.LateralAdjustmentModes.ChangePosition;
            framing.DollyRange = new Vector2(-MaxDollyOut, 0f);
            framing.FovRange = new Vector2(mainCamera.fieldOfView, Mathf.Max(mainCamera.fieldOfView, MaxFieldOfView));

            Debug.Log(
                $"[AllyCameraSceneWiring] 가상 카메라 '{FollowCameraObjectName}'을 만들었다. 자리 {cameraTransform.position}, " +
                $"오프셋 {follow.FollowOffset}, 시야각 {lens.FieldOfView}→최대 {framing.FovRange.y}, 돌리 {framing.DollyRange}.");
            return (followCamera, true);
        }

        /// <summary>가상 카메라 오브젝트에 <see cref="AllyFollowCameraGroup"/>이 없으면 붙이고, 그룹 연결이 비었거나 다르면 맞춘다.</summary>
        /// <returns>붙였거나 연결을 바꿨으면 true이다.</returns>
        private static bool EnsureAllyGroupComponent(Scene scene, CinemachineCamera followCamera, CinemachineTargetGroup targetGroup)
        {
            var changed = false;
            var allyGroup = FindInScene<AllyFollowCameraGroup>(scene);
            if (allyGroup == null)
            {
                allyGroup = followCamera.gameObject.AddComponent<AllyFollowCameraGroup>();
                changed = true;
                Debug.Log($"[AllyCameraSceneWiring] '{followCamera.name}'에 AllyFollowCameraGroup을 붙였다.");
            }

            var serialized = new SerializedObject(allyGroup);
            var property = serialized.FindProperty(TargetGroupPropertyName);
            if (property == null)
            {
                Debug.LogError(
                    $"[AllyCameraSceneWiring] AllyFollowCameraGroup에 '{TargetGroupPropertyName}' 필드가 없어 그룹을 연결하지 못한다. " +
                    "이름이 바뀌었으면 이 메뉴와 씬 검사를 함께 고쳐야 한다.");
                return changed;
            }

            if (property.objectReferenceValue != targetGroup)
            {
                property.objectReferenceValue = targetGroup;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
                Debug.Log($"[AllyCameraSceneWiring] AllyFollowCameraGroup의 대상 그룹을 '{targetGroup.name}'으로 연결했다.");
            }

            return changed;
        }

        /// <summary>
        /// Main Camera의 시선이 배치 구역의 바닥 높이와 만나는 점을 구한다. 구역이 없으면 높이 0의 바닥을 쓰고,
        /// 카메라가 아래를 보고 있지 않아 만나지 않으면 구역 중심(없으면 카메라 앞 10미터)으로 물러난다.
        /// </summary>
        private static Vector3 ResolveLookPoint(Scene scene, Camera mainCamera)
        {
            var zone = FindInScene<UnitPlacementZone>(scene);
            var groundHeight = zone != null ? zone.Area.Center.y : 0f;
            var origin = mainCamera.transform.position;
            var forward = mainCamera.transform.forward;

            if (forward.y < -1e-4f)
            {
                var distance = (origin.y - groundHeight) / -forward.y;
                if (distance > 0f)
                {
                    return origin + forward * distance;
                }
            }

            return zone != null ? zone.Area.Center : origin + forward * 10f;
        }

        /// <summary>씬의 뿌리 오브젝트들을 훑어 컴포넌트를 찾는다. 비활성 오브젝트도 포함한다.</summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
