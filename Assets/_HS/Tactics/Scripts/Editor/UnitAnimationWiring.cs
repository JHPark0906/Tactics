using System.Linq;
using HS.Tactics.Character.Animation;
using HS.Tactics.Character.Movement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HS.Tactics.Editor
{
    /// <summary>
    /// 유닛이 걸을 때 걷는 동작이 보이도록, 서 있기·걷기 두 상태짜리 컨트롤러를 만들고 두 유닛 프리팹에
    /// 걷기 애니메이션 다리를 붙여 겉모습의 애니메이터에 그 컨트롤러를 거는 메뉴이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>왜 우리 컨트롤러인가.</b> 유닛 프리팹이 품은 서드파티 데모 프리팹은 트리거 30개짜리 데모 컨트롤러와, 그 트리거를
    /// 매 프레임 다시 쏘는 데모 스크립트(<c>KevinIglesias.HumanSoldierController</c>)를 들고 있다. 이동기는 속도 하나만
    /// 알므로, 그 구조에 묶이는 대신 실수 파라미터 <see cref="UnitLocomotionAnimator.SpeedParameterName"/> 하나로 서 있기와
    /// 걷기를 오가는 우리 컨트롤러를 두고 프리팹 오버라이드로 갈아 끼운다. 겉모습을 진짜 에셋으로 바꿀 때 다시 거는 것도
    /// 이 메뉴 하나다.
    /// </para>
    /// <para>
    /// <b>데모 스크립트는 끈다.</b> 켜 둔 채 우리 컨트롤러를 걸면 있지도 않은 트리거를 매 프레임 쏘며 경고를 채운다.
    /// 서드파티 원본은 손대지 않고 우리 프리팹 인스턴스의 오버라이드로만 끈다. 척추 프록시(<c>SpineProxy</c>)는 애니메이션
    /// 표시를 돕는 것이라 그대로 둔다.
    /// </para>
    /// <para>
    /// <b>클립은 제자리 걷기다.</b> 위치를 옮기는 것은 이동기이므로 루트 모션이 구워진 <c>[RM]</c> 변형이 아니라 제자리
    /// 클립을 쓰고, 애니메이터의 루트 모션 적용도 끈다. 걷기 클립의 발 속도와 유닛의 이동 속도는 맞추지 않는다 — 지금
    /// 겉모습은 검증용 임시 에셋이다.
    /// </para>
    /// <para>
    /// <b>몇 번을 돌려도 같다.</b> 컨트롤러가 있으면 빠진 조각만 채우고, 다리가 있으면 붙이지 않으며, 이미 걸린 컨트롤러면
    /// 다시 걸지 않는다. 배치모드에서 안전하게 재실행할 수 있다.
    /// </para>
    /// </remarks>
    public static class UnitAnimationWiring
    {
        private const string GruntPrefabPath = "Assets/_HS/Tactics/Prefabs/Grunt.prefab";
        private const string RiflemanPrefabPath = "Assets/_HS/Tactics/Prefabs/Rifleman.prefab";

        private const string ControllerParentFolder = "Assets/_HS/Tactics";
        private const string ControllerFolderName = "Animation";
        private const string ControllerPath = ControllerParentFolder + "/" + ControllerFolderName + "/UnitLocomotion.controller";

        private const string IdleClipAssetPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@MilitaryIdle01.fbx";
        private const string IdleClipName = "HumanM@MilitaryIdle01";
        private const string WalkClipAssetPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string WalkClipName = "HumanM@Walk01_Forward";

        private const string IdleStateName = "Idle";
        private const string WalkStateName = "Walk";

        /// <summary>이 속도(초당 미터)를 넘으면 걷기로, 아래로 내려오면 서 있기로 간다. 이동기가 멈춘 뒤 속도가 0으로 줄어드는 몇 스텝을 덮는 값이다.</summary>
        private const float WalkSpeedThreshold = 0.1f;

        /// <summary>두 상태를 섞어 넘어가는 시간(초)이다. 발이 갑자기 바뀌지 않을 만큼만 짧다.</summary>
        private const float TransitionDuration = 0.15f;

        /// <summary>끌 데모 스크립트의 형식 이름이다. 서드파티 어셈블리를 참조하지 않으므로 이름으로 찾는다.</summary>
        private const string DemoControllerTypeName = "KevinIglesias.HumanSoldierController";

        private static readonly string[] UnitPrefabPaths = { GruntPrefabPath, RiflemanPrefabPath };

        /// <summary>컨트롤러를 갖추고 두 유닛 프리팹에 다리와 컨트롤러를 건 뒤 저장한다. 이미 되어 있는 것은 그대로 둔다.</summary>
        [MenuItem("Tools/HS Tactics/Units/Wire Locomotion Animation Into Unit Prefabs")]
        public static void WireUnitPrefabs()
        {
            var controller = EnsureController();
            if (controller == null)
            {
                Debug.LogError("[UnitAnimationWiring] 컨트롤러를 갖추지 못해 프리팹은 손대지 않는다.");
                return;
            }

            foreach (var prefabPath in UnitPrefabPaths)
            {
                WirePrefab(prefabPath, controller);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 서 있기·걷기 두 상태와 속도 파라미터를 가진 컨트롤러를 갖춘다. 없으면 만들고, 있으면 빠진 조각만 채운다.
        /// </summary>
        /// <returns>갖춘 컨트롤러이며, 클립을 찾지 못했으면 null이다.</returns>
        private static AnimatorController EnsureController()
        {
            var idleClip = LoadClip(IdleClipAssetPath, IdleClipName);
            var walkClip = LoadClip(WalkClipAssetPath, WalkClipName);
            if (idleClip == null || walkClip == null)
            {
                return null;
            }

            if (!AssetDatabase.IsValidFolder(ControllerParentFolder + "/" + ControllerFolderName))
            {
                AssetDatabase.CreateFolder(ControllerParentFolder, ControllerFolderName);
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var changed = false;
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                changed = true;
                Debug.Log($"[UnitAnimationWiring] {ControllerPath}를 새로 만든다.");
            }

            changed |= EnsureSpeedParameter(controller);

            var stateMachine = controller.layers[0].stateMachine;
            var idleState = EnsureState(stateMachine, IdleStateName, idleClip, ref changed);
            var walkState = EnsureState(stateMachine, WalkStateName, walkClip, ref changed);
            if (stateMachine.defaultState != idleState)
            {
                stateMachine.defaultState = idleState;
                changed = true;
            }

            changed |= EnsureTransition(idleState, walkState, AnimatorConditionMode.Greater);
            changed |= EnsureTransition(walkState, idleState, AnimatorConditionMode.Less);

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log($"[UnitAnimationWiring] {ControllerPath}를 저장했다.");
            }
            else
            {
                Debug.Log($"[UnitAnimationWiring] {ControllerPath}에 바꿀 것이 없다.");
            }

            return controller;
        }

        /// <summary>속도 실수 파라미터가 없으면 더한다.</summary>
        /// <returns>더했으면 true이다.</returns>
        private static bool EnsureSpeedParameter(AnimatorController controller)
        {
            if (controller.parameters.Any(parameter =>
                    parameter.name == UnitLocomotionAnimator.SpeedParameterName &&
                    parameter.type == AnimatorControllerParameterType.Float))
            {
                return false;
            }

            controller.AddParameter(UnitLocomotionAnimator.SpeedParameterName, AnimatorControllerParameterType.Float);
            return true;
        }

        /// <summary>이름이 같은 상태가 없으면 만들고, 모션이 다르면 바꾼다.</summary>
        private static AnimatorState EnsureState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, ref bool changed)
        {
            var state = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate != null && candidate.name == stateName);
            if (state == null)
            {
                state = stateMachine.AddState(stateName);
                changed = true;
            }

            if (state.motion != clip)
            {
                state.motion = clip;
                changed = true;
            }

            return state;
        }

        /// <summary>
        /// 두 상태 사이에 전이가 없으면 속도 파라미터를 조건으로 하는 전이를 더한다. 이미 있으면 손대지 않는다.
        /// </summary>
        /// <returns>더했으면 true이다.</returns>
        private static bool EnsureTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            if (from.transitions.Any(transition => transition.destinationState == to))
            {
                return false;
            }

            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = TransitionDuration;
            transition.AddCondition(mode, WalkSpeedThreshold, UnitLocomotionAnimator.SpeedParameterName);
            return true;
        }

        /// <summary>FBX 안의 클립을 이름으로 찾는다. 없으면 오류를 남기고 null이다.</summary>
        private static AnimationClip LoadClip(string assetPath, string clipName)
        {
            var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.name == clipName);
            if (clip == null)
            {
                Debug.LogError($"[UnitAnimationWiring] {assetPath}에서 클립 '{clipName}'을 찾지 못했다.");
            }

            return clip;
        }

        /// <summary>
        /// 프리팹 뿌리에 다리를 붙이고, 겉모습의 애니메이터에 컨트롤러를 걸고 루트 모션을 끄며, 데모 스크립트를 끈다.
        /// 바뀐 것이 있을 때만 저장한다.
        /// </summary>
        private static void WirePrefab(string prefabPath, AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError($"[UnitAnimationWiring] {prefabPath}가 없어 손대지 않는다.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<PlanarCharacterMover>() == null)
                {
                    Debug.LogError($"[UnitAnimationWiring] {prefabPath} 뿌리에 PlanarCharacterMover가 없어 다리를 붙이지 않는다. 다리는 이동기를 요구한다.");
                    return;
                }

                var changed = false;
                if (root.GetComponent<UnitLocomotionAnimator>() == null)
                {
                    root.AddComponent<UnitLocomotionAnimator>();
                    changed = true;
                }

                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    Debug.LogError($"[UnitAnimationWiring] {prefabPath}의 겉모습에 Animator가 없어 컨트롤러를 걸지 못한다.");
                }
                else
                {
                    if (animator.runtimeAnimatorController != controller)
                    {
                        animator.runtimeAnimatorController = controller;
                        changed = true;
                    }

                    if (animator.applyRootMotion)
                    {
                        animator.applyRootMotion = false;
                        changed = true;
                    }
                }

                changed |= DisableDemoController(root);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[UnitAnimationWiring] {prefabPath}에 걷기 애니메이션을 배선해 저장했다.");
                }
                else
                {
                    Debug.Log($"[UnitAnimationWiring] {prefabPath}에 바꿀 것이 없다.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 데모 스크립트가 켜져 있으면 끈다. 서드파티 어셈블리를 참조하지 않으므로 형식 이름으로 찾으며,
        /// 스크립트가 빠진 컴포넌트(null)는 건너뛴다.
        /// </summary>
        /// <returns>하나라도 껐으면 true이다.</returns>
        private static bool DisableDemoController(GameObject root)
        {
            var changed = false;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().FullName != DemoControllerTypeName || !behaviour.enabled)
                {
                    continue;
                }

                behaviour.enabled = false;
                changed = true;
            }

            return changed;
        }
    }
}
