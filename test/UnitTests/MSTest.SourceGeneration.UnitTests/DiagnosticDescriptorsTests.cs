// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class DiagnosticDescriptorsTests
{
    [TestMethod]
    [DataRow(
        "AOTSG0001",
        "Test class is static",
        "[TestClass] type '{0}' is static and cannot be instantiated by the generated registry",
        "Static test classes cannot be instantiated by the generated registry. Remove the 'static' modifier or use a non-static container.")]
    [DataRow(
        "AOTSG0002",
        "Test class is generic",
        "[TestClass] type '{0}' has unbound type parameters (either directly or via a generic outer type) and cannot be materialized as a closed type",
        "Open generic test classes (or test classes nested inside a generic outer type) have no closed type at generation time. Use a concrete subclass that closes every type parameter.")]
    [DataRow(
        "AOTSG0003",
        "Test class is not accessible from generated code",
        "[TestClass] type '{0}' is not reachable from generated code in the same assembly (file-local, or nested in a private/private-protected outer type)",
        "Generated registry code lives in the same assembly but in a different file/type and therefore cannot reference file-local types or types nested in a private (or private-protected) outer type. Make the test class \u2014 and every enclosing type \u2014 at least internal.")]
    [DataRow(
        "AOTSG0004",
        "Test method is generic",
        "Method '{0}.{1}' has type parameters which are not knowable at compile time; the source-generated invoker will skip it",
        "Generic test methods would need a concrete type-argument list at the invocation site. Replace the method with one or more non-generic specializations.")]
    [DataRow(
        "AOTSG0005",
        "Parameter uses a by-ref kind",
        "Parameter '{2}' of '{0}.{1}' is declared with 'ref', 'in', or 'out' and cannot be passed through the 'object?[]' invoker; the member will be skipped",
        "By-ref parameters cannot flow through the 'Func<object?, object?[]?, Task>' invoker shape. Drop the ref/in/out modifier or move the dependency out of the test signature.")]
    public void Descriptor_DescribesTheDiagnosticExactly(string id, string title, string messageFormat, string description)
    {
        DiagnosticDescriptor descriptor = DiagnosticDescriptors.GetById(id);

        descriptor.Id.Should().Be(id);
        descriptor.Title.ToString(CultureInfo.InvariantCulture).Should().Be(title);
        descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture).Should().Be(messageFormat);
        descriptor.Description.ToString(CultureInfo.InvariantCulture).Should().Be(description);
        descriptor.Category.Should().Be("MSTest.AotReflection");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.HelpLinkUri.Should().Be($"https://github.com/microsoft/testfx/blob/main/docs/source-generator/diagnostics.md#{id.ToLowerInvariant()}");
    }

    [TestMethod]
    public void GetById_WithKnownId_ReturnsTheMatchingDescriptor() =>
        DiagnosticDescriptors.GetById("AOTSG0003").Should().BeSameAs(DiagnosticDescriptors.InaccessibleTestClass);

    [TestMethod]
    public void GetById_WithUnknownId_ThrowsAndReportsTheId()
    {
        Action act = () => DiagnosticDescriptors.GetById("AOTSG9999");

        act.Should().Throw<InvalidOperationException>().WithMessage("Unknown diagnostic id 'AOTSG9999'.");
    }
}
