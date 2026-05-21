// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Guarantees that every public assertion API is covered by <c>[StackTraceHidden]</c>, so
/// MSTest framework frames are stripped from <see cref="Exception.StackTrace"/> on
/// runtimes that honor the attribute (.NET 6+). The reflection-based checks ensure new
/// public APIs added to <see cref="Assert"/>/<see cref="CollectionAssert"/>/<see cref="StringAssert"/>
/// in the future cannot silently regress this guarantee, and two runtime smoke tests
/// confirm the attribute is actually honored end-to-end.
/// </summary>
public class AssertStackTraceHiddenTests : TestContainer
{
    // We compare attributes by full type name rather than by Type identity because each
    // assembly that needs the polyfill (MSTest.TestFramework, the test assembly itself, …)
    // contributes its own internal copy of System.Diagnostics.StackTraceHiddenAttribute on
    // pre-.NET 6 TFMs. Their FullName matches; their Type identities do not.
    private const string StackTraceHiddenAttributeFullName = "System.Diagnostics.StackTraceHiddenAttribute";

    public void Assert_AllPublicMethodsAreCoveredByStackTraceHidden()
        => AssertAllPublicMethodsAreCovered(typeof(Assert));

    public void CollectionAssert_AllPublicMethodsAreCoveredByStackTraceHidden()
        => AssertAllPublicMethodsAreCovered(typeof(CollectionAssert));

    public void StringAssert_AllPublicMethodsAreCoveredByStackTraceHidden()
        => AssertAllPublicMethodsAreCovered(typeof(StringAssert));

    public void AssertExtensions_AllPublicMethodsAreCoveredByStackTraceHidden()
    {
        Type extensionsType = typeof(Assert).Assembly.GetType("Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions", throwOnError: true)!;
        AssertAllPublicMethodsAreCovered(extensionsType);
    }

    public void Assert_NestedInterpolatedStringHandlersAreCoveredByStackTraceHidden()
        => AssertNestedHandlerTypesAreCovered(typeof(Assert));

    public void InternalAssertionHelpers_AreCoveredByStackTraceHidden()
    {
        // These helpers sit between Assert/CollectionAssert/StringAssert and the throw site;
        // they need to be hidden so the "hidden span" of frames is contiguous (see #8277).
        string[] internalHelperNames =
        [
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertScope",
            "Microsoft.VisualStudio.TestTools.UnitTesting.StructuredAssertionMessage",
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertionValueRenderer",
            "Microsoft.VisualStudio.TestTools.UnitTesting.EvidenceBlock",
            "Microsoft.VisualStudio.TestTools.UnitTesting.EvidenceLine",
        ];

        Assembly assembly = typeof(Assert).Assembly;
        foreach (string typeName in internalHelperNames)
        {
            Type type = assembly.GetType(typeName, throwOnError: true)!;
            HasStackTraceHiddenAttribute(type)
                .Should()
                .BeTrue($"internal helper '{typeName}' should carry [StackTraceHidden] so framework frames between Assert.* and the throw site are contiguously hidden");
        }
    }

    public void AssertFailure_StackTraceDoesNotContainAnyFrameworkFrameOnNet6Plus()
    {
        // Behavioral smoke test: forces a real assertion failure and confirms the runtime
        // actually strips MSTest frames from the captured stack trace. Reflection-only
        // checks can't catch issues like a broken polyfill or the BCL ignoring our
        // attribute, so we keep this one runtime check as a backstop.
        AssertFailedException exception = CaptureFailure(() => Assert.AreEqual(1, 2));

#if NET6_0_OR_GREATER
        exception.StackTrace.Should().NotBeNull();
        exception.StackTrace!
            .Should()
            .NotContain("Microsoft.VisualStudio.TestTools.UnitTesting.Assert.", because: $"[StackTraceHidden] should strip Assert frames on .NET 6+. Actual:{Environment.NewLine}{exception.StackTrace}");
#else
        exception.Should().NotBeNull();
#endif
    }

    public void AssertFailureInsideScope_StackTraceDoesNotContainAnyFrameworkFrameOnNet6Plus()
    {
        // Verifies the AssertScope rethrow path also produces a hidden stack on .NET 6+:
        // the original failure is captured by ExceptionDispatchInfo and re-thrown from
        // AssertScope.Dispose(), so AssertScope itself must also be hidden.
        AssertFailedException exception = CaptureFailure(() =>
        {
            using (Assert.Scope())
            {
                Assert.AreEqual(1, 2);
            }
        });

#if NET6_0_OR_GREATER
        exception.StackTrace.Should().NotBeNull();
        exception.StackTrace!
            .Should()
            .NotContain("Microsoft.VisualStudio.TestTools.UnitTesting.AssertScope.", because: $"AssertScope.Dispose() must also be hidden. Actual:{Environment.NewLine}{exception.StackTrace}");
        exception.StackTrace!
            .Should()
            .NotContain("Microsoft.VisualStudio.TestTools.UnitTesting.Assert.", because: $"the captured failure frame itself must remain hidden. Actual:{Environment.NewLine}{exception.StackTrace}");
#else
        exception.Should().NotBeNull();
#endif
    }

    private static void AssertAllPublicMethodsAreCovered(Type type)
    {
        bool typeIsHidden = HasStackTraceHiddenAttribute(type);

        MethodInfo[] publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        List<string> uncovered = [];
        foreach (MethodInfo method in publicMethods)
        {
            // Property accessors, operator overloads and compiler-emitted methods do not
            // appear in user-visible stack traces; skip them to keep the contract focused on
            // surface APIs.
            if (method.IsSpecialName)
            {
                continue;
            }

            // Equals/GetHashCode/ToString inherited via 'public static new' overloads on Assert
            // are obsolete sentinels; they still go through Fail (hidden) so don't need
            // independent annotation, but they are covered for free when the declaring type
            // is hidden. Inherited Object methods (no override) aren't returned with
            // DeclaredOnly, so nothing to skip here.
            if (typeIsHidden || HasStackTraceHiddenAttribute(method))
            {
                continue;
            }

            uncovered.Add($"{method.DeclaringType!.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
        }

        uncovered.Should().BeEmpty(
            $"every public method on {type.FullName} must be covered by [StackTraceHidden] (either directly or via its declaring type) so MSTest frames are stripped from user stack traces on .NET 6+. Uncovered: {string.Join(", ", uncovered)}");
    }

    private static void AssertNestedHandlerTypesAreCovered(Type ownerType)
    {
        Type[] nestedTypes = ownerType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
        Type[] handlers = nestedTypes
            .Where(static t => t.Name.Contains("InterpolatedStringHandler", StringComparison.Ordinal))
            .ToArray();

        // Sanity check: the assertion API exposes several InterpolatedStringHandler types; if
        // this drops to zero we've likely broken our reflection assumptions rather than
        // actually removed every handler.
        handlers.Should().NotBeEmpty($"{ownerType.FullName} is expected to expose nested *InterpolatedStringHandler structs");

        List<string> uncovered = [];
        foreach (Type handler in handlers.Where(static handler => !HasStackTraceHiddenAttribute(handler)))
        {
            uncovered.Add(handler.FullName ?? handler.Name);
        }

        uncovered.Should().BeEmpty(
            $"every nested *InterpolatedStringHandler under {ownerType.FullName} must carry [StackTraceHidden] (nested types have their own DeclaringType, so the outer type's attribute does not propagate). Uncovered: {string.Join(", ", uncovered)}");
    }

    private static bool HasStackTraceHiddenAttribute(MemberInfo member)
        => member.GetCustomAttributesData()
            .Any(static attr => attr.AttributeType.FullName == StackTraceHiddenAttributeFullName);

    private static AssertFailedException CaptureFailure(Action action)
    {
        try
        {
            action();
        }
        catch (AssertFailedException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected AssertFailedException was not thrown.");
    }
}
