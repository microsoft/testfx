// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace TestFx.BazelValidation;

[TestClass]
public sealed class NativePackageTests
{
    [TestMethod]
    public void NativeAssembliesAreLoadedFromConsumer()
    {
        using JsonDocument expectations = ReadExpectations();
        JsonElement assemblies = expectations.RootElement.GetProperty("Assemblies");
        string[] names =
        [
            "MSTest.TestFramework",
            "MSTest.TestFramework.Extensions",
            "MSTest.TestAdapter",
            "MSTestAdapter.PlatformServices",
            "Microsoft.Testing.Platform",
            "Microsoft.Testing.Extensions.MSBuild",
            "Microsoft.Testing.Extensions.Telemetry",
            "Microsoft.Testing.Extensions.TrxReport",
            "Microsoft.Testing.Extensions.TrxReport.Abstractions",
        ];
        CollectionAssert.AreEquivalent(names, assemblies.EnumerateObject().Select(property => property.Name).ToArray());

        var loaded = new List<object>();
        foreach (string name in names)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(name));
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
            string publicKeyToken = Convert.ToHexString(assembly.GetName().GetPublicKeyToken() ?? []);
            Assert.AreEqual(name + ".dll", Path.GetRelativePath(AppContext.BaseDirectory, assembly.Location));
            Assert.AreEqual(assemblies.GetProperty(name).GetString(), hash, $"Unexpected native payload for {name}.");
            Assert.AreEqual("B03F5F7F11D50A3A", publicKeyToken, name);
            Assert.IsTrue(assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(attribute => attribute.Key == "TestFx.BuildSystem" && attribute.Value == "Bazel"), name);
            loaded.Add(new { Name = name, Sha256 = hash, PublicKeyToken = publicKeyToken, assembly.Location });
        }

        string evidence = Environment.GetEnvironmentVariable("TESTFX_BAZEL_CONSUMER_EVIDENCE")
            ?? throw new InvalidOperationException("The consumer evidence path was not supplied.");
        File.WriteAllText(evidence, JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            CoreLib = typeof(object).Assembly.Location,
            Framework = expectations.RootElement.GetProperty("Framework").GetString(),
            Assemblies = loaded,
        }));
    }

    [TestMethod]
    public void KoreanSatellitesAreCopiedAndLoadable()
    {
        using JsonDocument expectations = ReadExpectations();
        JsonElement resources = expectations.RootElement.GetProperty("Resources");
        Assert.AreEqual(3, resources.GetArrayLength());
        CultureInfo culture = CultureInfo.GetCultureInfo("ko");
        foreach (JsonElement resource in resources.EnumerateArray())
        {
            string name = resource.GetProperty("Assembly").GetString()!;
            string satellitePath = Path.Combine(AppContext.BaseDirectory, "ko", name + ".resources.dll");
            Assert.IsTrue(File.Exists(satellitePath), $"Missing native Korean satellite: {name}.");
            Assembly assembly = Assembly.Load(new AssemblyName(name));
            Assembly satellite = assembly.GetSatelliteAssembly(culture);
            Assert.AreEqual(Path.GetFullPath(satellitePath), satellite.Location);
            Assert.AreEqual("B03F5F7F11D50A3A", Convert.ToHexString(satellite.GetName().GetPublicKeyToken() ?? []), name);
            Assert.AreEqual(resource.GetProperty("Sha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(satellite.Location))));
            var manager = new ResourceManager(resource.GetProperty("BaseName").GetString()!, assembly);
            Assert.AreEqual(resource.GetProperty("Value").GetString(), manager.GetString(resource.GetProperty("Key").GetString()!, culture));
        }
    }

    [TestMethod]
    public void PackagedTargetsProvideExtensionsAndParallelization()
    {
        Assert.AreEqual("MSTest.TestFramework.Extensions", typeof(TestContext).Assembly.GetName().Name);
        ParallelizeAttribute? parallelize = typeof(NativePackageTests).Assembly.GetCustomAttribute<ParallelizeAttribute>();
        Assert.IsNotNull(parallelize);
        Assert.AreEqual(ExecutionScope.MethodLevel, parallelize.Scope);
        Assert.AreEqual(2, parallelize.Workers);
        using JsonDocument configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MSTestPackageConsumer.testconfig.json")));
        Assert.AreEqual(1, configuration.RootElement.EnumerateObject().Count());
        JsonElement defaults = configuration.RootElement.GetProperty("commandLineOptionDefaults");
        Assert.AreEqual(1, defaults.EnumerateObject().Count());
        Assert.AreEqual("1", defaults.GetProperty("minimum-expected-tests").GetString());
    }

    [TestMethod]
    public void IntentionalFailure()
        => Assert.Fail("BAZEL_INTENTIONAL_FAILURE");

    private static JsonDocument ReadExpectations()
    {
        string path = Environment.GetEnvironmentVariable("TESTFX_BAZEL_MSTEST_EXPECTATIONS")
            ?? throw new InvalidOperationException("The native package expectations were not supplied.");

        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
