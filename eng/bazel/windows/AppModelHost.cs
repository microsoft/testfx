// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;

using TestingPlatformExplorer.TestingFramework;

namespace TestFx.BazelValidation;

internal static class AppModelHost
{
    public static async Task<int> Main(string[] args)
    {
        WriteProvenance();
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddTestingFramework(() => [Assembly.GetExecutingAssembly()]);
        builder.CommandLine.AddProvider(() => new TestingFrameworkCommandLineOptions());
        builder.AddTrxReportProvider();
        builder.AddPackagedAppDeployment();
        builder.AddRetryProvider();
        using ITestApplication application = await builder.BuildAsync();
        return await application.RunAsync();
    }

    private static void WriteProvenance()
    {
        uint length = 0;
        int result = GetCurrentPackageFullName(ref length, null);
        const int NoPackage = 15700;
        const int InsufficientBuffer = 122;
        if (result is not (0 or NoPackage or InsufficientBuffer))
        {
            throw new Win32Exception(result);
        }

        Windows.ApplicationModel.Package? package = result == NoPackage ? null : Windows.ApplicationModel.Package.Current;
        var report = new
        {
            ProcessId = Environment.ProcessId,
            BaseDirectory = AppContext.BaseDirectory,
            PackageName = package?.Id.Name,
            PackageFullName = package?.Id.FullName,
            PackageFamilyName = package?.Id.FamilyName,
            InstalledPath = package?.InstalledPath,
            Assemblies = new[] { typeof(TestApplication).Assembly, typeof(PackagedAppExtensions).Assembly, Assembly.GetExecutingAssembly() }
                .Select(assembly => new
                {
                    Name = assembly.GetName().Name,
                    Path = assembly.Location,
                    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
                    PublicKeyToken = Convert.ToHexString(assembly.GetName().GetPublicKeyToken() ?? []).ToLowerInvariant(),
                }),
        };
        string json = JsonSerializer.Serialize(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "native-app-model.json"), json);
        Console.WriteLine("BAZEL_APPMODEL_PROVENANCE: " + json);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}
