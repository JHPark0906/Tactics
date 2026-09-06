using HS.Tactics.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Editor
{
    /// <summary>Level1.unity에 <see cref="PartySelectionReplay"/>를 배선하는 메뉴이다.</summary>
    /// <remarks>
    /// <para>
    /// MainMenu의 영웅 배치 패널이 확정한 선택을 게임플레이 씬 진입 시 재생하려면 이 컴포넌트 하나만
    /// 있으면 된다 — 실제 스폰은 이미 있는 <see cref="UnitPlacementController"/>가 그대로 한다. 이 메뉴는
    /// 씬에 새 GameObject 하나를 만들어 컴포넌트를 붙이고, 같은 씬의 컨트롤러를 찾아 연결한다.
    /// </para>
    /// <para>
    /// <b>이미 배선되어 있으면 다시 만들지 않는다.</b> 몇 번을 돌려도 씬에 중복 오브젝트가 생기지 않으므로
    /// 배치모드에서 안전하게 재실행할 수 있다.
    /// </para>
    /// </remarks>
    public static class PartySelectionReplaySceneWiring
    {
        private const string ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string ObjectName = "PartySelectionReplay";

        /// <summary>Level1.unity를 열어 배선하고 저장한다. 이미 배선되어 있으면 그대로 둔다.</summary>
        [MenuItem("Tools/HS Tactics/Placement/Wire PartySelectionReplay Into Level1")]
        public static void WireLevel1()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = FindInScene<PartySelectionReplay>(scene);
            if (existing != null)
            {
                Debug.Log(
                    $"[PartySelectionReplaySceneWiring] 이미 배선되어 있어 다시 만들지 않는다: {existing.gameObject.name}.",
                    existing);
                return;
            }

            var controller = FindInScene<UnitPlacementController>(scene);
            if (controller == null)
            {
                Debug.LogError(
                    $"[PartySelectionReplaySceneWiring] {ScenePath}에서 UnitPlacementController를 찾지 못해 배선을 멈춘다.");
                return;
            }

            var hostObject = new GameObject(ObjectName);
            SceneManager.MoveGameObjectToScene(hostObject, scene);
            var replay = hostObject.AddComponent<PartySelectionReplay>();

            var serializedReplay = new SerializedObject(replay);
            serializedReplay.FindProperty("placementController").objectReferenceValue = controller;
            serializedReplay.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[PartySelectionReplaySceneWiring] {ScenePath}에 PartySelectionReplay를 배선하고 {controller.name}에 연결했다.",
                replay);
        }

        /// <summary>씬의 뿌리 오브젝트들을 훑어 컴포넌트를 찾는다. 비활성 오브젝트도 포함한다.</summary>
        /// <param name="scene">찾을 씬이다.</param>
        /// <typeparam name="T">찾을 컴포넌트 형식이다.</typeparam>
        /// <returns>찾은 컴포넌트이며 없으면 null이다.</returns>
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
