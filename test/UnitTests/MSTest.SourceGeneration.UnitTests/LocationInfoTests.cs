// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class LocationInfoTests
{
    private static readonly LocationInfo ExpectedTypeLocation = new("C.cs", new TextSpan(0, 11), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 11)));

    [TestMethod]
    public void CreateFrom_Symbol_UsesTheDeclarationLocation()
    {
        CSharpCompilation compilation = RoslynTestHelpers.CreateCompilation("class C { }", path: "C.cs");

        var info = LocationInfo.CreateFrom(compilation.GetSourceType("C"));

        info.Should().Be(ExpectedTypeLocation);
    }

    [TestMethod]
    public void CreateFrom_SymbolWithoutSyntax_ReturnsNull()
    {
        CSharpCompilation compilation = RoslynTestHelpers.CreateCompilation("class C { }", path: "C.cs");

        LocationInfo.CreateFrom(compilation.GetSpecialType(SpecialType.System_Object)).Should().BeNull();
    }

    [TestMethod]
    public void CreateFrom_Node_UsesTheNodeLocation()
    {
        CSharpCompilation compilation = RoslynTestHelpers.CreateCompilation("class C { }", path: "C.cs");
        SyntaxNode node = compilation.SyntaxTrees.Single().GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single();

        LocationInfo.CreateFrom(node).Should().Be(ExpectedTypeLocation);
    }

    [TestMethod]
    public void CreateFrom_LocationWithoutSourceTree_ReturnsNull()
        => LocationInfo.CreateFrom(Location.None).Should().BeNull();

    [TestMethod]
    public void ToLocation_RoundTripsTheCapturedLocation()
    {
        var location = ExpectedTypeLocation.ToLocation();

        location.Kind.Should().Be(LocationKind.ExternalFile);
        location.SourceSpan.Should().Be(ExpectedTypeLocation.SourceSpan);
        location.GetLineSpan().Span.Should().Be(ExpectedTypeLocation.LineSpan);
        location.GetLineSpan().Path.Should().Be("C.cs");
    }
}
