// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.TestInfrastructure;

public class TempDirectory : IDisposable
{
    private readonly bool _cleanup;
    private readonly string _baseDirectory;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TempDirectory"/> class.
    /// </summary>
    public TempDirectory(string? subDirectory = null, bool arcadeConvention = true, bool cleanup = true)
    {
        if (Environment.GetEnvironmentVariable("Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup") == "0")
        {
            cleanup = false;
        }

        (_baseDirectory, Path) = CreateUniqueDirectory(subDirectory, arcadeConvention);
        _cleanup = cleanup;
    }

    ~TempDirectory() => Clean();

    public string Path { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing && _cleanup)
        {
            Clean();
        }

        _isDisposed = true;
    }

    public DirectoryInfo CreateDirectory(string dir)
        => Directory.CreateDirectory(System.IO.Path.Combine(Path, dir));

    public static async Task WriteFileAsync(string targetDirectory, string fileName, string fileContents)
    {
        string finalFile = System.IO.Path.Combine(targetDirectory, fileName);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(finalFile)!);
        using var fs = new FileStream(finalFile, FileMode.CreateNew);
        using var stream = new StreamWriter(fs);
        await stream.WriteLineAsync(fileContents);
    }

    public async Task CopyDirectoryAsync(string sourceDirectory, string targetDirectory, bool retainAttributes = false)
        => await CopyDirectoryAsync(new DirectoryInfo(sourceDirectory), new DirectoryInfo(targetDirectory), retainAttributes);

    public static async Task CopyDirectoryAsync(DirectoryInfo source, DirectoryInfo target, bool retainAttributes = false)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in source.GetFiles())
        {
            if (retainAttributes)
            {
                File.Copy(fi.FullName, System.IO.Path.Combine(target.FullName, fi.Name));
            }
            else
            {
                using FileStream fileStream = File.OpenRead(fi.FullName);
                using var destinationStream = new FileStream(
                    System.IO.Path.Combine(target.FullName, fi.Name),
                    FileMode.CreateNew);
                await fileStream.CopyToAsync(destinationStream);
            }
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            await CopyDirectoryAsync(diSourceSubDir, nextTargetSubDir);
        }
    }

    public void CopyDirectory(string sourceDirectory, string targetDirectory)
        => CopyDirectory(new DirectoryInfo(sourceDirectory), new DirectoryInfo(targetDirectory));

    public static void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in source.GetFiles())
        {
            fi.CopyTo(System.IO.Path.Combine(target.FullName, fi.Name), true);
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            CopyDirectory(diSourceSubDir, nextTargetSubDir);
        }
    }

    /// <summary>
    /// Copy given files into the TempDirectory and return the updated paths that are pointing to TempDirectory.
    /// </summary>
    public string[] CopyFile(params string[] filePaths)
    {
        var paths = new List<string>(filePaths.Length);
        foreach (string filePath in filePaths)
        {
            string destination = System.IO.Path.Combine(Path, System.IO.Path.GetFileName(filePath));
            File.Copy(filePath, destination);
            paths.Add(destination);
        }

        return paths.ToArray();
    }

    /// <summary>
    /// Copy given file into TempDirectory and return the updated path.
    /// </summary>
    public string CopyFile(string filePath)
    {
        string destination = System.IO.Path.Combine(Path, System.IO.Path.GetFileName(filePath));
        File.Copy(filePath, destination);
        return destination;
    }

    /// <summary>
    /// Creates an unique temporary directory.
    /// </summary>
    /// <returns>
    /// Path of the created directory.
    /// </returns>
    internal static (string BaseDirectory, string FinalDirectory) CreateUniqueDirectory(string? subDirectory, bool arcadeConvention)
    {
        if (arcadeConvention)
        {
            string currentDirectory = AppContext.BaseDirectory;
            while (System.IO.Path.GetFileName(currentDirectory) != "artifacts" && currentDirectory is not null)
            {
                currentDirectory = System.IO.Path.GetDirectoryName(currentDirectory)!;
            }

            if (currentDirectory is null)
            {
                throw new InvalidOperationException("artifacts folder not found");
            }

            string directoryPath = System.IO.Path.Combine(currentDirectory, "tmp", Constants.BuildConfiguration, "testsuite", RandomId.Next());
            Directory.CreateDirectory(directoryPath);

            string directoryBuildProps = System.IO.Path.Combine(directoryPath, "Directory.Build.props");
            File.WriteAllText(directoryBuildProps, $"""
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
      <RepoRoot>{System.IO.Path.GetDirectoryName(currentDirectory)}/</RepoRoot>
      <!--
        Do not warn about package downgrade. NuGet uses alphabetical sort as ordering so -dev or -ci are considered downgrades of -preview.
        -->
      <NoWarn>NU1605</NoWarn>
      <RunAnalyzers>false</RunAnalyzers>
    </PropertyGroup>
</Project>
""");

            string directoryBuildTarget = System.IO.Path.Combine(directoryPath, "Directory.Build.targets");
            File.WriteAllText(directoryBuildTarget, """
<?xml version="1.0" encoding="utf-8"?>
<Project/>
""");

            string directoryPackagesProps = System.IO.Path.Combine(directoryPath, "Directory.Packages.props");
            File.WriteAllText(directoryPackagesProps, """
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
      <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    </PropertyGroup>
</Project>
""");

            string finalDirectory = directoryPath;
            if (!string.IsNullOrWhiteSpace(subDirectory))
            {
                finalDirectory = System.IO.Path.Combine(directoryPath, subDirectory);
            }

            Directory.CreateDirectory(finalDirectory);

            return (directoryPath, finalDirectory);
        }
        else
        {
            string temp = GetTempPath();
            string directoryPath = System.IO.Path.Combine(temp, "testingplatform", RandomId.Next());
            string finalDirectory = directoryPath;
            if (!string.IsNullOrWhiteSpace(subDirectory))
            {
                finalDirectory = System.IO.Path.Combine(directoryPath, subDirectory);
            }

            Directory.CreateDirectory(finalDirectory);

            return (directoryPath, finalDirectory);
        }
    }

    // AGENT_TEMPDIRECTORY is Azure DevOps variable, which is set to path
    // that is cleaned up after every job. This is preferable to use over
    // just the normal TEMP, because that is not cleaned up for every run.
    //
    // System.IO.Path.GetTempPath is banned from the rest of the code. This is the only
    // place where we are allowed to use it. All other methods should use our GetTempPath (this method).
    private static string GetTempPath()
        => Environment.GetEnvironmentVariable("AGENT_TEMPDIRECTORY")
        ?? System.IO.Path.GetTempPath();

    public void Clean()
    {
        if (!Directory.Exists(_baseDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
        catch
        {
        }
    }

    public void Add(string fileContents)
    {
        List<InlineFile> files = InlineFileParser.ParseFiles(fileContents);
        foreach (InlineFile file in files)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, file.Name), file.Content, Encoding.UTF8);
        }
    }

    public override string ToString() => Path;

    internal sealed class InlineFile(string name, string content)
    {
        public string Name { get; } = name;

        public string Content { get; } = content;
    }

    internal static class InlineFileParser
    {
        internal static List<InlineFile> ParseFiles(string fileContents)
        {
            List<InlineFile> files = new();
            string? name = null;
            bool inFile = false;
            List<string> lines = new();
            foreach (string line in fileContents.Split('\n'))
            {
                if (line.Trim()?.StartsWith("### ", StringComparison.InvariantCulture) ?? false)
                {
                    if (inFile)
                    {
                        files.Add(new InlineFile(name!, string.Join(Environment.NewLine, lines)));
                        inFile = false;
                        name = null;
                        lines.Clear();
                    }

                    inFile = true;
                    name = line.Trim().TrimStart('#').Trim();
                    continue;
                }

                lines.Add(line);
            }

            if (inFile)
            {
                files.Add(new InlineFile(name!, string.Join(Environment.NewLine, lines)));
            }

            return files;
        }
    }
}
