// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class DiagnosticInfoTests
{
    private const string ExpectedMessage = "[TestClass] type 'MyType' is static and cannot be instantiated by the generated registry";

    [TestMethod]
    public void ToDiagnostic_WithLocation_ReportsTheSourceLocationAndFormatsTheMessage()
    {
        var location = new LocationInfo("C.cs", new TextSpan(6, 1), new LinePositionSpan(new LinePosition(0, 6), new LinePosition(0, 7)));
        var info = DiagnosticInfo.Create(DiagnosticDescriptors.StaticTestClass, location, "MyType");

        var diagnostic = info.ToDiagnostic();

        diagnostic.Id.Should().Be("AOTSG0001");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(ExpectedMessage);
        diagnostic.Location.Kind.Should().Be(LocationKind.ExternalFile);
        diagnostic.Location.SourceSpan.Should().Be(new TextSpan(6, 1));
        diagnostic.Location.GetLineSpan().Path.Should().Be("C.cs");
    }

    [TestMethod]
    public void ToDiagnostic_WithoutLocation_ReportsNoLocation()
    {
        var info = DiagnosticInfo.Create(DiagnosticDescriptors.StaticTestClass, location: null, "MyType");

        var diagnostic = info.ToDiagnostic();

        diagnostic.Location.Should().Be(Location.None);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(ExpectedMessage);
    }
}
