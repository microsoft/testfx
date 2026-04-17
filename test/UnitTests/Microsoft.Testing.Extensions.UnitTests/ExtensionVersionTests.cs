// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.Reporting;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ExtensionVersionTests
{
    [TestMethod]
    public void AzureDevOpsCommandLineProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new AzureDevOpsCommandLineProvider();
        Assert.AreEqual(GetExpectedVersion(typeof(AzureDevOpsCommandLineProvider), "Microsoft.Testing.Extensions.Reporting.ExtensionVersion"), provider.Version);
    }

    [TestMethod]
    public void CrashDumpCommandLineProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new CrashDumpCommandLineProvider();
        Assert.AreEqual(GetExpectedVersion(typeof(CrashDumpCommandLineProvider), "Microsoft.Testing.Extensions.Diagnostics.ExtensionVersion"), provider.Version);
    }

    [TestMethod]
    public void HangDumpCommandLineProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new HangDumpCommandLineProvider();
        Assert.AreEqual(GetExpectedVersion(typeof(HangDumpCommandLineProvider), "Microsoft.Testing.Extensions.Diagnostics.ExtensionVersion"), provider.Version);
    }

    [TestMethod]
    public void RetryCommandLineOptionsProvider_UsesItsOwnAssemblyVersion()
    {
        var provider = new RetryCommandLineOptionsProvider();
        Assert.AreEqual(GetExpectedVersion(typeof(RetryCommandLineOptionsProvider), "Microsoft.Testing.Extensions.Policy.ExtensionVersion"), provider.Version);
    }

    private static string GetExpectedVersion(Type extensionType, string extensionVersionTypeName)
    {
        Type? extensionVersionType = extensionType.Assembly.GetType(extensionVersionTypeName);
        Assert.IsNotNull(extensionVersionType);

        FieldInfo? defaultSemVer = extensionVersionType.GetField("DefaultSemVer", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(defaultSemVer);

        return (string?)defaultSemVer.GetValue(null) ?? string.Empty;
    }
}
