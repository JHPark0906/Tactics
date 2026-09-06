using HS.Tactics.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 두 유닛 프리팹이 <see cref="EnemyDetector"/>를 들고 있는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 두 행동 트리의 뿌리는 적을 찾는 서비스이고, 그 서비스는 유닛에 탐지기가 있어야 만들어진다. 탐지기가 없으면
    /// 트리가 비어 아무도 교전하지 않는데, 트리 검사들은 탐지기를 직접 붙여 세우므로 그 결핍을 보지 못한다.
    /// 그래서 프리팹 에셋 자체를 열어 확인한다 — 프리팹에서 탐지기가 빠지는 날 여기가 붉어진다.
    /// </para>
    /// <para>
    /// 프로덕션 코드 어디도 실행 중에 탐지기를 붙이지 않으므로, 프리팹에 있는 것이 유닛이 탐지기를 갖는 유일한 길이다.
    /// </para>
    /// </remarks>
    public sealed class UnitPrefabsCarryEnemyDetectorTests
    {
        private const string GruntPrefabPath = "Assets/_HS/Tactics/Prefabs/Grunt.prefab";
        private const string RiflemanPrefabPath = "Assets/_HS/Tactics/Prefabs/Rifleman.prefab";

        [TestCase(GruntPrefabPath)]
        [TestCase(RiflemanPrefabPath)]
        public void TheUnitPrefabRootCarriesAnEnemyDetector(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"유닛 프리팹이 {prefabPath}에 있어야 한다.");

            Assert.That(
                prefab.GetComponent<EnemyDetector>(),
                Is.Not.Null,
                $"{prefabPath}의 뿌리에 EnemyDetector가 없다. 두 행동 트리의 뿌리가 탐지 서비스라 탐지기 없이는 트리가 빈다.");
        }
    }
}
