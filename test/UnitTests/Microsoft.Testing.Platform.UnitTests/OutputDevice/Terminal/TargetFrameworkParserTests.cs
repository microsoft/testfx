// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Reflection.Emit;
#endif

using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TargetFrameworkParserTests
{
    // known 2 digit versions
    [DataRow(".NET Framework 4.7.0", "net47")]
    [DataRow(".NET Framework 4.8.0", "net48")]

    // known 3 digit versions
    [DataRow(".NET Framework 4.6.2", "net462")]
    [DataRow(".NET Framework 4.7.1", "net471")]
    [DataRow(".NET Framework 4.7.2", "net472")]
    [DataRow(".NET Framework 4.8.1", "net481")]

    // other
    [DataRow(".NET Framework 4.6.3", "net46")]
    [DataRow(".NET Framework 4.8.9", "net48")]
    [TestMethod]
    public void ParseNETFramework(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));

    [DataRow(".NET Core 3.1.0", "netcoreapp3.1")]
    [TestMethod]
    public void ParseNETCore(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));

    [DataRow(".NET 6.0.0", "net6.0")]
    [DataRow(".NET 8.0.0", "net8.0")]
    [DataRow(".NET 10.0.0", "net10.0")]
    [TestMethod]
    public void ParseNET(string longName, string expectedShortName)
        => Assert.AreEqual(expectedShortName, TargetFrameworkParser.GetShortTargetFramework(longName));

    [TestMethod]
    public void BuildTargetFrameworkMoniker_WithoutPlatform_ReturnsShortTargetFramework()
        => Assert.AreEqual("net8.0", TargetFrameworkParser.BuildTargetFrameworkMoniker("net8.0", null));

    [TestMethod]
    public void BuildTargetFrameworkMoniker_WithEmptyPlatform_ReturnsShortTargetFramework()
        => Assert.AreEqual("net8.0", TargetFrameworkParser.BuildTargetFrameworkMoniker("net8.0", string.Empty));

    [TestMethod]
    public void BuildTargetFrameworkMoniker_WithPlatform_AppendsLowercasedPlatform()
        => Assert.AreEqual("net8.0-windows10.0.18362.0", TargetFrameworkParser.BuildTargetFrameworkMoniker("net8.0", "Windows10.0.18362.0"));

    [TestMethod]
    public void BuildTargetFrameworkMoniker_WithNullShortTargetFramework_ReturnsNull()
        => Assert.IsNull(TargetFrameworkParser.BuildTargetFrameworkMoniker(null, "Windows10.0.18362.0"));

    [TestMethod]
    public void GetTargetPlatformName_WithNullAssembly_ReturnsNull()
        => Assert.IsNull(TargetFrameworkParser.GetTargetPlatformName(null));

#if NET
    [TestMethod]
    public void GetTargetPlatformName_WithTargetPlatformAttribute_ReturnsPlatformName()
    {
        Assembly dynamicAssembly = CreateAssemblyWithTargetPlatform("Windows10.0.18362.0");
        Assert.AreEqual("Windows10.0.18362.0", TargetFrameworkParser.GetTargetPlatformName(dynamicAssembly));
    }

    [TestMethod]
    public void GetTargetPlatformName_WithoutTargetPlatformAttribute_ReturnsNull()
    {
        Assembly dynamicAssembly = CreateAssemblyWithoutTargetPlatform();
        Assert.IsNull(TargetFrameworkParser.GetTargetPlatformName(dynamicAssembly));
    }

    [TestMethod]
    public void GetTargetPlatformName_WithEmptyPlatformValue_ReturnsNull()
    {
        Assembly dynamicAssembly = CreateAssemblyWithTargetPlatform(string.Empty);
        Assert.IsNull(TargetFrameworkParser.GetTargetPlatformName(dynamicAssembly));
    }

    [TestMethod]
    public void GetShortTargetFrameworkIncludingPlatform_WithTargetPlatformAttribute_AppendsLowercasedPlatform()
    {
        Assembly dynamicAssembly = CreateAssemblyWithTargetPlatform("Windows10.0.18362.0");

        // The dynamic assembly carries no TargetFrameworkAttribute, so the parser falls back to
        // RuntimeInformation.FrameworkDescription. The point of this test is to verify the platform
        // discriminator gets appended (and lowercased) on top of whatever the .NET runtime reports.
        string? result = TargetFrameworkParser.GetShortTargetFrameworkIncludingPlatform(dynamicAssembly);

        Assert.IsNotNull(result);
        Assert.EndsWith("-windows10.0.18362.0", result);
    }

    [TestMethod]
    public void GetShortTargetFrameworkIncludingPlatform_WithoutTargetPlatformAttribute_ReturnsShortTargetFrameworkOnly()
    {
        Assembly dynamicAssembly = CreateAssemblyWithoutTargetPlatform();

        string? result = TargetFrameworkParser.GetShortTargetFrameworkIncludingPlatform(dynamicAssembly);

        Assert.IsNotNull(result);
        Assert.DoesNotContain("-", result);
    }

    private static Assembly CreateAssemblyWithTargetPlatform(string platformName)
    {
        var name = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        var builder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);

        ConstructorInfo ctor = typeof(System.Runtime.Versioning.TargetPlatformAttribute).GetConstructor([typeof(string)])!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(ctor, [platformName]));
        return builder;
    }

    private static Assembly CreateAssemblyWithoutTargetPlatform()
    {
        var name = new AssemblyName($"TestAssembly_{Guid.NewGuid():N}");
        return AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
    }
#endif
}
