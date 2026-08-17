// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class PackagedAppActivationArgumentsTests : AcceptanceTestBase<PackagedAppActivationArgumentsTests.TestAssetFixture>
{
    private const string AssetName = "PackagedAppActivationArgumentsTest";

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "LaunchActivatedEventArgs is a Windows application activation contract.")]
    public async Task AppContainerOnLaunched_RestoresArgumentsBeforeMtpParsesThem(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        string markerPath = Path.Combine(testHost.DirectoryName, $"activation-arguments-{Guid.NewGuid():N}.txt");
        string[] expectedArguments = ["--help"];

        try
        {
            TestHostResult result = await testHost.ExecuteAsync(
                environmentVariables: new Dictionary<string, string?>
                {
                    ["PACKAGEDAPP_ACTIVATION_ARGUMENTS"] = CreateInlineActivationArguments(expectedArguments),
                    ["PACKAGEDAPP_ACTIVATION_MARKER"] = markerPath,
                },
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(ExitCode.Success);
            Assert.IsTrue(File.Exists(markerPath), "The activation-shaped host must record arguments only after MTP accepts them.");
            string[] actualArguments = File.ReadAllLines(markerPath);
            Assert.HasCount(expectedArguments.Length, actualArguments);
            for (int i = 0; i < expectedArguments.Length; i++)
            {
                Assert.AreEqual(expectedArguments[i], actualArguments[i], $"Argument {i} differs.");
            }
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    private static string CreateInlineActivationArguments(IReadOnlyList<string> arguments)
    {
        int byteCount = sizeof(int) + arguments.Sum(static argument => sizeof(int) + (argument.Length * sizeof(char)));
        byte[] payload = new byte[byteCount];
        WriteInt32(payload, arguments.Count);
        int offset = sizeof(int);
        foreach (string argument in arguments)
        {
            WriteInt32(payload.AsSpan(offset), argument.Length);
            offset += sizeof(int);
            foreach (char value in argument)
            {
                payload[offset++] = (byte)value;
                payload[offset++] = (byte)(value >> 8);
            }
        }

        return "mtp:v1:inline:" + Convert.ToBase64String(payload);
    }

    private static void WriteInt32(Span<byte> destination, int value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
        destination[3] = (byte)(value >> 24);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
            #file PackagedAppActivationArgumentsTest.csproj

            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <UseAppHost>true</UseAppHost>
                <Nullable>enable</Nullable>
                <LangVersion>preview</LangVersion>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
                <PackageReference Include="Microsoft.Testing.Extensions.PackagedApp" Version="$MicrosoftTestingExtensionsPackagedAppVersion$" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using System;
            using System.IO;
            using System.Threading.Tasks;
            using Microsoft.Testing.Extensions;
            using Microsoft.Testing.Platform.Builder;

            public static class Program
            {
                public static Task<int> Main()
                {
                    string activationArguments = Environment.GetEnvironmentVariable("PACKAGEDAPP_ACTIVATION_ARGUMENTS")
                        ?? throw new InvalidOperationException("Missing activation arguments.");
                    return new ActivationApplication().OnLaunched(new LaunchActivatedEventArgs(activationArguments));
                }
            }

            // Mirrors the Windows.ApplicationModel.Activation shape without requiring package registration.
            // A real UWP/WinUI AppContainer override passes args.Arguments to the same package API.
            public sealed class ActivationApplication
            {
                public async Task<int> OnLaunched(LaunchActivatedEventArgs args)
                {
                    string[] cliArgs = PackagedAppExtensions.GetTestApplicationArguments(args.Arguments);

                    // CreateBuilderAsync performs MTP's real command-line parse. Write the marker only after
                    // it succeeds, proving the restored array was available at the required bootstrap point.
                    _ = await TestApplication.CreateBuilderAsync(cliArgs);
                    File.WriteAllLines(
                        Environment.GetEnvironmentVariable("PACKAGEDAPP_ACTIVATION_MARKER")
                            ?? throw new InvalidOperationException("Missing activation marker path."),
                        cliArgs);
                    return 0;
                }
            }

            public sealed record LaunchActivatedEventArgs(string Arguments);
            """;

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources
                .PatchTargetFrameworks(TargetFrameworks.Net)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsPackagedAppVersion$", MicrosoftTestingExtensionsPackagedAppVersion));
    }

    public TestContext TestContext { get; set; }
}
