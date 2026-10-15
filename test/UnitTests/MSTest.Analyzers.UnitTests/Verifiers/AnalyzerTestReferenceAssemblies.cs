// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

namespace MSTest.Analyzers.Test;

internal static class AnalyzerTestReferenceAssemblies
{
    private static readonly string NuGetConfigFilePath = Path.Combine(RootFinder.Find(), "NuGet.config");

    public static ReferenceAssemblies Default { get; } = CreateDefault();

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
