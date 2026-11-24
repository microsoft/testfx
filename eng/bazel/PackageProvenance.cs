// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace TestFx.BazelValidation;

internal static class PackageProvenance
{
    [ModuleInitializer]
    internal static void Verify()
    {
        string expectationsPath = Environment.GetEnvironmentVariable("TESTFX_BAZEL_PACKAGE_EXPECTATIONS")
            ?? throw new InvalidOperationException("Package provenance expectations were not supplied.");
        Dictionary<string, string> expectations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(expectationsPath))
            ?? throw new InvalidDataException("Package provenance expectations are empty.");
        foreach ((string name, string expectedHash) in expectations)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(name));
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
            if (hash != expectedHash || !assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(attribute => attribute.Key == "TestFx.BuildSystem" && attribute.Value == "Bazel"))
            {
                throw new InvalidDataException($"The loaded {name} is not the expected native Bazel assembly: {assembly.Location}");
            }

            Console.WriteLine($"BAZEL_PACKAGE_ASSEMBLY: {name}; {hash}; {assembly.Location}");
        }

        Console.WriteLine($"BAZEL_PACKAGE_RUNTIME: {RuntimeInformation.FrameworkDescription}");
    }
}
