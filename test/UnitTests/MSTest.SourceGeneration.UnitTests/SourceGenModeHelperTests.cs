// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class SourceGenModeHelperTests
{
    [TestMethod]
    public void IsReflectionFree_WhenPropertyIsReflectionFree_ReturnsTrue()
        => SourceGenModeHelper.IsReflectionFree(new FakeOptions("ReflectionFree")).Should().BeTrue();

    [TestMethod]
    public void IsReflectionFree_IgnoresCasingOfPropertyValue()
        => SourceGenModeHelper.IsReflectionFree(new FakeOptions("reflectionFREE")).Should().BeTrue();

    [TestMethod]
    public void IsReflectionFree_WhenPropertyHasAnotherValue_ReturnsFalse()
        => SourceGenModeHelper.IsReflectionFree(new FakeOptions("Rooting")).Should().BeFalse();

    [TestMethod]
    public void IsReflectionFree_WhenPropertyIsNotSet_ReturnsFalse()
        => SourceGenModeHelper.IsReflectionFree(new FakeOptions(null)).Should().BeFalse();

    private sealed class FakeOptions(string? value) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? result)
        {
            result = key == "build_property.MSTestSourceGenMode" ? value : null;
            return result is not null;
        }
    }
}
