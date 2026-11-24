// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if DEBUG && !NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class AttributeQueryHelperPass2MutationTests : TestContainer
{
    public void GetAttributes_ReportsANullEntryOfTheAttributesArrayAndSkipsIt()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var marker = new MarkerAttribute();

        // GetAttributes is a lazy iterator: the null entry is only inspected while the result is enumerated.
        var found = AttributeQueryHelper.GetAttributes<MarkerAttribute>([null!, marker]).ToList();

        found.Should().Equal([marker], because: "a null entry never matches the requested attribute type and is skipped");
        assertions.Should().Equal(
            [$"AttributeQueryHelper: internal error: null entry in the attributes array while looking for '{typeof(MarkerAttribute)}'."],
            because: "a null entry in the attributes array is an internal error that has to be reported while enumerating");
    }

    public void GetAttributes_DoesNotReportAnythingWhenEveryEntryOfTheAttributesArrayIsSet()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var marker = new MarkerAttribute();
        var other = new OtherAttribute();

        var found = AttributeQueryHelper.GetAttributes<MarkerAttribute>([other, marker]).ToList();

        found.Should().Equal([marker], because: "only the attributes of the requested type are returned");
        assertions.Should().BeEmpty(because: "no entry of the attributes array is null");
    }

    [AttributeUsage(AttributeTargets.All)]
    private sealed class MarkerAttribute : Attribute;

    [AttributeUsage(AttributeTargets.All)]
    private sealed class OtherAttribute : Attribute;
}
#endif
