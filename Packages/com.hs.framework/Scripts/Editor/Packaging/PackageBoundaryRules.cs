using System;
using System.Collections.Generic;

namespace HS.Framework.Editor.Packaging
{
    /// <summary>어셈블리 정의가 참조하는 대상 하나의 이름과 위치이다.</summary>
    public readonly struct AssemblyReferenceInfo
    {
        /// <summary>참조 대상의 표시 이름이며 어셈블리 이름이나 DLL 파일 이름이다.</summary>
        public string Name { get; }

        /// <summary>
        /// 참조 대상이 놓인 에셋 경로이다.
        /// 엔진 내장 어셈블리처럼 경로를 확인할 수 없으면 null이거나 빈 문자열이다.
        /// </summary>
        public string AssetPath { get; }

        /// <summary>asmdef 참조가 아니라 미리 컴파일된 DLL 참조인지 여부이다.</summary>
        public bool IsPrecompiled { get; }

        /// <summary>참조 대상 정보를 생성한다.</summary>
        /// <param name="name">참조 대상의 표시 이름이다.</param>
        /// <param name="assetPath">참조 대상의 에셋 경로이며 확인할 수 없으면 null이다.</param>
        /// <param name="isPrecompiled">미리 컴파일된 DLL 참조이면 true이다.</param>
        public AssemblyReferenceInfo(string name, string assetPath, bool isPrecompiled = false)
        {
            Name = name;
            AssetPath = assetPath;
            IsPrecompiled = isPrecompiled;
        }
    }

    /// <summary>검사 대상 어셈블리 정의 하나의 이름, 위치와 참조 목록이다.</summary>
    public sealed class AssemblyDefinitionInfo
    {
        private readonly List<AssemblyReferenceInfo> _references = new();

        /// <summary>어셈블리 정의 정보를 생성한다.</summary>
        /// <param name="name">어셈블리 이름이다.</param>
        /// <param name="assetPath">asmdef 파일의 에셋 경로이다.</param>
        /// <param name="references">이 어셈블리가 참조하는 대상 목록이다.</param>
        public AssemblyDefinitionInfo(
            string name,
            string assetPath,
            IEnumerable<AssemblyReferenceInfo> references = null)
        {
            Name = name;
            AssetPath = assetPath;
            if (references != null)
            {
                _references.AddRange(references);
            }
        }

        /// <summary>어셈블리 이름이다.</summary>
        public string Name { get; }

        /// <summary>asmdef 파일의 에셋 경로이다.</summary>
        public string AssetPath { get; }

        /// <summary>이 어셈블리가 참조하는 대상 목록이다.</summary>
        public IReadOnlyList<AssemblyReferenceInfo> References => _references;

        /// <summary>참조 대상을 추가한다.</summary>
        /// <param name="reference">추가할 참조 대상이다.</param>
        public void AddReference(AssemblyReferenceInfo reference)
        {
            _references.Add(reference);
        }
    }

    /// <summary>패키지 경계를 넘는 참조 하나에 대한 위반 정보이다.</summary>
    public sealed class PackageBoundaryViolation
    {
        /// <summary>경계 위반 정보를 생성한다.</summary>
        /// <param name="assembly">위반한 어셈블리 정의이다.</param>
        /// <param name="reference">패키지 밖을 가리키는 참조 대상이다.</param>
        public PackageBoundaryViolation(AssemblyDefinitionInfo assembly, AssemblyReferenceInfo reference)
        {
            Assembly = assembly;
            Reference = reference;
        }

        /// <summary>위반한 어셈블리 정의이다.</summary>
        public AssemblyDefinitionInfo Assembly { get; }

        /// <summary>패키지 밖을 가리키는 참조 대상이다.</summary>
        public AssemblyReferenceInfo Reference { get; }

        /// <summary>
        /// 위반 내용과 금지 이유, 해결 방향을 담은 진단 메시지를 만든다.
        /// 이 검증기에 처음 걸리는 사람이 배경을 몰라도 판단할 수 있도록 모든 맥락을 포함한다.
        /// </summary>
        /// <returns>사람이 읽는 진단 메시지이다.</returns>
        public string BuildMessage()
        {
            var referenceKind = Reference.IsPrecompiled ? "미리 컴파일된 DLL" : "어셈블리";
            return $"[HS Framework 패키지 경계] {Assembly.Name}({Assembly.AssetPath})이(가) " +
                   $"프로젝트 소유 {referenceKind} {Reference.Name}({Reference.AssetPath})을(를) 참조한다. " +
                   "프레임워크 패키지는 프로젝트(Assets) 코드에 의존해서는 안 된다. " +
                   "이 참조는 패키지를 다른 프로젝트로 옮길 때 따라가지 못해 컴파일이 깨지기 때문이다. " +
                   "참조를 제거하거나, 필요한 계약을 패키지 안에 인터페이스로 두고 프로젝트가 구현하도록 의존 방향을 뒤집어라.";
        }
    }

    /// <summary>
    /// 어셈블리 정의 목록에서 패키지 경계를 넘는 참조를 찾아내는 순수 규칙이다.
    /// 에셋 데이터베이스에 접근하지 않으므로 스캔과 분리해 그대로 테스트할 수 있다.
    /// </summary>
    public static class PackageBoundaryRules
    {
        /// <summary>프로젝트 소유 에셋 폴더의 경로 접두사이다.</summary>
        public const string ProjectAssetRoot = "Assets";

        /// <summary>
        /// 패키지 안 어셈블리가 프로젝트(Assets) 소유 대상을 참조하는 위반을 모두 찾는다.
        /// 같은 패키지 내부 참조와 다른 패키지 참조는 정상이므로 위반이 아니며,
        /// 경로를 확인할 수 없는 엔진 제공 어셈블리도 위반으로 보지 않는다.
        /// </summary>
        /// <param name="assemblies">검사할 어셈블리 정의 목록이다.</param>
        /// <param name="packageRootPath">패키지 루트의 에셋 경로이다. 예를 들면 Packages/com.hs.framework이다.</param>
        /// <param name="allowedExternalReferences">
        /// 패키지 밖에 있어도 허용하는 참조 이름 목록이며, 의도적으로 승인된 외부 의존성에 사용한다.
        /// </param>
        /// <returns>발견한 위반 목록이며 없으면 빈 목록이다.</returns>
        public static IReadOnlyList<PackageBoundaryViolation> FindViolations(
            IEnumerable<AssemblyDefinitionInfo> assemblies,
            string packageRootPath,
            IEnumerable<string> allowedExternalReferences = null)
        {
            var violations = new List<PackageBoundaryViolation>();
            if (assemblies == null || string.IsNullOrWhiteSpace(packageRootPath))
            {
                return violations;
            }

            var allowed = CreateAllowedSet(allowedExternalReferences);
            var packageRoot = NormalizePath(packageRootPath);
            foreach (var assembly in assemblies)
            {
                if (assembly == null || !IsInsidePackage(assembly.AssetPath, packageRoot))
                {
                    continue;
                }

                foreach (var reference in assembly.References)
                {
                    if (IsViolation(reference, packageRoot, allowed))
                    {
                        violations.Add(new PackageBoundaryViolation(assembly, reference));
                    }
                }
            }

            return violations;
        }

        /// <summary>지정한 에셋 경로가 패키지 루트 아래에 있는지 확인한다.</summary>
        /// <param name="assetPath">확인할 에셋 경로이다.</param>
        /// <param name="packageRootPath">패키지 루트의 에셋 경로이다.</param>
        /// <returns>패키지 루트 자신이거나 그 하위 경로이면 true이다.</returns>
        public static bool IsInsidePackage(string assetPath, string packageRootPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(packageRootPath))
            {
                return false;
            }

            var path = NormalizePath(assetPath);
            var root = NormalizePath(packageRootPath);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>지정한 에셋 경로가 프로젝트 소유 Assets 폴더 아래에 있는지 확인한다.</summary>
        /// <param name="assetPath">확인할 에셋 경로이다.</param>
        /// <returns>Assets 폴더 아래의 경로이면 true이다.</returns>
        public static bool IsProjectAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var path = NormalizePath(assetPath);
            return path.StartsWith(ProjectAssetRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>참조 하나가 경계 위반인지 판정한다.</summary>
        private static bool IsViolation(
            AssemblyReferenceInfo reference,
            string packageRoot,
            ICollection<string> allowed)
        {
            if (!string.IsNullOrWhiteSpace(reference.Name) && allowed.Contains(reference.Name))
            {
                return false;
            }

            return !IsInsidePackage(reference.AssetPath, packageRoot) && IsProjectAsset(reference.AssetPath);
        }

        /// <summary>
        /// 여러 출처의 허용 목록을 하나로 합친다.
        /// 앞뒤 공백과 대소문자 차이를 무시하고 중복을 제거하며, 등록 순서를 유지한다.
        /// </summary>
        /// <param name="sources">합칠 허용 목록들이며 null 항목은 무시한다.</param>
        /// <returns>정규화된 허용 목록이다.</returns>
        public static IReadOnlyList<string> CombineAllowedReferences(
            params IEnumerable<string>[] sources)
        {
            var combined = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sources == null)
            {
                return combined;
            }

            foreach (var source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                foreach (var name in source)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var trimmedName = name.Trim();
                    if (seen.Add(trimmedName))
                    {
                        combined.Add(trimmedName);
                    }
                }
            }

            return combined;
        }

        /// <summary>허용 목록을 대소문자 구분 없는 집합으로 만든다.</summary>
        private static ICollection<string> CreateAllowedSet(IEnumerable<string> allowedExternalReferences)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (allowedExternalReferences == null)
            {
                return allowed;
            }

            foreach (var name in allowedExternalReferences)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    allowed.Add(name.Trim());
                }
            }

            return allowed;
        }

        /// <summary>경로 구분자를 에셋 경로 형식으로 통일하고 끝의 구분자를 제거한다.</summary>
        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
