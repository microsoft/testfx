// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

namespace MSTest.Analyzers.Test;

internal static class AnalyzerTestReferenceAssemblies
{
    private static readonly string NuGetConfigFilePath = Path.Combine(RootFinder.Find(), "NuGet.config");

    public static ReferenceAssemblies Default { get; } = CreateDefault();

    // v4's netstandard2.0 package assets include both framework and extensions (for example DeploymentItemAttribute).
    // Its newer targets put extensions in buildTransitive, which the analyzer reference resolver does not import.
    public static ReferenceAssemblies MSTestV4 { get; } =
        ReferenceAssemblies.NetStandard.NetStandard20.WithNuGetConfigFilePath(NuGetConfigFilePath)
            .AddPackages([new PackageIdentity("MSTest.TestFramework", "4.0.2")]);

    // Migration inputs and intermediate fixes need both legacy assertions and their modern replacements.
    // Keep this separate from MSTestV4 so tests of older APIs retain their original reference contract.
    public static ReferenceAssemblies MSTestWithLegacyAssertions { get; } =
        ReferenceAssemblies.NetStandard.NetStandard20.WithNuGetConfigFilePath(NuGetConfigFilePath)
            .AddPackages([new PackageIdentity("MSTest.TestFramework", "4.5.2")]);

    public static ReferenceAssemblies Net100OrLater { get; } =
#if NET11_0
        Default;
#else
        ReferenceAssemblies.Net.Net100.WithNuGetConfigFilePath(NuGetConfigFilePath);
#endif

    private static ReferenceAssemblies CreateDefault()
    {
#if NET462
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.NetFramework.Net462.Default;
#elif NET11_0
        // The pinned testing library has no Net110. Use the same pack version as the repository's net11 build.
        string version = typeof(AnalyzerTestReferenceAssemblies).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "Net11RuntimeVersion").Value!;
        var referenceAssemblies = new ReferenceAssemblies(
            "net11.0",
            new PackageIdentity("Microsoft.NETCore.App.Ref", version),
            Path.Combine("ref", "net11.0"));
#elif NET10_0
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net100;
#elif NET9_0
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net90;
#else
        ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net80;
#endif
        return referenceAssemblies.WithNuGetConfigFilePath(NuGetConfigFilePath);
    }
}
