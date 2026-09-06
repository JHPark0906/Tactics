using System.Linq;
using HS.Framework.Editor.Packaging;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>패키지 경계 규칙이 프로젝트 방향 참조만 위반으로 판정하는지 검증한다.</summary>
    public sealed class PackageBoundaryRulesTests
    {
        private const string PackageRoot = "Packages/com.hs.framework";

        [Test]
        public void ReferenceInsideThePackageIsNotAViolation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Runtime",
                "Scripts/Runtime",
                new AssemblyReferenceInfo(
                    "HS.Framework.Foundation",
                    $"{PackageRoot}/Scripts/Foundation/HS.Framework.Foundation.asmdef"));

            var violations = PackageBoundaryRules.FindViolations(new[] { assembly }, PackageRoot);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ReferenceToAnotherPackageIsNotAViolation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Scene",
                "Scripts/Scene",
                new AssemblyReferenceInfo("UniTask", "Packages/com.cysharp.unitask/UniTask.asmdef"));

            var violations = PackageBoundaryRules.FindViolations(new[] { assembly }, PackageRoot);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ReferenceWithoutResolvedPathIsNotAViolation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Foundation",
                "Scripts/Foundation",
                new AssemblyReferenceInfo("UnityEngine.CoreModule", null));

            var violations = PackageBoundaryRules.FindViolations(new[] { assembly }, PackageRoot);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ReferenceToProjectAssemblyIsAViolation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Gameplay",
                "Scripts/Gameplay",
                new AssemblyReferenceInfo("Game.Content", "Assets/Game/Game.Content.asmdef"));

            var violations = PackageBoundaryRules.FindViolations(new[] { assembly }, PackageRoot);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Assembly.Name, Is.EqualTo("HS.Framework.Gameplay"));
            Assert.That(violations[0].Reference.Name, Is.EqualTo("Game.Content"));
        }

        [TestCase("Game.Content")]
        [TestCase("MessagePipe.VContainer.Game")]
        public void DefaultAllowListDoesNotAllowProjectAssembliesOrSimilarNames(string referenceName)
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Runtime",
                "Scripts/Runtime",
                new AssemblyReferenceInfo(referenceName, $"Assets/Game/{referenceName}.asmdef"));

            var violations = PackageBoundaryRules.FindViolations(
                new[] { assembly },
                PackageRoot,
                PackageBoundaryValidator.DefaultAllowedExternalReferences);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Reference.Name, Is.EqualTo(referenceName));
        }

        [Test]
        public void AllInstalledFrameworkAssembliesRespectTheDefaultPackageBoundary()
        {
            var packageRoot = PackageBoundaryValidator.ResolvePackageRoot();
            Assert.That(packageRoot, Is.Not.Null.And.Not.Empty);

            var assemblies = PackageBoundaryValidator.CollectAssemblyDefinitions();
            var runtimeAssembly = assemblies.SingleOrDefault(assembly =>
                assembly.Name == "HS.Framework.Runtime" &&
                PackageBoundaryRules.IsInsidePackage(assembly.AssetPath, packageRoot));
            Assert.That(runtimeAssembly, Is.Not.Null, "실제 패키지 asmdef가 검사에 포함되어야 한다.");

            var integrationReference = runtimeAssembly.References.Single(reference =>
                reference.Name == "MessagePipe.VContainer");
            Assert.That(integrationReference.AssetPath, Is.Not.Null.And.Not.Empty,
                "VContainer 통합 참조 경로가 해석되지 않아 경계 검사를 건너뛰어서는 안 된다.");

            // 프로젝트별 예외 에셋 없이 패키지 전체가 기본 외부 의존성 계약을 지키는지 검사한다.
            var violations = PackageBoundaryRules.FindViolations(
                assemblies,
                packageRoot,
                PackageBoundaryValidator.DefaultAllowedExternalReferences);

            Assert.That(violations, Is.Empty,
                string.Join("\n", violations.Select(violation => violation.BuildMessage())));
        }

        [Test]
        public void ViolationMessageExplainsTheReferenceAndTheReason()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Gameplay",
                "Scripts/Gameplay",
                new AssemblyReferenceInfo("Game.Content", "Assets/Game/Game.Content.asmdef"));

            var message = PackageBoundaryRules
                .FindViolations(new[] { assembly }, PackageRoot)[0]
                .BuildMessage();

            Assert.That(message, Does.Contain("HS.Framework.Gameplay"));
            Assert.That(message, Does.Contain($"{PackageRoot}/Scripts/Gameplay/HS.Framework.Gameplay.asmdef"));
            Assert.That(message, Does.Contain("Game.Content"));
            Assert.That(message, Does.Contain("Assets/Game/Game.Content.asmdef"));
            Assert.That(message, Does.Contain("다른 프로젝트로 옮길 때"));
        }

        [Test]
        public void AllowedExternalReferenceIsNotAViolation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Foundation",
                "Scripts/Foundation",
                new AssemblyReferenceInfo("R3.dll", "Assets/Packages/R3.1.3.1/lib/netstandard2.1/R3.dll", true));

            var violations = PackageBoundaryRules.FindViolations(
                new[] { assembly },
                PackageRoot,
                PackageBoundaryValidator.DefaultAllowedExternalReferences);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void PrecompiledReferenceOutsideTheAllowListIsAViolation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Foundation",
                "Scripts/Foundation",
                new AssemblyReferenceInfo("GamePlugin.dll", "Assets/Plugins/GamePlugin.dll", true));

            var violations = PackageBoundaryRules.FindViolations(
                new[] { assembly },
                PackageRoot,
                PackageBoundaryValidator.DefaultAllowedExternalReferences);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Reference.IsPrecompiled, Is.True);
            Assert.That(violations[0].BuildMessage(), Does.Contain("미리 컴파일된 DLL"));
        }

        [Test]
        public void ProjectAssemblyReferencingProjectAssemblyIsNotChecked()
        {
            var assembly = new AssemblyDefinitionInfo(
                "Game.Content",
                "Assets/Game/Game.Content.asmdef",
                new[] { new AssemblyReferenceInfo("Game.Ui", "Assets/Game/Ui/Game.Ui.asmdef") });

            var violations = PackageBoundaryRules.FindViolations(new[] { assembly }, PackageRoot);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ProjectAssemblyReferencingThePackageIsAllowedDirection()
        {
            var assembly = new AssemblyDefinitionInfo(
                "Game.Content",
                "Assets/Game/Game.Content.asmdef",
                new[]
                {
                    new AssemblyReferenceInfo(
                        "HS.Framework.Runtime",
                        $"{PackageRoot}/Scripts/Runtime/HS.Framework.Runtime.asmdef")
                });

            var violations = PackageBoundaryRules.FindViolations(new[] { assembly }, PackageRoot);

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void EveryViolatingReferenceIsReportedSeparately()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Gameplay",
                "Scripts/Gameplay",
                new AssemblyReferenceInfo("Game.Content", "Assets/Game/Game.Content.asmdef"),
                new AssemblyReferenceInfo("Game.Ui", "Assets/Game/Ui/Game.Ui.asmdef"),
                new AssemblyReferenceInfo(
                    "HS.Framework.Foundation",
                    $"{PackageRoot}/Scripts/Foundation/HS.Framework.Foundation.asmdef"));

            var violations = PackageBoundaryRules.FindViolations(new[] { assembly }, PackageRoot);

            Assert.That(violations, Has.Count.EqualTo(2));
        }

        [TestCase("Packages\\com.hs.framework\\Scripts\\Runtime", true)]
        [TestCase("Packages/com.hs.framework/", true)]
        [TestCase("Packages/com.hs.framework.extras/Scripts", false)]
        [TestCase("Assets/Game", false)]
        public void IsInsidePackageNormalizesPathsAndRequiresAFolderBoundary(string path, bool expected)
        {
            Assert.That(PackageBoundaryRules.IsInsidePackage(path, PackageRoot), Is.EqualTo(expected));
        }

        [TestCase("Assets/Game/Game.Content.asmdef", true)]
        [TestCase("Assets", false)]
        [TestCase("AssetsExtra/Game.asmdef", false)]
        [TestCase("Packages/com.hs.framework/Scripts", false)]
        [TestCase(null, false)]
        public void IsProjectAssetRecognizesOnlyAssetsFolderContent(string path, bool expected)
        {
            Assert.That(PackageBoundaryRules.IsProjectAsset(path), Is.EqualTo(expected));
        }

        [Test]
        public void CombineAllowedReferencesMergesSourcesWithoutDuplicates()
        {
            // 합치기 규칙만 재는 검사이므로 기본 허용 목록의 내용에 기대지 않는다.
            // 그 목록에 무엇이 들어 있는지는 DefaultAllowListCoversTheApprovedExternalDependencies 가 따로 지킨다.
            var combined = PackageBoundaryRules.CombineAllowedReferences(
                new[] { "R3.dll" },
                new[] { " MyPlugin.dll ", "r3.dll", string.Empty, null },
                new[] { "OtherPlugin.dll", "MyPlugin.dll" },
                null);

            Assert.That(combined, Is.EqualTo(new[] { "R3.dll", "MyPlugin.dll", "OtherPlugin.dll" }));
        }

        [Test]
        public void CombineAllowedReferencesReturnsEmptyListWithoutSources()
        {
            Assert.That(PackageBoundaryRules.CombineAllowedReferences(), Is.Empty);
            Assert.That(PackageBoundaryRules.CombineAllowedReferences(null), Is.Empty);
        }

        [Test]
        public void ProjectDeclaredAllowListSuppressesTheViolation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Foundation",
                "Scripts/Foundation",
                new AssemblyReferenceInfo("MyPlugin.dll", "Assets/Plugins/MyPlugin.dll", true));
            var projectAllowList = new[] { "MyPlugin.dll" };

            var withoutAllowList = PackageBoundaryRules.FindViolations(
                new[] { assembly },
                PackageRoot,
                PackageBoundaryValidator.DefaultAllowedExternalReferences);
            var withAllowList = PackageBoundaryRules.FindViolations(
                new[] { assembly },
                PackageRoot,
                PackageBoundaryRules.CombineAllowedReferences(
                    PackageBoundaryValidator.DefaultAllowedExternalReferences,
                    projectAllowList));

            Assert.That(withoutAllowList, Has.Count.EqualTo(1));
            Assert.That(withAllowList, Is.Empty);
        }

        [Test]
        public void DefaultAllowListCoversTheApprovedExternalDependencies()
        {
            Assert.That(PackageBoundaryValidator.DefaultAllowedExternalReferences, Does.Contain("R3.dll"));
            Assert.That(
                PackageBoundaryValidator.DefaultAllowedExternalReferences,
                Does.Contain("MessagePipe"),
                "MessagePipe 도 Assets 아래 설치라 UPM 의존성으로 표현할 수 없는 승인된 의존이다.");
            Assert.That(
                PackageBoundaryValidator.DefaultAllowedExternalReferences,
                Does.Contain("MessagePipe.VContainer"),
                "Runtime 의 VContainer 통합도 MessagePipe 와 함께 설치하는 승인된 외부 의존이다.");
        }

        [Test]
        public void MissingPackageRootSkipsValidation()
        {
            var assembly = CreatePackageAssembly(
                "HS.Framework.Gameplay",
                "Scripts/Gameplay",
                new AssemblyReferenceInfo("Game.Content", "Assets/Game/Game.Content.asmdef"));

            Assert.That(PackageBoundaryRules.FindViolations(new[] { assembly }, null), Is.Empty);
            Assert.That(PackageBoundaryRules.FindViolations(null, PackageRoot), Is.Empty);
        }

        private static AssemblyDefinitionInfo CreatePackageAssembly(
            string name,
            string folder,
            params AssemblyReferenceInfo[] references)
        {
            return new AssemblyDefinitionInfo($"{name}", $"{PackageRoot}/{folder}/{name}.asmdef", references);
        }
    }
}
