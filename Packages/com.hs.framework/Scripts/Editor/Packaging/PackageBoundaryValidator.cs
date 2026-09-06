using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HS.Framework.Editor.Packaging
{
    /// <summary>
    /// 프레임워크 패키지 안의 어셈블리가 프로젝트(Assets) 코드를 참조하지 않는지 검사한다.
    /// Unity는 패키지에서 프로젝트 어셈블리로 향하는 참조를 막지 않으므로,
    /// 재사용 가능한 패키지를 지키려면 이 검증기가 경계를 대신 강제한다.
    /// </summary>
    public static class PackageBoundaryValidator
    {
        /// <summary>
        /// 프레임워크가 기본으로 허용하는 참조 이름 목록이다.
        /// R3는 NuGetForUnity로 Assets/Packages 아래에, MessagePipe는 Assets/Plugins 아래에 설치되는
        /// 외부 의존성이므로 이 기본 허용 목록에 포함한다.
        /// 프로젝트 고유의 예외는 이 목록을 고치지 말고
        /// <see cref="PackageBoundaryAllowList"/> 에셋으로 선언한다.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultAllowedExternalReferences = new[]
        {
            "R3.dll",
            // MessagePipe 는 프레임워크 자신의 의존이다 — Bootstrap·Gameplay·Persistence·Runtime·Scene asmdef 가 직접 참조하며,
            // Assets/Plugins 아래 설치라 UPM 의존성으로 표현할 수 없다. 프로젝트 고유 예외가 아니므로 허용 목록 에셋으로 옮기지 않는다.
            "MessagePipe",
            // Runtime 의 VContainer 통합도 같은 Assets/Plugins 외부 라이브러리 설치에 포함된다.
            "MessagePipe.VContainer"
        };

        private const string PackageManifestFileName = "package.json";

        /// <summary>
        /// 패키지 경계를 검사하고 발견한 위반 메시지를 반환한다.
        /// 패키지 루트를 찾지 못하면 검사를 건너뛰고 빈 목록을 반환한다.
        /// </summary>
        /// <returns>위반 메시지 목록이며 위반이 없으면 빈 목록이다.</returns>
        public static IReadOnlyList<string> CollectViolationMessages()
        {
            var messages = new List<string>();
            var packageRoot = ResolvePackageRoot();
            if (string.IsNullOrEmpty(packageRoot))
            {
                Debug.LogWarning(
                    "[HS Framework 패키지 경계] 패키지 루트를 찾을 수 없어 경계 검사를 건너뛴다.");
                return messages;
            }

            var violations = PackageBoundaryRules.FindViolations(
                CollectAssemblyDefinitions(),
                packageRoot,
                CollectAllowedExternalReferences());
            foreach (var violation in violations)
            {
                messages.Add(violation.BuildMessage());
            }

            return messages;
        }

        /// <summary>
        /// 기본 허용 목록과 프로젝트가 선언한 <see cref="PackageBoundaryAllowList"/> 에셋을 모두 합쳐
        /// 이번 검사에 적용할 허용 목록을 만든다.
        /// 에셋을 형식으로 찾으므로 프로젝트 안의 위치를 가정하지 않으며,
        /// 에셋이 하나도 없으면 기본 목록만으로 정상 동작한다.
        /// </summary>
        /// <returns>이번 검사에 적용할 허용 목록이다.</returns>
        public static IReadOnlyList<string> CollectAllowedExternalReferences()
        {
            var projectAllowedReferences = new List<IEnumerable<string>>();
            foreach (var assetGuid in AssetDatabase.FindAssets($"t:{nameof(PackageBoundaryAllowList)}"))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var allowList = AssetDatabase.LoadAssetAtPath<PackageBoundaryAllowList>(assetPath);
                if (allowList != null)
                {
                    projectAllowedReferences.Add(allowList.AllowedReferences);
                }
            }

            var sources = new List<IEnumerable<string>> { DefaultAllowedExternalReferences };
            sources.AddRange(projectAllowedReferences);
            return PackageBoundaryRules.CombineAllowedReferences(sources.ToArray());
        }

        /// <summary>패키지 경계를 검사하고 결과를 콘솔에 보고한다.</summary>
        [MenuItem("Tools/HS Framework/Validate Package Boundary")]
        public static void ValidatePackageBoundary()
        {
            var messages = CollectViolationMessages();
            if (messages.Count == 0)
            {
                Debug.Log("[HS Framework 패키지 경계] 위반이 없다. 패키지는 프로젝트 코드에 의존하지 않는다.");
                return;
            }

            foreach (var message in messages)
            {
                Debug.LogError(message);
            }
        }

        /// <summary>
        /// 이 검증기가 속한 패키지의 루트 에셋 경로를 찾는다.
        /// 프로젝트 고유 경로를 가정하지 않으므로 임베디드 패키지와 읽기 전용 설치본에서 모두 동작한다.
        /// </summary>
        /// <returns>패키지 루트의 에셋 경로이며 찾지 못하면 null이다.</returns>
        public static string ResolvePackageRoot()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(PackageBoundaryValidator).Assembly);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.assetPath))
            {
                return packageInfo.assetPath;
            }

            return FindPackageRootFromScriptPath();
        }

        /// <summary>
        /// 패키지 정보를 얻을 수 없을 때 이 스크립트의 위치에서 위로 올라가며 패키지 매니페스트를 찾는다.
        /// 패키지를 Assets 아래에 복사해 사용하는 경우까지 대비한 대체 경로이다.
        /// </summary>
        /// <returns>패키지 루트의 에셋 경로이며 찾지 못하면 null이다.</returns>
        private static string FindPackageRootFromScriptPath()
        {
            var scriptGuids = AssetDatabase.FindAssets($"t:MonoScript {nameof(PackageBoundaryValidator)}");
            foreach (var scriptGuid in scriptGuids)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                if (string.IsNullOrEmpty(scriptPath))
                {
                    continue;
                }

                var folder = GetParentFolder(scriptPath);
                while (!string.IsNullOrEmpty(folder))
                {
                    if (AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/{PackageManifestFileName}") != null)
                    {
                        return folder;
                    }

                    folder = GetParentFolder(folder);
                }
            }

            return null;
        }

        /// <summary>에셋 경로의 상위 폴더 경로를 반환하며 더 올라갈 수 없으면 null이다.</summary>
        private static string GetParentFolder(string assetPath)
        {
            var separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex > 0 ? assetPath.Substring(0, separatorIndex) : null;
        }

        /// <summary>프로젝트의 모든 어셈블리 정의를 참조 대상 경로까지 해석해 수집한다.</summary>
        /// <returns>수집한 어셈블리 정의 목록이다.</returns>
        public static IReadOnlyList<AssemblyDefinitionInfo> CollectAssemblyDefinitions()
        {
            var assemblies = new List<AssemblyDefinitionInfo>();
            foreach (var assetGuid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
                if (asset == null)
                {
                    continue;
                }

                var definition = ParseDefinition(asset.text);
                if (definition == null)
                {
                    continue;
                }

                var assembly = new AssemblyDefinitionInfo(
                    string.IsNullOrEmpty(definition.name) ? assetPath : definition.name,
                    assetPath);
                AddAssemblyReferences(assembly, definition);
                AddPrecompiledReferences(assembly, definition);
                assemblies.Add(assembly);
            }

            return assemblies;
        }

        /// <summary>asmdef 참조를 이름 또는 GUID 형식에서 에셋 경로로 해석해 추가한다.</summary>
        private static void AddAssemblyReferences(
            AssemblyDefinitionInfo assembly,
            AssemblyDefinitionJson definition)
        {
            if (definition.references == null)
            {
                return;
            }

            foreach (var reference in definition.references)
            {
                if (string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                var referencePath = ResolveAssemblyDefinitionPath(reference, out var referenceName);
                assembly.AddReference(new AssemblyReferenceInfo(referenceName, referencePath));
            }
        }

        /// <summary>미리 컴파일된 DLL 참조를 실제 파일 경로로 해석해 추가한다.</summary>
        private static void AddPrecompiledReferences(
            AssemblyDefinitionInfo assembly,
            AssemblyDefinitionJson definition)
        {
            if (definition.precompiledReferences == null)
            {
                return;
            }

            foreach (var reference in definition.precompiledReferences)
            {
                if (string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                var referencePath = CompilationPipeline.GetPrecompiledAssemblyPathFromAssemblyName(reference);
                assembly.AddReference(new AssemblyReferenceInfo(reference, referencePath, true));
            }
        }

        /// <summary>
        /// asmdef 참조 항목을 에셋 경로로 해석한다.
        /// 참조는 어셈블리 이름이거나 GUID 형식이며 두 경우를 모두 처리한다.
        /// </summary>
        /// <param name="reference">asmdef에 기록된 참조 항목이다.</param>
        /// <param name="referenceName">진단에 사용할 참조 대상 이름을 반환한다.</param>
        /// <returns>참조 대상 asmdef의 에셋 경로이며 해석하지 못하면 null이다.</returns>
        private static string ResolveAssemblyDefinitionPath(string reference, out string referenceName)
        {
            const string guidPrefix = "GUID:";
            if (reference.StartsWith(guidPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(reference.Substring(guidPrefix.Length));
                referenceName = ResolveAssemblyName(assetPath) ?? reference;
                return assetPath;
            }

            referenceName = reference;
            return CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(reference);
        }

        /// <summary>asmdef 에셋 경로에서 어셈블리 이름을 읽는다.</summary>
        private static string ResolveAssemblyName(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(assetPath);
            var definition = asset == null ? null : ParseDefinition(asset.text);
            return string.IsNullOrEmpty(definition?.name) ? null : definition.name;
        }

        /// <summary>asmdef 본문을 파싱하며 형식이 잘못되었으면 null을 반환한다.</summary>
        private static AssemblyDefinitionJson ParseDefinition(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<AssemblyDefinitionJson>(text);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>asmdef 본문에서 경계 검사에 필요한 항목만 담는 직렬화 형식이다.</summary>
        [Serializable]
        private sealed class AssemblyDefinitionJson
        {
            public string name;
            public string[] references;
            public string[] precompiledReferences;
        }
    }

    /// <summary>빌드 직전에 패키지 경계를 검사해 위반이 있으면 빌드를 중단한다.</summary>
    public sealed class PackageBoundaryBuildValidator : IPreprocessBuildWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => -1000;

        /// <inheritdoc />
        public void OnPreprocessBuild(BuildReport report)
        {
            var messages = PackageBoundaryValidator.CollectViolationMessages();
            if (messages.Count > 0)
            {
                throw new BuildFailedException(string.Join("\n", messages));
            }
        }
    }
}
