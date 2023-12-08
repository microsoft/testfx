// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

namespace Microsoft.Testing.TestInfrastructure;

public static class TestHostFinderExtensions
{
    public static TestHostFinder FindTestHost(
        this string rootFolder,
        string testHostModuleNameWithoutExtension,
        string tfm,
        string rid = "",
        Verb verb = Verb.Build,
        BuildConfiguration buildConfiguration = BuildConfiguration.Release)
    {
        string moduleName = $"{testHostModuleNameWithoutExtension}{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty)}";
        string? expectedRootPath = Path.Combine(rootFolder, "bin", buildConfiguration.ToString(), tfm);
        string[] executables = Directory.GetFiles(expectedRootPath, moduleName, SearchOption.AllDirectories);
        string? expectedPath = executables.SingleOrDefault(p => p.Contains(rid) && p.Contains(verb == Verb.Publish ? "publish" : string.Empty));

        return expectedPath is null
            ? throw new InvalidOperationException($"Host '{moduleName}' not found in '{expectedRootPath}'")
            : new TestHostFinder(expectedPath, testHostModuleNameWithoutExtension);
    }

    public static TestHostFinder FindTestHost(this string rootFolder, string testHostModuleNameWithoutExtension)
    {
        string moduleName = $"{testHostModuleNameWithoutExtension}{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty)}";

        // Try to find module in current dir, useful when we use to close
        return File.Exists(Path.Combine(rootFolder, moduleName))
            ? new TestHostFinder(Path.Combine(rootFolder, moduleName), testHostModuleNameWithoutExtension)
            : throw new FileNotFoundException("Test host file not found", Path.Combine(rootFolder, moduleName));
    }

    public static async Task<TestHostResult> ExecWithTestHostAsync(this string command, TestHostFinder testHostFinder, Dictionary<string, string>? environmentVariables = null)
    {
        return await testHostFinder.RunAsync(command, environmentVariables);
    }
}
