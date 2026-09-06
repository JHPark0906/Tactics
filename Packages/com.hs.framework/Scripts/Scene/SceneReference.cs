using System;
using UnityEngine;

namespace HS.Framework.Scene
{
    /// <summary>
    /// Build Settings에 등록된 씬을 전체 경로로 식별한다.
    /// </summary>
    [Serializable]
    public sealed class SceneReference : IEquatable<SceneReference>
    {
        [SerializeField] private string scenePath;
        [SerializeField] private string sceneGuid;

        /// <summary>씬의 Assets 기준 경로이다.</summary>
        public string ScenePath => scenePath;

#if UNITY_EDITOR
        /// <summary>에디터에서 씬 이동을 추적할 때 사용하는 Asset GUID이다.</summary>
        public string EditorSceneGuid => sceneGuid;
#endif

        /// <summary>표시에 사용할 씬 이름이다.</summary>
        public string DisplayName => SceneLoader.GetSceneDisplayName(scenePath);

        /// <summary>참조가 비어 있지 않은지 확인한다.</summary>
        public bool IsAssigned => !string.IsNullOrWhiteSpace(scenePath);

        /// <summary>전체 경로를 사용해 씬 참조를 생성한다.</summary>
        public static SceneReference Create(string path)
        {
            var normalizedPath = SceneLoader.NormalizeScenePath(path);
            var reference = new SceneReference { scenePath = normalizedPath };
#if UNITY_EDITOR
            reference.sceneGuid = UnityEditor.AssetDatabase.AssetPathToGUID(normalizedPath);
#endif
            return reference;
        }

        /// <inheritdoc />
        public bool Equals(SceneReference other)
        {
            return other != null && string.Equals(scenePath, other.scenePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => Equals(obj as SceneReference);

        /// <inheritdoc />
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(scenePath ?? string.Empty);
    }
}
