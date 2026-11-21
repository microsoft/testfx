// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
#if DEBUG && !NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class MethodInfoExtensionsMutationTests : TestContainer
{
#if DEBUG && !NETFRAMEWORK
    public void HasCorrectClassOrAssemblyInitializeSignatureShouldAssertThatTheMethodIsNotNull()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        MethodInfo method = null!;

        Action act = () => _ = method.HasCorrectClassOrAssemblyInitializeSignature();

        act.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("method should not be null.");
    }

    public void HasCorrectClassOrAssemblyCleanupSignatureShouldAssertThatTheMethodIsNotNull()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        MethodInfo method = null!;

        method.HasCorrectClassOrAssemblyCleanupSignature().Should().BeFalse();

        assertions.Should().Equal("method should not be null.");
    }

    public void HasCorrectTestInitializeOrCleanupSignatureShouldAssertThatTheMethodIsNotNull()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        MethodInfo method = null!;

        Action act = () => _ = method.HasCorrectTestInitializeOrCleanupSignature();

        act.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("method should not be null.");
    }

    public void HasCorrectTestMethodSignatureShouldAssertThatTheMethodIsNotNull()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        MethodInfo method = null!;

        Action act = () => _ = method.HasCorrectTestMethodSignature(ignoreParameterLength: true);

        act.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("method should not be null.");
    }

    public void ConstructGenericMethodShouldAssertThatTheMethodIsGeneric()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        MethodInfo constructGenericMethod = GetPrivateExtensionMethod("ConstructGenericMethod");
        MethodInfo nonGenericMethod = typeof(object).GetMethod(nameof(ToString))!;

        Action act = () => constructGenericMethod.Invoke(null, [nonGenericMethod, nonGenericMethod.GetParameters(), null]);

        act.Should().Throw<TargetInvocationException>().WithInnerException<TestFailedException>();
        assertions.Should().Equal("ConstructGenericMethod should only be called for a generic method.");
    }
#endif

    public async Task InvokeAsFixtureMethodAsyncShouldHandOverTheExecutionContextCapturedAfterTheFixtureRan()
    {
        using var testContext = new TestContextImplementation(null, typeof(ContextFlowFixture).FullName, new Dictionary<string, object?>(), null, null);
        MethodInfo fixtureMethod = typeof(ContextFlowFixture).GetMethod(nameof(ContextFlowFixture.Initialize))!;
        ExecutionContext? captured = null;
        List<string> calls = [];

        await fixtureMethod.InvokeAsFixtureMethodAsync(
            testContext,
            context =>
            {
                captured = context;
                calls.Add("captured");
            },
            () => calls.Add("after captured"));

        calls.Should().Equal("captured", "after captured");
        captured.Should().NotBeNull();
        string? observed = null;
        ExecutionContext.Run(captured!, _ => observed = ContextFlowFixture.Value.Value, null);
        observed.Should().Be("set by fixture");
    }

    public void GetMapIndexForParameterTypeShouldThrowAnUnreachableExceptionWhenTheTypeIsNotInTheMap()
    {
        MethodInfo getMapIndex = GetPrivateExtensionMethod("GetMapIndexForParameterType");
        var map = new (Type GenericDefinition, Type? Substitution)[] { (typeof(string), null) };

        Action act = () => getMapIndex.Invoke(null, [typeof(int), map]);

        // UnreachableException is a per-assembly polyfill on some target frameworks, so only its full name is portable.
        act.Should().Throw<TargetInvocationException>()
            .Which.InnerException!.GetType().FullName.Should().Be("System.Diagnostics.UnreachableException");
    }

    // System.Reflection also declares a MethodInfoExtensions type, so the adapter's one is referenced by its full name.
    private static MethodInfo GetPrivateExtensionMethod(string name)
        => typeof(Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions.MethodInfoExtensions).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static class ContextFlowFixture
    {
        public static readonly AsyncLocal<string?> Value = new();

        public static void Initialize() => Value.Value = "set by fixture";
    }
}
