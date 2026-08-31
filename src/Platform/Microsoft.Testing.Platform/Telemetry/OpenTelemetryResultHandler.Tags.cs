// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed partial class OpenTelemetryResultHandler
{
    /// <summary>
    /// What Roslyn's <c>INamespaceSymbol.ToDisplayString()</c> returns for the global namespace.
    /// </summary>
    private const string RoslynGlobalNamespaceDisplayString = "<global namespace>";

    /// <summary>
    /// The OpenTelemetry conventions ask for the span name to be the test case name rather than an opaque id,
    /// because it is what shows up in trace waterfalls and what backends group on.
    /// </summary>
    private static string GetActivityName(TestNode testNode)
        => RoslynString.IsNullOrWhiteSpace(testNode.DisplayName) ? testNode.Uid.Value : testNode.DisplayName;

    private static string? GetSuiteName(TestNode testNode)
        => testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>()?.TypeName;

    /// <summary>
    /// Builds the value for <c>code.function.name</c>, which the convention defines as the fully qualified name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type in the global namespace contributes no namespace segment, so the name must not gain a leading dot.
    /// Two spellings have to be recognised, because the property is a plain string filled in by whichever test
    /// framework produced the node: an empty namespace (what MSTest produces, since it derives the namespace by
    /// splitting the managed type name) and Roslyn's <c>&lt;global namespace&gt;</c>, which is what
    /// <c>INamespaceSymbol.ToDisplayString()</c> returns when the caller does not first check
    /// <c>IsGlobalNamespace</c>.
    /// </para>
    /// <para>
    /// Dropping the segment - rather than emitting Roslyn's <c>global::</c> prefix - keeps the attribute
    /// language-agnostic, matches the convention's own examples, and is consistent with how the rest of this
    /// repository treats the global namespace (see <c>TestClassModelBuilder</c>, <c>ReflectionMetadataGenerator</c>
    /// and <c>DiscoveredTestsJsonSerializer</c>, which all omit it).
    /// </para>
    /// </remarks>
    private static string GetFullyQualifiedName(TestMethodIdentifierProperty identifierProperty)
        => IsGlobalNamespace(identifierProperty.Namespace)
            ? $"{identifierProperty.TypeName}.{identifierProperty.MethodName}"
            : $"{identifierProperty.Namespace}.{identifierProperty.TypeName}.{identifierProperty.MethodName}";

    private static bool IsGlobalNamespace(string? @namespace)
        => RoslynString.IsNullOrEmpty(@namespace)
            || @namespace == RoslynGlobalNamespaceDisplayString;

    private static string GetRunResultStatus(int failedTests, int exitCode)
        => failedTests > 0 || exitCode != (int)ExitCode.Success
            ? TestingPlatformSemanticConventions.TestResultStatus.Fail
            : TestingPlatformSemanticConventions.TestResultStatus.Pass;

    private IEnumerable<KeyValuePair<string, object?>> GetTestInitialInfo(TestNode testNode, TestNodeUid? parentUid)
    {
        yield return new(TestingPlatformSemanticConventions.Attributes.TestCaseName, testNode.DisplayName);
        yield return new(TestingPlatformSemanticConventions.Attributes.TestCaseId, testNode.Uid.Value);
        if (parentUid is not null)
        {
            yield return new(TestingPlatformSemanticConventions.Attributes.TestCaseParentId, parentUid.Value);
        }

        if (_options.EmitLegacyAttributes)
        {
            yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestName, testNode.DisplayName);
            yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestId, testNode.Uid.Value);
            if (parentUid is not null)
            {
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestParentId, parentUid.Value);
            }
        }

        // Single pass over the property bag for the two singleton properties below: two separate
        // SingleOrDefault<T>() calls walked the whole bag once each.
        TestMethodIdentifierProperty? identifierProperty = null;
        TestFileLocationProperty? testLocationProperty = null;
        PropertyBag.PropertyBagEnumerator enumerator = testNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TestMethodIdentifierProperty identifier:
                    if (identifierProperty is not null)
                    {
                        throw new InvalidOperationException($"Found multiple properties of type '{typeof(TestMethodIdentifierProperty)}'.");
                    }

                    identifierProperty = identifier;
                    break;
                case TestFileLocationProperty location:
                    if (testLocationProperty is not null)
                    {
                        throw new InvalidOperationException($"Found multiple properties of type '{typeof(TestFileLocationProperty)}'.");
                    }

                    testLocationProperty = location;
                    break;
            }
        }

        if (identifierProperty is not null)
        {
            // code.function.name is defined as the *fully qualified* name; there is no separate namespace
            // attribute (code.namespace is deprecated upstream).
            yield return new(TestingPlatformSemanticConventions.Attributes.CodeFunctionName, GetFullyQualifiedName(identifierProperty));
            yield return new(TestingPlatformSemanticConventions.Attributes.TestSuiteName, identifierProperty.TypeName);
            yield return new(TestingPlatformSemanticConventions.Attributes.TestAssemblyName, identifierProperty.AssemblyFullName);

            if (_options.EmitLegacyAttributes)
            {
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestMethod, identifierProperty.MethodName);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestClass, identifierProperty.TypeName);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestNamespace, identifierProperty.Namespace);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestAssembly, identifierProperty.AssemblyFullName);
            }
        }

        if (testLocationProperty is not null)
        {
            yield return new(TestingPlatformSemanticConventions.Attributes.CodeFilePath, testLocationProperty.FilePath);
            yield return new(TestingPlatformSemanticConventions.Attributes.CodeLineNumber, testLocationProperty.LineSpan.Start.Line);

            if (_options.EmitLegacyAttributes)
            {
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestFilePath, testLocationProperty.FilePath);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestLineStart, testLocationProperty.LineSpan.Start.Line);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestLineEnd, testLocationProperty.LineSpan.End.Line);
            }
        }

        // The metadata is yielded after the blocks above, so it needs its own pass. OfType<TestMetadataProperty>()
        // would materialize a TProperty[] just to enumerate it once; the struct enumerator allocates nothing.
        PropertyBag.PropertyBagEnumerator metadataEnumerator = testNode.Properties.GetStructEnumerator();
        while (metadataEnumerator.MoveNext())
        {
            if (metadataEnumerator.Current is not TestMetadataProperty metadata)
            {
                continue;
            }

            yield return new KeyValuePair<string, object?>($"{TestingPlatformSemanticConventions.Attributes.TestMetadataPrefix}{metadata.Key}", metadata.Value);
            if (_options.EmitLegacyAttributes)
            {
                yield return new KeyValuePair<string, object?>($"{TestingPlatformSemanticConventions.Attributes.LegacyTestMetadataPrefix}{metadata.Key}", metadata.Value);
            }
        }
    }
}
