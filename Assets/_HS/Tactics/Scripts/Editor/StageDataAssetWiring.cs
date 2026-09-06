using HS.Tactics.Battlefield;
using HS.Tactics.Units;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Editor
{
    /// <summary>
    /// 표본 스테이지 데이터 에셋 Stage1.asset을 만드는 편집기 메뉴이다.
    /// 대상 경로에 어떤 형식의 에셋이든 있으면 덮어쓰지 않는다.
    /// 반복 실행으로 사람이 편집한 값이 사라지지 않도록 생성이 필요한 경우에만 저장한다.
    /// </summary>
    public static class StageDataAssetWiring
    {
        private const string StagesFolderParent = "Assets/_HS/Tactics/Definitions";
        private const string StagesFolderName = "Stages";
        private const string StageAssetPath = StagesFolderParent + "/" + StagesFolderName + "/Stage1.asset";
        private const string GruntAssetPath = "Assets/_HS/Tactics/Definitions/Grunt.asset";

        /// <summary>
        /// 표본 전장의 길이(미터)이다.
        /// </summary>
        private const float SampleBattlefieldLength = 36f;

        /// <summary>Stage1.asset을 만들고 저장한다. 이미 있으면 그대로 둔다.</summary>
        [MenuItem("Tools/HS Tactics/Battlefield/Create Sample StageData")]
        public static void CreateSampleStage1()
        {
            // 형식으로 거르지 않는다. 다른 형식의 에셋이 그 자리에 있어도 덮어쓰지 않고 그대로 두어야 한다.
            var existing = AssetDatabase.LoadMainAssetAtPath(StageAssetPath);
            if (existing != null)
            {
                Debug.Log(
                    $"[StageDataAssetWiring] 이미 있어 다시 만들지 않는다: {StageAssetPath} ({existing.GetType().Name}).",
                    existing);
                return;
            }

            var grunt = AssetDatabase.LoadAssetAtPath<UnitDefinition>(GruntAssetPath);
            if (grunt == null)
            {
                Debug.LogError($"[StageDataAssetWiring] {GruntAssetPath}를 읽지 못해 표본을 만들지 않는다.");
                return;
            }

            var stagesFolder = StagesFolderParent + "/" + StagesFolderName;
            if (!AssetDatabase.IsValidFolder(stagesFolder))
            {
                AssetDatabase.CreateFolder(StagesFolderParent, StagesFolderName);
            }

            var stage = StageData.CreateRuntime(
                SampleBattlefieldLength,
                new UnitPlacement(grunt, 1, new Vector3(24f, 0f, 3f)),
                new UnitPlacement(grunt, 1, new Vector3(24f, 0f, -3f)));

            AssetDatabase.CreateAsset(stage, StageAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[StageDataAssetWiring] {StageAssetPath}를 만들었다. 길이 {SampleBattlefieldLength}, 배치 {stage.UnitPlacements.Count}기.",
                stage);
        }
    }
}
