using HS.Framework.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>클라이언트 설정 구성의 필수 및 선택 프리셋 검증 규칙을 확인한다.</summary>
    public sealed class ClientSettingsConfigurationTests
    {
        [Test]
        public void ValidationSucceedsWithoutLocalePreset()
        {
            var configuration = ScriptableObject.CreateInstance<ClientSettingsConfiguration>();
            var inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            var displayPreset = ScriptableObject.CreateInstance<DisplaySettingsPreset>();
            var audioPreset = ScriptableObject.CreateInstance<AudioSettingsPreset>();
            try
            {
                var serialized = new SerializedObject(configuration);
                serialized.FindProperty("inputActionAsset").objectReferenceValue = inputActionAsset;
                serialized.FindProperty("displaySettingsPreset").objectReferenceValue = displayPreset;
                serialized.FindProperty("audioSettingsPreset").objectReferenceValue = audioPreset;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(configuration.TryValidate(out var error), Is.True, error);
                Assert.That(configuration.LocaleSettingsPreset, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(configuration);
                Object.DestroyImmediate(inputActionAsset);
                Object.DestroyImmediate(displayPreset);
                Object.DestroyImmediate(audioPreset);
            }
        }

        [Test]
        public void ValidationStillRequiresDisplayAndAudioPresets()
        {
            var configuration = ScriptableObject.CreateInstance<ClientSettingsConfiguration>();
            var inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                var serialized = new SerializedObject(configuration);
                serialized.FindProperty("inputActionAsset").objectReferenceValue = inputActionAsset;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(configuration.TryValidate(out var error), Is.False);
                Assert.That(error, Does.Contain("프리셋"));
            }
            finally
            {
                Object.DestroyImmediate(configuration);
                Object.DestroyImmediate(inputActionAsset);
            }
        }
    }
}
