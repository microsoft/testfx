// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LegacyExtensionContractTests
{
    [TestMethod]
    [DataRow("Microsoft.Testing.Platform.Extensions.TestHostOrchestrator.ITestHostOrchestrator")]
    [DataRow("Microsoft.Testing.Platform.Extensions.TestHostOrchestrator.ITestHostOrchestratorManager")]
    [DataRow("Microsoft.Testing.Platform.Extensions.TestHostOrchestrator.TestHostOrchestratorManager")]
    public void RetiredOrchestratorIdentity_IsNotExported(string typeName)
        => Assert.IsNull(typeof(ITestApplicationBuilder).Assembly.GetType(typeName));

    [TestMethod]
    public void ConcreteBuilder_DoesNotExposeCompatibilityGetter()
        => Assert.IsNull(typeof(TestApplicationBuilder).GetProperty(
            nameof(ITestApplicationBuilder.TestHostOrchestrator),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

    [TestMethod]
    [DataRow(typeof(IProcess))]
    [DataRow(typeof(SystemProcess))]
    [DataRow(typeof(TestHostHandleToProcessAdapter))]
    public void ProcessWait_RequiresExplicitCancellationToken(Type processType)
    {
        Assert.IsNull(processType.GetMethod(nameof(IProcess.WaitForExitAsync), Type.EmptyTypes));
        Assert.IsNotNull(processType.GetMethod(nameof(IProcess.WaitForExitAsync), [typeof(CancellationToken)]));
    }

    [TestMethod]
    [DataRow("Microsoft.Testing.Framework")]
    [DataRow("MSTest.Engine")]
    public void RetiredEngine_DoesNotHaveFriendAccess(string assemblyName)
    {
        string?[] friends = typeof(ITestApplicationBuilder).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => new AssemblyName(attribute.AssemblyName).Name)
            .ToArray();

        Assert.DoesNotContain(assemblyName, friends);
    }

#if NETFRAMEWORK
    [TestMethod]
    public void PlatformPolyfills_DoNotExportRetiredCompatibilityHelpers()
    {
        Type polyfill = typeof(ITestApplicationBuilder).Assembly.GetType("Polyfills.Polyfill", throwOnError: true)!;
        string[] exportedMethods = polyfill.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => method.Name)
            .ToArray();

        foreach (string retiredMethod in new[] { "Deconstruct", "Contains", "AppendJoin", "CopyToAsync", "SaveAsync" })
        {
            Assert.DoesNotContain(retiredMethod, exportedMethods);
        }

        Assert.IsNotNull(polyfill.GetMethod(nameof(IProcess.WaitForExitAsync), [typeof(Process), typeof(CancellationToken)]));
    }
#endif
}
