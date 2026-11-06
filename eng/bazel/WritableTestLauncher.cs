// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

if (args.Length < 3)
{
    throw new ArgumentException("Expected the declared SDK directory, SDK version, and published application.");
}

string sdk = Path.GetFullPath(args[0]);
string application = Path.GetFullPath(args[2]);
string source = Path.GetDirectoryName(application)!;
string temporary = Environment.GetEnvironmentVariable("TEST_TMPDIR")
    ?? throw new InvalidOperationException("The writable test launcher requires Bazel's isolated TEST_TMPDIR.");
string root = Path.Combine(temporary, "runtime-" + Guid.NewGuid().ToString("N"));
string output = Path.Combine(root, "app");
string sdkLink = Path.Combine(root, ".dotnet");
Directory.CreateDirectory(output);
try
{
    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        string destination = Path.Combine(output, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(file, destination);
        File.SetAttributes(destination, File.GetAttributes(destination) & ~FileAttributes.ReadOnly);
    }

    Directory.CreateSymbolicLink(sdkLink, sdk);
    File.WriteAllText(Path.Combine(root, "global.json"), JsonSerializer.Serialize(new
    {
        sdk = new { version = args[1], rollForward = "disable" },
    }));

    string dotnet = Path.Combine(sdk, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
    var startInfo = new ProcessStartInfo(dotnet)
    {
        UseShellExecute = false,
        WorkingDirectory = output,
    };
    string assemblyName = OperatingSystem.IsWindows()
        ? Path.GetFileNameWithoutExtension(application)
        : Path.GetFileName(application);
    startInfo.ArgumentList.Add(Path.Combine(output, assemblyName + ".dll"));
    foreach (string argument in args.Skip(3))
    {
        startInfo.ArgumentList.Add(argument);
    }

    startInfo.Environment["DOTNET_ROOT"] = sdk;
    startInfo.Environment["DOTNET_ROOT_X64"] = sdk;
    startInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
    using Process process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("The staged test host did not start.");
    try
    {
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
    finally
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }
}
finally
{
    // Remove the link itself before recursively deleting only this invocation's copy.
    if (Directory.Exists(sdkLink))
    {
        Directory.Delete(sdkLink);
    }

    Directory.Delete(root, recursive: true);
}
