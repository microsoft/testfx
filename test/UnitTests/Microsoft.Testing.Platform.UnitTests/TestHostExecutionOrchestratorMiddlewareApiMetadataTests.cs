// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

using PublicApi = Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Focused API-metadata regression coverage for the v1 test host execution orchestrator middleware surface.
/// Verifies that every new public type and member introduced for middleware composition carries its own
/// explicit <see cref="ExperimentalAttribute"/> (diagnostic id <c>TPEXP</c>) rather than relying only on a
/// containing type's attribute, and that the pre-existing <see cref="PublicApi.ITestHostOrchestratorManager"/>
/// contract was not widened with a new required member. This is reflection-only metadata inspection: it
/// does not build or require any additional target framework beyond whatever this test project already
/// targets.
/// </summary>
[TestClass]
public sealed class TestHostExecutionOrchestratorMiddlewareApiMetadataTests
{
    private const string ExpectedDiagnosticId = "TPEXP";
    private const string ExpectedUrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}";

    [TestMethod]
    public void NewMiddlewareType_ITestHostExecutionOrchestratorMiddleware_HasExplicitExperimentalAttribute()
        => AssertHasExplicitExperimentalAttribute(typeof(ITestHostExecutionOrchestratorMiddleware));

    [TestMethod]
    public void NewMiddlewareType_ITestHostExecutionOrchestratorMiddleware_OrchestrateMethod_HasExplicitExperimentalAttribute()
    {
        MethodInfo method = typeof(ITestHostExecutionOrchestratorMiddleware).GetMethod(
            nameof(ITestHostExecutionOrchestratorMiddleware.OrchestrateTestHostExecutionAsync))
            ?? throw new InvalidOperationException("Method not found via reflection - has it been renamed?");

        AssertHasExplicitExperimentalAttribute(method);
    }

    [TestMethod]
    public void NewMiddlewareType_ITestHostExecutionOrchestratorMiddlewareManager_HasExplicitExperimentalAttribute()
        => AssertHasExplicitExperimentalAttribute(typeof(PublicApi.ITestHostExecutionOrchestratorMiddlewareManager));

    [TestMethod]
    public void NewMiddlewareType_ITestHostExecutionOrchestratorMiddlewareManager_AddMethod_HasExplicitExperimentalAttribute()
    {
        MethodInfo method = typeof(PublicApi.ITestHostExecutionOrchestratorMiddlewareManager).GetMethod(
            nameof(PublicApi.ITestHostExecutionOrchestratorMiddlewareManager.AddTestHostExecutionOrchestratorMiddleware))
            ?? throw new InvalidOperationException("Method not found via reflection - has it been renamed?");

        AssertHasExplicitExperimentalAttribute(method);
    }

    [TestMethod]
    public void NewMiddlewareType_TestHostOrchestratorManagerExtensions_HasExplicitExperimentalAttribute()
        => AssertHasExplicitExperimentalAttribute(typeof(PublicApi.TestHostOrchestratorManagerExtensions));

    [TestMethod]
    public void NewMiddlewareType_TestHostOrchestratorManagerExtensions_AddMethod_HasExplicitExperimentalAttribute()
    {
        MethodInfo method = typeof(PublicApi.TestHostOrchestratorManagerExtensions).GetMethod(
            nameof(PublicApi.TestHostOrchestratorManagerExtensions.AddTestHostExecutionOrchestratorMiddleware))
            ?? throw new InvalidOperationException("Method not found via reflection - has it been renamed?");

        AssertHasExplicitExperimentalAttribute(method);
    }

    [TestMethod]
    public void ExistingITestHostOrchestratorManager_ContractIsUnchanged_NoNewRequiredMemberWasAdded()
    {
        // Middleware registration was deliberately added as a *separate*, optional capability
        // (ITestHostExecutionOrchestratorMiddlewareManager) plus an extension method, precisely so this
        // interface would not gain a new required member and break existing implementers. This test pins
        // that contract down: if it starts failing, a new member was added directly here instead of through
        // the optional-capability + extension-method pattern, which is a breaking change for any external
        // ITestHostOrchestratorManager implementation.
        MethodInfo[] declaredMethods = typeof(PublicApi.ITestHostOrchestratorManager).GetMethods();
        string[] declaredMethodNames = [.. declaredMethods.Select(static m => m.Name)];

        Assert.HasCount(2, declaredMethods);
        Assert.Contains(
            nameof(PublicApi.ITestHostOrchestratorManager.AddTestHostOrchestrator),
            declaredMethodNames);
        Assert.Contains(
            nameof(PublicApi.ITestHostOrchestratorManager.AddTestHostOrchestratorApplicationLifetime),
            declaredMethodNames);
    }

    [TestMethod]
    public void ExistingManager_DoesNotImplementMiddlewareCapabilityByDefault()
    {
        // The optional capability interface must not be part of the base contract itself, or every existing
        // implementer would suddenly be required to implement it too.
        Type middlewareCapability = typeof(PublicApi.ITestHostExecutionOrchestratorMiddlewareManager);
        Type baseManager = typeof(PublicApi.ITestHostOrchestratorManager);

        Assert.IsFalse(middlewareCapability.IsAssignableFrom(baseManager));
    }

    private static void AssertHasExplicitExperimentalAttribute(MemberInfo member)
    {
        // Matched by full type name rather than typeof(ExperimentalAttribute) so this test is agnostic to
        // whether the running target framework resolves the attribute to the BCL type (NETCOREAPP TFMs) or
        // to this repository's embedded polyfill (non-NETCOREAPP TFMs) - both are named identically.
        CustomAttributeData? attribute = member.GetCustomAttributesData().FirstOrDefault(
            static data => data.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.ExperimentalAttribute");

        Assert.IsNotNull(attribute, $"'{member}' must carry an explicit [Experimental] attribute of its own, not merely inherit one from its containing type.");

        string? diagnosticId = attribute.ConstructorArguments is [{ Value: string ctorDiagnosticId }] ? ctorDiagnosticId : null;
        Assert.AreEqual(ExpectedDiagnosticId, diagnosticId, $"'{member}' has an [Experimental] attribute with an unexpected diagnostic id.");

        string? urlFormat = attribute.NamedArguments
            .Where(static arg => arg.MemberName == nameof(ExperimentalAttribute.UrlFormat))
            .Select(static arg => arg.TypedValue.Value as string)
            .FirstOrDefault();
        Assert.AreEqual(ExpectedUrlFormat, urlFormat, $"'{member}' has an [Experimental] attribute with an unexpected UrlFormat.");
    }
}
