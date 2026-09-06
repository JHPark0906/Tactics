using System;
using System.Collections.Generic;
using HS.Framework.UI.Settings;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>설정 섹션 ViewModel이 공용으로 사용하는 옵션 헬퍼의 동작을 검증한다.</summary>
    public sealed class SettingsOptionsTests
    {
        [Test]
        public void IndexOfValueReturnsMatchingIndex()
        {
            var options = CreateOptions("ko", "en", "ja");

            Assert.That(SettingsOptions.IndexOfValue(options, "ja"), Is.EqualTo(2));
        }

        [Test]
        public void IndexOfValueReturnsNegativeWhenValueIsMissing()
        {
            var options = CreateOptions("ko", "en");

            Assert.That(SettingsOptions.IndexOfValue(options, "ja"), Is.EqualTo(-1));
            Assert.That(SettingsOptions.IndexOfValue<string>(null, "ko"), Is.EqualTo(-1));
        }

        [Test]
        public void IndexOfValueUsesGivenComparer()
        {
            var options = CreateOptions("ko", "en");

            Assert.That(SettingsOptions.IndexOfValue(options, "KO", StringComparer.Ordinal), Is.EqualTo(-1));
            Assert.That(
                SettingsOptions.IndexOfValue(options, "KO", StringComparer.OrdinalIgnoreCase),
                Is.EqualTo(0));
        }

        [Test]
        public void IndexOfValueOrDefaultFallsBackWhenValueIsMissing()
        {
            var options = CreateOptions("ko", "en");

            Assert.That(SettingsOptions.IndexOfValueOrDefault(options, "ja"), Is.EqualTo(0));
            Assert.That(
                SettingsOptions.IndexOfValueOrDefault(options, "ja", StringComparer.Ordinal, 1),
                Is.EqualTo(1));
        }

        [TestCase(-5, 3, 0)]
        [TestCase(0, 3, 0)]
        [TestCase(2, 3, 2)]
        [TestCase(7, 3, 2)]
        [TestCase(7, 0, 0)]
        public void ClampSelectedIndexKeepsIndexInsideOptionRange(int index, int count, int expected)
        {
            Assert.That(SettingsOptions.ClampSelectedIndex(index, count), Is.EqualTo(expected));
        }

        private static IReadOnlyList<SettingsOptionViewModel<string>> CreateOptions(params string[] values)
        {
            var options = new List<SettingsOptionViewModel<string>>();
            foreach (var value in values)
            {
                options.Add(new SettingsOptionViewModel<string>(value, value, value));
            }

            return options;
        }
    }
}
