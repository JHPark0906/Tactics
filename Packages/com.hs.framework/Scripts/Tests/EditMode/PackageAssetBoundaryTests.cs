using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HS.Framework.Editor.Packaging;
using HS.Framework.Settings;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HS.Framework.Tests.EditMode
{
    public sealed class PackageAssetBoundaryTests
    {
        private const string PackageRoot = "Packages/com.hs.framework";
        private static readonly Regex GuidReference = new("guid: ([0-9a-f]{32})");
        private static readonly HashSet<string> SerializedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".asset", ".prefab", ".unity", ".mat", ".controller", ".overrideController", ".anim", ".inputactions"
        };

        [Test]
        public void ShippedAssetsResolveWithoutProjectContent()
        {
            var package = PackageInfo.FindForAssembly(typeof(ClientSettingsConfiguration).Assembly);
            Assert.That(package, Is.Not.Null);
            var violations = new List<string>();
            foreach (var assetPath in AssetDatabase.GetAllAssetPaths())
            {
                if (!assetPath.StartsWith(PackageRoot + "/", StringComparison.Ordinal)
                    || !SerializedExtensions.Contains(Path.GetExtension(assetPath)))
                {
                    continue;
                }

                var relativePath = assetPath.Substring(PackageRoot.Length + 1);
                var physicalPath = Path.Combine(package.resolvedPath, relativePath);
                InspectReferences(assetPath, physicalPath, violations);
                if (File.Exists(physicalPath + ".meta"))
                {
                    InspectReferences(assetPath + ".meta", physicalPath + ".meta", violations);
                }
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void DefaultSettingsLoadTheirOwnInputAssetAndValidate()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<ClientSettingsConfiguration>(
                PackageRoot + "/Settings/DefaultClientSettingsConfiguration.asset");
            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.TryValidate(out var error), Is.True, error);
            Assert.That(AssetDatabase.GetAssetPath(configuration.InputActionAsset),
                Is.EqualTo(PackageRoot + "/Settings/DefaultInputActions.inputactions"));
        }

        [Test]
        public void BootstrapUsesTheFrameworkScopeScript()
        {
            var package = PackageInfo.FindForAssembly(typeof(ClientSettingsConfiguration).Assembly);
            var scene = File.ReadAllText(Path.Combine(package.resolvedPath, "Scenes/Bootstrap.unity"));
            var expectedGuid = AssetDatabase.AssetPathToGUID(PackageRoot + "/Scripts/Runtime/FrameworkLifetimeScope.cs");
            Assert.That(expectedGuid, Is.Not.Empty);
            Assert.That(scene, Does.Contain("guid: " + expectedGuid));
        }

        private static void InspectReferences(string assetPath, string physicalPath, List<string> violations)
        {
            foreach (Match match in GuidReference.Matches(File.ReadAllText(physicalPath)))
            {
                var guid = match.Groups[1].Value;
                if (guid.StartsWith("0000000000000000", StringComparison.Ordinal))
                {
                    continue;
                }

                var dependency = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(dependency))
                {
                    violations.Add($"{assetPath}: 해석할 수 없는 GUID {guid}");
                }
                else if (dependency.StartsWith("Assets/", StringComparison.Ordinal) && !IsAllowedRuntime(dependency))
                {
                    violations.Add($"{assetPath}: 프로젝트 에셋 {dependency}에 의존한다.");
                }
            }
        }

        private static bool IsAllowedRuntime(string path)
        {
            foreach (var allowed in PackageBoundaryValidator.DefaultAllowedExternalReferences)
            {
                if (string.Equals(Path.GetFileName(path), allowed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileNameWithoutExtension(path), allowed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
