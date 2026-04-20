// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ExtensionVersionTests
{
    [TestMethod]
    public void AzureDevOpsCommandLineProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new AzureDevOpsCommandLineProvider();
        AssertVersionMatchesAssembly(provider.Version, typeof(AzureDevOpsCommandLineProvider).Assembly);
    }

    [TestMethod]
    public void CrashDumpCommandLineProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new CrashDumpCommandLineProvider();
        AssertVersionMatchesAssembly(provider.Version, typeof(CrashDumpCommandLineProvider).Assembly);
    }

    [TestMethod]
    public void HangDumpCommandLineProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new HangDumpCommandLineProvider();
        AssertVersionMatchesAssembly(provider.Version, typeof(HangDumpCommandLineProvider).Assembly);
    }

    [TestMethod]
    public void RetryCommandLineOptionsProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new RetryCommandLineOptionsProvider();
        AssertVersionMatchesAssembly(provider.Version, typeof(RetryCommandLineOptionsProvider).Assembly);
    }

    [TestMethod]
    public void TrxReportGeneratorCommandLine_UsesItsOwnAssemblyVersion()
    {
        var provider = new TrxReportGeneratorCommandLine();
        AssertVersionMatchesAssembly(provider.Version, typeof(TrxReportGeneratorCommandLine).Assembly);
    }

    private static void AssertVersionMatchesAssembly(string reportedVersion, Assembly extensionAssembly)
    {
        Assert.IsFalse(string.IsNullOrEmpty(reportedVersion), "Reported version should not be null or empty.");

        string expectedVersion = extensionAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? extensionAssembly.GetName().Version?.ToString()
            ?? string.Empty;

        Assert.AreEqual(expectedVersion, reportedVersion);
    }
}
