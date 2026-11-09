// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace MSTest.Acceptance.IntegrationTests;

public sealed partial class AssemblyFixtureProviderDiscoveryTests
{
    [TestMethod]
    public async Task CustomLoadContextUsesItsProviderInsteadOfAnUnmarkedSameIdentityFile()
    {
        using TestAsset asset = await CreateAssetAsync(TargetFrameworks.NetCurrent, "none", "ProviderLibrary", "ProviderLibrary.SharedFixtures", """
        System.Runtime.Loader.AssemblyLoadContext context = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(DiscoveryTests).Assembly)!;
        Assert.AreNotSame(System.Runtime.Loader.AssemblyLoadContext.Default, context);
        Assembly provider = context.Assemblies.Single(assembly => assembly.GetName().Name == "ProviderLibrary");
        Assert.IsTrue((bool)provider.GetType("ProviderLibrary.SharedFixtures")!.GetField("Initialized")!.GetValue(null)!);
        Assembly unmarked = System.Runtime.Loader.AssemblyLoadContext.Default.Assemblies.Single(assembly => assembly.GetName().Name == "ProviderLibrary");
        Assert.IsFalse((bool)unmarked.GetType("ProviderLibrary.SharedFixtures")!.GetField("Initialized")!.GetValue(null)!);
        Assert.AreEqual(unmarked.FullName, provider.FullName, "The decoy intentionally has the same assembly identity.");
        Assert.AreNotEqual(unmarked.ManifestModule.ModuleVersionId, provider.ManifestModule.ModuleVersionId);
        """,
            libraryMarker: false,
            extraSource: (ActualProviderSource + DriverSource)
                .PatchCodeWithReplace("$ActualVersion$", "1.0.0.0")
                .PatchCodeWithReplace("$CustomContext$", "true"));

        await BuildConsumerAsync(asset, "Driver", "DiscoveryDriver");
        await RunConsumerAsync(asset, TargetFrameworks.NetCurrent, "MTP", "provider-initialize | test | provider-cleanup",
            assertFallbackTrace: true, hostProjectDirectory: "Driver", hostProjectName: "DiscoveryDriver");
    }

    [TestMethod]
    public async Task LoadedHigherVersionProviderIsNotHiddenByUnmarkedRequestedVersion()
    {
        using TestAsset asset = await CreateAssetAsync(TargetFrameworks.NetCurrent, "none", "ProviderLibrary", "ProviderLibrary.SharedFixtures",
            ProviderAssertions + """

        Assert.AreEqual(new Version(2, 0, 0, 0), provider.GetName().Version);
        Assert.AreEqual(new Version(1, 0, 0, 0), typeof(DiscoveryTests).Assembly.GetReferencedAssemblies()
            .Single(reference => reference.Name == "ProviderLibrary").Version);
        """,
            libraryMarker: false,
            extraSource: (ActualProviderSource + DriverSource)
                .PatchCodeWithReplace("$ActualVersion$", "2.0.0.0")
                .PatchCodeWithReplace("$CustomContext$", "false"));

        await BuildConsumerAsync(asset, "Driver", "DiscoveryDriver");
        await RunConsumerAsync(asset, TargetFrameworks.NetCurrent, "MTP", "provider-initialize | test | provider-cleanup",
            assertFallbackTrace: true, hostProjectDirectory: "Driver", hostProjectName: "DiscoveryDriver");
    }

    [TestMethod]
    public async Task DeferredDefaultResolutionDoesNotHideProviderBehindAnUnmarkedComponentFile()
    {
        using TestAsset asset = await CreateAssetAsync(TargetFrameworks.NetCurrent, "none", "ProviderLibrary", "ProviderLibrary.SharedFixtures",
            ProviderAssertions + """

        Assert.AreEqual(new Version(2, 0, 0, 0), provider.GetName().Version);
        """,
            libraryMarker: false,
            extraSource: (ActualProviderSource + DriverSource)
                .PatchCodeWithReplace("$ActualVersion$", "2.0.0.0")
                .PatchCodeWithReplace("$CustomContext$", "false")
                .PatchCodeWithReplace("$DeferredResolution$", "true"));

        await BuildConsumerAsync(asset, "Driver", "DiscoveryDriver");
        await RunConsumerAsync(asset, TargetFrameworks.NetCurrent, "MTP", "provider-initialize | test | provider-cleanup",
            assertFallbackTrace: true, hostProjectDirectory: "Driver", hostProjectName: "DiscoveryDriver");
    }

    [TestMethod]
    public async Task OpaqueLoadContextRetainsRuntimeFallbackForAnUnmarkedReference()
    {
        using TestAsset asset = await CreateAssetAsync(TargetFrameworks.NetCurrent, "none", "ProviderLibrary", "ProviderLibrary.SharedFixtures", """
        System.Runtime.Loader.AssemblyLoadContext context = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(DiscoveryTests).Assembly)!;
        Assert.AreNotSame(System.Runtime.Loader.AssemblyLoadContext.Default, context);
        Assembly unmarked = context.Assemblies.Single(assembly => assembly.GetName().Name == "ProviderLibrary");
        Assert.IsFalse(unmarked.IsDefined(typeof(AssemblyFixtureProviderAttribute), inherit: false));
        Assert.IsFalse((bool)unmarked.GetType("ProviderLibrary.SharedFixtures")!.GetField("Initialized")!.GetValue(null)!);
        """,
            libraryMarker: false,
            extraSource: (ActualProviderSource + DriverSource)
                .PatchCodeWithReplace("[assembly: AssemblyFixtureProvider(typeof(ProviderLibrary.SharedFixtures))]", string.Empty)
                .PatchCodeWithReplace("$ActualVersion$", "1.0.0.0")
                .PatchCodeWithReplace("$CustomContext$", "true"));

        await BuildConsumerAsync(asset, "Driver", "DiscoveryDriver");
        await RunConsumerAsync(asset, TargetFrameworks.NetCurrent, "MTP", "test",
            assertFallbackTrace: true, hostProjectDirectory: "Driver", hostProjectName: "DiscoveryDriver");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("MTP")]
    [DataRow("VSTest")]
    public async Task FrameworkBindingRedirectDoesNotTreatWrongVersionAsUnmarked(string runner)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Fail("This Windows-only test must not execute on another OS.");
        }

        const string tfm = "net472";
        const string signing = """
    <SignAssembly>true</SignAssembly>
    <AssemblyOriginatorKeyFile>../fixture.snk</AssemblyOriginatorKeyFile>
""";
        using TestAsset asset = await CreateAssetAsync(tfm, "none", "ProviderLibrary", "ProviderLibrary.SharedFixtures",
            ProviderAssertions + """

        Assert.AreEqual(new Version(2, 0, 0, 0), provider.GetName().Version);
        Assert.AreEqual(new Version(1, 0, 0, 0), typeof(DiscoveryTests).Assembly.GetReferencedAssemblies()
            .Single(reference => reference.Name == "ProviderLibrary").Version);
        """,
            libraryMarker: false,
            extraSource: ActualProviderSource.PatchCodeWithReplace("$ActualVersion$", "2.0.0.0"),
            signing: signing, useVSTest: runner == "VSTest");

        using (var key = new RSACryptoServiceProvider(1024) { PersistKeyInCsp = false })
        {
            File.WriteAllBytes(Path.Combine(asset.TargetAssetPath, "fixture.snk"), key.ExportCspBlob(includePrivateParameters: true));
        }

        await BuildConsumerAsync(asset);
        await BuildConsumerAsync(asset, "ActualProvider", "ActualProvider");
        string outputDirectory = Path.Combine(asset.TargetAssetPath, "Tests", "bin", "Release", tfm);
        string providerPath = Path.Combine(asset.TargetAssetPath, "ActualProvider", "bin", "Release", tfm, "ProviderLibrary.dll");
        string redirectedDirectory = Path.Combine(outputDirectory, "redirected");
        Directory.CreateDirectory(redirectedDirectory);
        string redirectedPath = Path.Combine(redirectedDirectory, "ProviderLibrary.dll");
        File.Copy(providerPath, redirectedPath);
        string configPath = Path.Combine(outputDirectory, ProjectName + ".exe.config");
        var config = XDocument.Load(configPath);
        XElement runtime = config.Root!.Element("runtime")!;
        XNamespace binding = "urn:schemas-microsoft-com:asm.v1";
        XElement? assemblies = runtime.Element(binding + "assemblyBinding");
        if (assemblies is null)
        {
            assemblies = new XElement(binding + "assemblyBinding");
            runtime.Add(assemblies);
        }

        byte[] token = AssemblyName.GetAssemblyName(providerPath).GetPublicKeyToken()!;
        assemblies.Add(new XElement(
            binding + "dependentAssembly",
            new XElement(
                binding + "assemblyIdentity",
                new XAttribute("name", "ProviderLibrary"),
                new XAttribute("publicKeyToken", Convert.ToHexString(token).ToLowerInvariant()),
                new XAttribute("culture", "neutral")),
            new XElement(binding + "bindingRedirect", new XAttribute("oldVersion", "0.0.0.0-2.0.0.0"), new XAttribute("newVersion", "2.0.0.0")),
            new XElement(binding + "codeBase", new XAttribute("version", "2.0.0.0"), new XAttribute("href", new Uri(redirectedPath).AbsoluteUri))));
        config.Save(configPath);

        await RunConsumerAsync(asset, tfm, runner, "provider-initialize | test | provider-cleanup",
            disableAppDomain: runner != "VSTest", assertFallbackTrace: true);
    }

    private const string ActualProviderSource = """

#file ActualProvider/ActualProvider.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <AssemblyName>ProviderLibrary</AssemblyName>
    <AssemblyVersion>$ActualVersion$</AssemblyVersion>
    <LangVersion>preview</LangVersion>
$Signing$
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <Compile Include="../ProviderLibrary/SharedFixtures.cs" Link="SharedFixtures.cs" />
  </ItemGroup>
</Project>

#file ActualProvider/Marker.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
[assembly: AssemblyFixtureProvider(typeof(ProviderLibrary.SharedFixtures))]
""";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task FrameworkLoadFileDecoyDoesNotHideTheApplicationBaseProvider()
    {
        const string tfm = "net472";
        using TestAsset asset = await CreateAssetAsync(tfm, "none", "ProviderLibrary", "ProviderLibrary.SharedFixtures", """
        Assembly actual = Assembly.Load("ProviderLibrary");
        Assembly decoy = AppDomain.CurrentDomain.GetAssemblies().Single(assembly =>
            assembly.GetName().Name == "ProviderLibrary" && !ReferenceEquals(assembly, actual));
        Assert.AreEqual(actual.FullName, decoy.FullName);
        Assert.AreNotSame(actual, decoy);
        Assert.IsTrue(actual.IsDefined(typeof(AssemblyFixtureProviderAttribute), inherit: false));
        Assert.IsFalse(decoy.IsDefined(typeof(AssemblyFixtureProviderAttribute), inherit: false));
        Assert.IsTrue((bool)actual.GetType("ProviderLibrary.SharedFixtures")!.GetField("Initialized")!.GetValue(null)!);
        """, extraSource: FrameworkDriverSource);

        await BuildConsumerAsync(asset, "FrameworkDriver", "FrameworkDiscoveryDriver");
        await RunConsumerAsync(asset, tfm, "MTP", "provider-initialize | test | provider-cleanup",
            assertFallbackTrace: true, hostProjectDirectory: "FrameworkDriver", hostProjectName: "FrameworkDiscoveryDriver");
    }

    private const string FrameworkDriverSource = """

#file Decoy/Decoy.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <AssemblyName>ProviderLibrary</AssemblyName>
  </PropertyGroup>
</Project>

#file Decoy/Marker.cs
namespace ProviderLibrary
{
    public sealed class DecoyMarker { }
}

#file FrameworkDriver/FrameworkDiscoveryDriver.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Tests/FixtureConsumer.csproj" ReferenceOutputAssembly="false" />
    <ProjectReference Include="../Decoy/Decoy.csproj" ReferenceOutputAssembly="false" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <None Include="../Tests/bin/Release/$TargetFramework$/FixtureConsumer.exe"
          Link="FixtureConsumer.exe" CopyToOutputDirectory="PreserveNewest" />
    <None Include="../Tests/bin/Release/$TargetFramework$/ProviderLibrary.dll"
          Link="ProviderLibrary.dll" CopyToOutputDirectory="PreserveNewest" />
    <None Include="../Decoy/bin/Release/$TargetFramework$/ProviderLibrary.dll"
          Link="decoy/ProviderLibrary.dll" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>

#file FrameworkDriver/Program.cs
using System;
using System.IO;
using System.Reflection;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

Assembly decoy = Assembly.LoadFile(Path.Combine(AppContext.BaseDirectory, "decoy", "ProviderLibrary.dll"));
if (decoy.IsDefined(typeof(AssemblyFixtureProviderAttribute), false))
{
    throw new InvalidOperationException("The LoadFile image must be unmarked.");
}

Assembly consumer = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "FixtureConsumer.exe"));
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { consumer });
builder.AddTrxReportProvider();
using ITestApplication application = await builder.BuildAsync();
return await application.RunAsync();
""";

    private const string DriverSource = """

#file Driver/DiscoveryDriver.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Tests/FixtureConsumer.csproj" ReferenceOutputAssembly="false" />
    <ProjectReference Include="../ActualProvider/ActualProvider.csproj" ReferenceOutputAssembly="false" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <None Include="../ActualProvider/bin/Release/$TargetFramework$/ProviderLibrary.dll"
          Link="actual/ProviderLibrary.dll" CopyToOutputDirectory="PreserveNewest" />
    <None Include="../Tests/bin/Release/$TargetFramework$/FixtureConsumer.dll"
          Link="consumer/FixtureConsumer.dll" CopyToOutputDirectory="PreserveNewest" />
    <None Include="../Tests/bin/Release/$TargetFramework$/FixtureConsumer.deps.json"
          Link="consumer/FixtureConsumer.deps.json" CopyToOutputDirectory="PreserveNewest" />
    <None Include="../Tests/bin/Release/$TargetFramework$/ProviderLibrary.dll"
          Link="consumer/ProviderLibrary.dll" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>

#file Driver/Program.cs
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

bool customContext = $CustomContext$;
bool deferredResolution = $DeferredResolution$;
string consumerPath = Path.Combine(AppContext.BaseDirectory, "consumer", "FixtureConsumer.dll");
string actualProviderPath = Path.Combine(AppContext.BaseDirectory, "actual", "ProviderLibrary.dll");
Assembly consumer;
if (customContext)
{
    AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "consumer", "ProviderLibrary.dll"));
    var context = new FixtureLoadContext(actualProviderPath);
    consumer = context.LoadFromAssemblyPath(consumerPath);
}
else
{
    if (deferredResolution)
    {
        if (AssemblyLoadContext.Default.Assemblies.Any(assembly => assembly.GetName().Name == "ProviderLibrary"))
        {
            throw new InvalidOperationException("No provider candidate may be preloaded.");
        }

        string? componentPath = new AssemblyDependencyResolver(consumerPath).ResolveAssemblyToPath(
            new AssemblyName("ProviderLibrary, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));
        if (componentPath is null || AssemblyName.GetAssemblyName(componentPath).Version != new Version(1, 0, 0, 0))
        {
            throw new InvalidOperationException("The component resolver must find the unmarked v1 image.");
        }

        AssemblyLoadContext.Default.Resolving += (_, name) => name.Name == "ProviderLibrary"
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(actualProviderPath)
            : null;
    }
    else
    {
        AssemblyLoadContext.Default.LoadFromAssemblyPath(actualProviderPath);
    }

    consumer = AssemblyLoadContext.Default.LoadFromAssemblyPath(consumerPath);
}

using IDisposable? reflection = customContext
    ? AssemblyLoadContext.GetLoadContext(consumer)!.EnterContextualReflection()
    : null;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { consumer });
builder.AddTrxReportProvider();
using ITestApplication application = await builder.BuildAsync();
return await application.RunAsync();

sealed class FixtureLoadContext(string providerPath) : AssemblyLoadContext
{
    protected override Assembly? Load(AssemblyName name)
        => name.Name == "ProviderLibrary" ? LoadFromAssemblyPath(providerPath) : null;
}
""";
}
