using System.Collections.Generic;
using HS.Tactics.Battlefield;
using HS.Tactics.Lane;
using HS.Tactics.Pathfinding;
using HS.Tactics.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Editor
{
    /// <summary>
    /// Level1의 수동 도로 배치를 BattlefieldGenerator에 연결하는 편집기 메뉴이다.
    /// 도로 프리팹 인스턴스를 지우고 이를 가리키는 레인 시종점을 비운 뒤 생성기에 스테이지 데이터·타일·레인을 지정한다.
    /// BattlePathfindingService와 UnitSpatialRegistry가 없으면 추가하고, 필수 에셋과 레인이 준비되면 씬을 저장한다.
    /// </summary>
    public static class BattlefieldSceneWiring
    {
        private const string ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string StageDataPath = "Assets/_HS/Tactics/Definitions/Stages/Stage1.asset";
        private const string BodyTilePrefabGuid = "4fa7824a621d72c419d77f7c478045eb";
        private const string CapPrefabGuid = "210adf71097388940bcd0ef3997f3c6e";
        private const string BattlefieldObjectName = "Battlefield";
        private const string PathfindingServiceObjectName = "BattlePathfindingService";
        private const string SpatialRegistryObjectName = "UnitSpatialRegistry";

        /// <summary>Level1.unity를 열어 손 배치 바닥을 생성기로 바꾸고 저장한다.</summary>
        [MenuItem("Tools/HS Tactics/Battlefield/Wire BattlefieldGenerator Into Level1")]
        public static void WireLevel1()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var stageData = AssetDatabase.LoadAssetAtPath<StageData>(StageDataPath);
            var bodyTilePrefab = LoadPrefab(BodyTilePrefabGuid);
            var capPrefab = LoadPrefab(CapPrefabGuid);
            var lane = FindInScene<BattleLane>(scene);
            if (stageData == null || bodyTilePrefab == null || capPrefab == null || lane == null)
            {
                Debug.LogError(
                    "[BattlefieldSceneWiring] 스테이지 데이터·타일 프리팹·레인 가운데 찾지 못한 것이 있어 배선을 멈춘다. " +
                    $"stageData={stageData != null}, body={bodyTilePrefab != null}, cap={capPrefab != null}, lane={lane != null}");
                return;
            }

            var removedRoads = RemoveHandPlacedRoads(scene, lane);
            var generator = EnsureGenerator(scene, stageData, bodyTilePrefab, capPrefab, lane);
            var addedPathfinding = EnsureRootComponent<BattlePathfindingService>(scene, PathfindingServiceObjectName);
            var addedRegistry = EnsureRootComponent<UnitSpatialRegistry>(scene, SpatialRegistryObjectName);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[BattlefieldSceneWiring] {ScenePath} 배선 완료 — 지운 도로 {removedRoads}개, " +
                $"생성기 {(generator.Item2 ? "새로 만듦" : "이미 있음")}, " +
                $"경로 계획 서비스 {(addedPathfinding ? "추가" : "이미 있음")}, 공간 레지스트리 {(addedRegistry ? "추가" : "이미 있음")}.",
                generator.Item1);
        }

        /// <summary>손으로 놓은 도로 프리팹 인스턴스를 전부 지우고, 그것을 가리키던 레인의 시종점을 비운다.</summary>
        /// <returns>지운 인스턴스 수이다.</returns>
        private static int RemoveHandPlacedRoads(Scene scene, BattleLane lane)
        {
            var roads = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (IsRoadPrefabInstance(child.gameObject))
                    {
                        roads.Add(child.gameObject);
                    }
                }
            }

            if (roads.Count == 0)
            {
                return 0;
            }

            var serializedLane = new SerializedObject(lane);
            serializedLane.FindProperty("startPoint").objectReferenceValue = null;
            serializedLane.FindProperty("endPoint").objectReferenceValue = null;
            serializedLane.ApplyModifiedPropertiesWithoutUndo();

            foreach (var road in roads)
            {
                Object.DestroyImmediate(road);
            }

            return roads.Count;
        }

        /// <summary>도로 타일 프리팹의 가장 바깥 인스턴스 루트인지 확인한다. 프리팹 안쪽 오브젝트는 루트와 함께 지워진다.</summary>
        private static bool IsRoadPrefabInstance(GameObject candidate)
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(candidate))
            {
                return false;
            }

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return guid == BodyTilePrefabGuid || guid == CapPrefabGuid;
        }

        /// <summary>생성기가 없으면 루트 "Battlefield"에 만들어 연결한다.</summary>
        /// <returns>생성기와, 새로 만들었는지 여부이다.</returns>
        private static (BattlefieldGenerator, bool) EnsureGenerator(
            Scene scene, StageData stageData, GameObject bodyTilePrefab, GameObject capPrefab, BattleLane lane)
        {
            var existing = FindInScene<BattlefieldGenerator>(scene);
            if (existing != null)
            {
                return (existing, false);
            }

            var hostObject = new GameObject(BattlefieldObjectName);
            SceneManager.MoveGameObjectToScene(hostObject, scene);
            var generator = hostObject.AddComponent<BattlefieldGenerator>();

            var serialized = new SerializedObject(generator);
            serialized.FindProperty("stageData").objectReferenceValue = stageData;
            serialized.FindProperty("bodyTilePrefab").objectReferenceValue = bodyTilePrefab;
            serialized.FindProperty("capPrefab").objectReferenceValue = capPrefab;
            serialized.FindProperty("lane").objectReferenceValue = lane;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return (generator, true);
        }

        /// <summary>씬에 컴포넌트가 없으면 그 이름의 루트 오브젝트를 만들어 붙인다.</summary>
        /// <returns>새로 만들었으면 true이다.</returns>
        private static bool EnsureRootComponent<T>(Scene scene, string objectName) where T : Component
        {
            if (FindInScene<T>(scene) != null)
            {
                return false;
            }

            var hostObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(hostObject, scene);
            hostObject.AddComponent<T>();
            return true;
        }

        /// <summary>guid로 프리팹을 읽는다. 폴더가 옮겨져도 같은 프리팹을 가리킨다.</summary>
        private static GameObject LoadPrefab(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
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
