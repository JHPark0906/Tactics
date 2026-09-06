using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneTemplate;
using UnityEngine;

namespace HS.Framework.Editor.ProjectManagement
{
    /// <summary>Framework 기본 씬을 씬 템플릿으로 발행해 File &gt; New Scene에서 선택할 수 있게 한다.</summary>
    [InitializeOnLoad]
    public static class FrameworkSceneTemplates
    {
        /// <summary>Bootstrap 씬 템플릿의 원본 씬 경로이며 패키지가 소유한다.</summary>
        public const string BootstrapTemplateScenePath = "Packages/com.hs.framework/Scenes/Bootstrap.unity";

        /// <summary>Loading 씬 템플릿의 원본 씬 경로이며 패키지가 소유한다.</summary>
        public const string LoadingTemplateScenePath = "Packages/com.hs.framework/Scenes/Loading.unity";

        /// <summary>
        /// 생성한 씬 템플릿 에셋이 놓이는 프로젝트 폴더이다.
        /// 패키지는 읽기 전용일 수 있고 AssetDatabase는 Assets 밖에 폴더를 만들 수 없으므로
        /// 템플릿 산출물은 패키지가 아니라 프로젝트가 소유한다.
        /// </summary>
        private const string TemplateFolder = "Assets/_HS/SceneTemplates";
        private const string BootstrapTemplatePath = TemplateFolder + "/Bootstrap.scenetemplate";
        private const string LoadingTemplatePath = TemplateFolder + "/Loading.scenetemplate";

        static FrameworkSceneTemplates()
        {
            EditorApplication.delayCall += EnsureTemplatesExist;
        }

        /// <summary>씬 템플릿 에셋을 생성하거나 원본 씬 기준으로 갱신한다.</summary>
        [MenuItem("Tools/HS Framework/Create Scene Templates")]
        public static void CreateOrUpdateTemplates()
        {
            CreateOrUpdateTemplate(
                BootstrapTemplateScenePath,
                BootstrapTemplatePath,
                "HS Bootstrap",
                "Framework 초기화와 최초 씬 전환을 수행하는 Bootstrap 씬입니다. " +
                "프로젝트가 소유하는 사본을 만들어 Build Settings 인덱스 0에 두세요.");
            CreateOrUpdateTemplate(
                LoadingTemplateScenePath,
                LoadingTemplatePath,
                "HS Loading",
                "씬 전환 중 표시되는 Loading 씬입니다. " +
                "프로젝트가 소유하는 사본을 만들고 FrameworkLoadingScreen 프리팹 배리언트로 연출을 교체하세요.");
            AssetDatabase.SaveAssets();
        }

        private static void EnsureTemplatesExist()
        {
            if (File.Exists(BootstrapTemplatePath) && File.Exists(LoadingTemplatePath))
            {
                return;
            }

            CreateOrUpdateTemplates();
        }

        private static void CreateOrUpdateTemplate(
            string scenePath,
            string templatePath,
            string templateName,
            string description)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"[HS Framework Scene Templates] 템플릿 원본 씬을 찾을 수 없습니다: {scenePath}");
                return;
            }

            if (!AssetDatabase.IsValidFolder(TemplateFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_HS"))
                {
                    AssetDatabase.CreateFolder("Assets", "_HS");
                }
                AssetDatabase.CreateFolder("Assets/_HS", "SceneTemplates");
            }

            var template = AssetDatabase.LoadAssetAtPath<SceneTemplateAsset>(templatePath);
            if (template == null)
            {
                template = SceneTemplateService.CreateSceneTemplate(templatePath);
            }

            template.templateScene = sceneAsset;
            template.templateName = templateName;
            template.description = description;
            template.addToDefaults = true;
            template.dependencies = CollectReferenceDependencies(scenePath);
            EditorUtility.SetDirty(template);
        }

        private static DependencyInfo[] CollectReferenceDependencies(string scenePath)
        {
            var dependencies = new List<DependencyInfo>();
            foreach (var dependencyPath in AssetDatabase.GetDependencies(scenePath, false))
            {
                if (string.Equals(dependencyPath, scenePath, System.StringComparison.OrdinalIgnoreCase) ||
                    dependencyPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dependency = AssetDatabase.LoadAssetAtPath<Object>(dependencyPath);
                if (dependency == null)
                {
                    continue;
                }

                dependencies.Add(new DependencyInfo
                {
                    dependency = dependency,
                    instantiationMode = TemplateInstantiationMode.Reference
                });
            }

            return dependencies.ToArray();
        }
    }
}
