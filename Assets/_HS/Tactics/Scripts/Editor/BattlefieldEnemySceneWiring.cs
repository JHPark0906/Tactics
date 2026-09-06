using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Combat;
using HS.Tactics.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HS.Tactics.Editor
{
    /// <summary>
    /// Grunt·Rifleman 프리팹에 EnemyDetector를 갖추고 Level1의 중복 배치를 정리하는 편집기 메뉴이다.
    /// 진영 2인 수동 Rifleman 프리팹 인스턴스와 지정된 중복 BattleOutcomeService를 제거한다.
    /// 프리팹과 씬은 각각 변경이 있을 때만 저장한다.
    /// </summary>
    public static class BattlefieldEnemySceneWiring
    {
        private const string ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const string GruntPrefabPath = "Assets/_HS/Tactics/Prefabs/Grunt.prefab";
        private const string RiflemanPrefabPath = "Assets/_HS/Tactics/Prefabs/Rifleman.prefab";

        /// <summary>정리 대상 수동 적 배치의 진영이다. 이 값이 아니면 다른 목적으로 놓인 것으로 보고 지우지 않는다.</summary>
        private const int HandPlacedEnemyTeamValue = 2;

        /// <summary>둘 중 지울 승패 집계 오브젝트의 이름이다. 컴포넌트 이름과 같은 쪽을 남긴다.</summary>
        private const string RedundantOutcomeObjectName = "BattleOutcome";

        /// <summary>두 프리팹에 탐지기를 붙이고 Level1.unity를 손본 뒤 저장한다. 이미 되어 있는 것은 그대로 둔다.</summary>
        [MenuItem("Tools/HS Tactics/Battlefield/Wire Enemy Setup Into Level1")]
        public static void WireLevel1()
        {
            EnsureEnemyDetectorOnPrefab(GruntPrefabPath);
            EnsureEnemyDetectorOnPrefab(RiflemanPrefabPath);

            // 에디터에서 손으로 실행했을 때 열려 있던 씬의 미저장 변경을 묻지 않고 버리지 않도록 먼저 묻는다.
            // 배치모드에서는 물을 사람이 없어 그대로 지나간다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[BattlefieldEnemySceneWiring] 열려 있던 씬의 저장을 취소해 Level1.unity는 손대지 않는다.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var changed = RemoveHandPlacedRiflemen(scene);
            changed |= RemoveRedundantBattleOutcomeService(scene);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[BattlefieldEnemySceneWiring] {ScenePath}를 저장했다.");
            }
            else
            {
                Debug.Log($"[BattlefieldEnemySceneWiring] {ScenePath}에 바꿀 것이 없어 저장하지 않는다.");
            }
        }

        /// <summary>프리팹 뿌리에 <see cref="EnemyDetector"/>가 없으면 붙여 저장한다. 요구 컴포넌트인 진영 구성요소는 프리팹에 이미 있다.</summary>
        /// <param name="prefabPath">손볼 프리팹의 경로이다.</param>
        private static void EnsureEnemyDetectorOnPrefab(string prefabPath)
        {
            // 없는 경로를 LoadPrefabContents에 넘기면 예외가 나 뒤의 씬 단계까지 멈추므로, 있는지 먼저 본다.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError($"[BattlefieldEnemySceneWiring] {prefabPath}가 없어 탐지기를 붙이지 않는다.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<EnemyDetector>() != null)
                {
                    Debug.Log($"[BattlefieldEnemySceneWiring] {prefabPath}에는 EnemyDetector가 이미 있어 붙이지 않는다.");
                    return;
                }

                if (root.GetComponent<TeamMember>() == null)
                {
                    Debug.LogError(
                        $"[BattlefieldEnemySceneWiring] {prefabPath} 뿌리에 TeamMember가 없어 EnemyDetector를 붙이지 않는다. " +
                        "탐지기는 진영 구성요소를 요구한다.");
                    return;
                }

                root.AddComponent<EnemyDetector>();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[BattlefieldEnemySceneWiring] {prefabPath}에 EnemyDetector를 붙여 저장했다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Rifleman 프리팹의 뿌리 인스턴스 가운데 진영이 <see cref="HandPlacedEnemyTeamValue"/>인 것을 씬에서 지운다.
        /// </summary>
        /// <param name="scene">손볼 씬이다.</param>
        /// <returns>하나라도 지웠으면 true이다.</returns>
        private static bool RemoveHandPlacedRiflemen(Scene scene)
        {
            var removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(root))
                {
                    continue;
                }

                var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (sourcePath != RiflemanPrefabPath)
                {
                    continue;
                }

                if (!root.TryGetComponent<TeamMember>(out var team) || team.TeamId.Value != HandPlacedEnemyTeamValue)
                {
                    continue;
                }

                Debug.Log(
                    $"[BattlefieldEnemySceneWiring] 손으로 놓인 Rifleman '{root.name}'(진영 {team.TeamId.Value}, {root.transform.position})을 지운다.");
                Object.DestroyImmediate(root);
                removed++;
            }

            if (removed == 0)
            {
                Debug.Log("[BattlefieldEnemySceneWiring] 지울 손배치 Rifleman이 없다.");
            }

            return removed > 0;
        }

        /// <summary>
        /// <see cref="BattleOutcomeService"/>가 둘 이상일 때만, <see cref="RedundantOutcomeObjectName"/>이라는 이름의 뿌리에
        /// 붙은 것을 지운다. 하나뿐이면 이름이 무엇이든 건드리지 않는다.
        /// </summary>
        /// <param name="scene">손볼 씬이다.</param>
        /// <returns>지웠으면 true이다.</returns>
        private static bool RemoveRedundantBattleOutcomeService(Scene scene)
        {
            var services = new List<BattleOutcomeService>();
            foreach (var root in scene.GetRootGameObjects())
            {
                services.AddRange(root.GetComponentsInChildren<BattleOutcomeService>(true));
            }

            if (services.Count < 2)
            {
                Debug.Log($"[BattlefieldEnemySceneWiring] BattleOutcomeService가 {services.Count}개라 지우지 않는다.");
                return false;
            }

            BattleOutcomeService redundant = null;
            foreach (var service in services)
            {
                var owner = service.gameObject;
                if (owner.transform.parent == null && owner.name == RedundantOutcomeObjectName)
                {
                    redundant = service;
                    break;
                }
            }

            if (redundant == null)
            {
                Debug.LogWarning(
                    $"[BattlefieldEnemySceneWiring] BattleOutcomeService가 {services.Count}개인데 '{RedundantOutcomeObjectName}'이라는 " +
                    "이름의 뿌리가 없어 어느 것을 지울지 정하지 못한다. 손으로 정해야 한다.");
                return false;
            }

            Debug.Log(
                $"[BattlefieldEnemySceneWiring] 중복된 BattleOutcomeService '{redundant.gameObject.name}'을 지운다. " +
                $"남는 것은 {services.Count - 1}개다.");
            Object.DestroyImmediate(redundant.gameObject);
            return true;
        }
    }
}
