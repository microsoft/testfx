// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for mocking")]
internal class FileUtility
{
    private readonly AssemblyUtility _assemblyUtility;

    public FileUtility() => _assemblyUtility = new AssemblyUtility();

    public virtual void CreateDirectoryIfNotExists(string directory)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(directory), "directory");

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);    // Creates subdir chain if necessary.
        }
    }

    /// <summary>
    /// Replaces the invalid file/path characters from the parameter file name with '_'.
    /// </summary>
    /// <param name="fileName"> The file Name. </param>
    /// <returns> The fileName devoid of any invalid characters. </returns>
    public static string ReplaceInvalidFileNameCharacters(string fileName)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(fileName), "fileName");

        return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, ch) => current.Replace(ch, '_'));
    }

    /// <summary>
    /// Checks whether directory with specified name exists in the specified directory.
    /// If it exits, adds [1],[2]... to the directory name and checks again.
    /// Returns full directory name (full path) of the iteration when the file does not exist.
    /// </summary>
    /// <param name="parentDirectoryName">The directory where to check.</param>
    /// <param name="originalDirectoryName">The original directory (that we would add [1],[2],.. in the end of if needed) name to check.</param>
    /// <returns>A unique directory name.</returns>
    public virtual string GetNextIterationDirectoryName(string parentDirectoryName, string originalDirectoryName)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(parentDirectoryName), "parentDirectoryName");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(originalDirectoryName), "originalDirectoryName");

        uint iteration = 0;
        do
        {
            string tryMe = iteration == 0
                ? originalDirectoryName
                : string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", originalDirectoryName, iteration.ToString(CultureInfo.InvariantCulture));
            string tryMePath = Path.Combine(parentDirectoryName, tryMe);

            if (!File.Exists(tryMePath) && !Directory.Exists(tryMePath))
            {
                return tryMePath;
            }

            ++iteration;
        }
        while (iteration != uint.MaxValue);

        // Return the original path in case file does not exist and let it fail.
        return Path.Combine(parentDirectoryName, originalDirectoryName);
    }

    /// <summary>
    /// Copies source file to destination file.
    /// </summary>
    /// <param name="source">Path to source file.</param>
    /// <param name="destination">Path to destination file.</param>
    /// <param name="warning">warnings to be reported.</param>
    /// <returns>
    /// Returns destination on full success,
    /// Returns empty string on error when specified to continue the run on error,
    /// throw on error when specified to abort the run on error.
    /// </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public virtual string CopyFileOverwrite(string source, string destination, out string? warning)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(source), "source should not be null.");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(destination), "destination should not be null.");

        try
        {
            string? destinationDirectory = Path.GetDirectoryName(destination);
            if (!StringEx.IsNullOrEmpty(destinationDirectory) && File.Exists(source) && !Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Overwrite
            DebugEx.Assert(Path.IsPathRooted(source), "DeploymentManager: source path " + source + " must be rooted!");
            File.Copy(source, destination, true);

            warning = null;
            return Path.GetFullPath(destination);
        }
        catch (Exception e)
        {
            warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToCopyWithOverwrite, source, destination, e.GetType(), e.GetExceptionMessage());
            return string.Empty;
        }
    }

    /// <summary>
    /// For given file checks if it is a binary, then finds and deploys PDB for line number info in call stack.
    /// </summary>
    /// <returns>
    /// Returns deployed destination pdb file path if everything is ok, otherwise null.
    /// </returns>
    /// <param name="destinationFile">The file we need to find PDBs for (we care only about binaries).</param>
    /// <param name="relativeDestination">Destination relative to the root of deployment dir.</param>
    /// <param name="sourceFile">Original file of destinationFile, i.e. the file copied to deployment dir.</param>
    /// <param name="destToSource">destToSource map.</param>
    public string? FindAndDeployPdb(string destinationFile, string relativeDestination, string sourceFile, Dictionary<string, string> destToSource)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(destinationFile), "destination should not be null or empty.");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(relativeDestination), "relative destination path should not be null or empty.");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(sourceFile), "sourceFile should not be null or empty.");
        DebugEx.Assert(destToSource != null, "destToSource should not be null.");

        if (!_assemblyUtility.IsAssemblyExtension(Path.GetExtension(destinationFile)))
        {
            return null;
        }

        string? pdbSource = GetSymbolsFileName(sourceFile);
        if (StringEx.IsNullOrEmpty(pdbSource))
        {
            return null;
        }

        string? pdbDestination;
        string? relativePdbDestination;
        try
        {
            pdbDestination = Path.Combine(Path.GetDirectoryName(destinationFile)!, Path.GetFileName(pdbSource));
            relativePdbDestination = Path.Combine(Path.GetDirectoryName(relativeDestination)!, Path.GetFileName(pdbDestination));
        }
        catch (ArgumentException ex)
        {
            EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "Error while trying to locate pdb for deployed assembly '{0}': {1}", destinationFile, ex);
            return null;
        }

        // If already processed, do nothing.
        if (!destToSource.TryGetValue(relativePdbDestination, out string? value))
        {
            if (DoesFileExist(pdbSource))
            {
                pdbDestination = CopyFileOverwrite(pdbSource, pdbDestination, out _);
                if (!StringEx.IsNullOrEmpty(pdbDestination))
                {
                    destToSource.Add(relativePdbDestination, pdbSource);
                    return pdbDestination;
                }
            }
        }
        else if (!string.Equals(pdbSource, value, StringComparison.OrdinalIgnoreCase))
        {
            EqtTrace.WarningIf(
                EqtTrace.IsWarningEnabled,
                "Conflict during copying PDBs for line number info: '{0}' and '{1}' are from different origins although they might be the same.",
                pdbSource,
                value);
        }

        return null;
    }

    public virtual List<string> AddFilesFromDirectory(string directoryPath, bool ignoreIOExceptions) => AddFilesFromDirectory(directoryPath, null, ignoreIOExceptions);

    public virtual List<string> AddFilesFromDirectory(string directoryPath, Func<string, bool>? ignoreDirectory, bool ignoreIOExceptions)
    {
        var files = new List<string>();

        try
        {
            files.AddRange(GetFilesInADirectory(directoryPath));
        }
        catch (IOException) when (ignoreIOExceptions)
        {
        }

        foreach (string subDirectoryPath in GetDirectoriesInADirectory(directoryPath))
        {
            if (ignoreDirectory != null && ignoreDirectory(subDirectoryPath))
            {
                continue;
            }

            files.AddRange(AddFilesFromDirectory(subDirectoryPath, ignoreDirectory, true));
        }

        return files;
    }

    public static string TryConvertPathToRelative(string path, string rootDir)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(path), "path should not be null or empty.");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(rootDir), "rootDir should not be null or empty.");

#pragma warning disable IDE0057 // Use range operator
        return Path.IsPathRooted(path) && path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase)
            ? path.Substring(rootDir.Length).TrimStart(Path.DirectorySeparatorChar)
            : path;
#pragma warning restore IDE0057 // Use range operator
    }

    /// <summary>
    /// The function goes among the subdirectories of the specified one and clears all of
    /// them.
    /// </summary>
    /// <param name="filePath">The root directory to clear.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public virtual void DeleteDirectories(string filePath)
    {
        Guard.NotNullOrWhiteSpace(filePath);
        try
        {
            var root = new DirectoryInfo(filePath);
            root.Delete(true);
        }
        catch (Exception ex)
        {
            EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, "DeploymentManager.DeleteDirectories failed for the directory '{0}': {1}", filePath, ex);
        }
    }

    public virtual bool DoesDirectoryExist(string deploymentDirectory) => Directory.Exists(deploymentDirectory);

    public virtual bool DoesFileExist(string testSource) => File.Exists(testSource);

    public virtual void SetAttributes(string path, FileAttributes fileAttributes) => File.SetAttributes(path, fileAttributes);

    public virtual string[] GetFilesInADirectory(string directoryPath) => Directory.GetFiles(directoryPath);

    public virtual string[] GetDirectoriesInADirectory(string directoryPath) => Directory.GetDirectories(directoryPath);

    /// <summary>
    /// Returns either PDB file name from inside compiled binary or null if this cannot be done.
    /// Does not throw.
    /// </summary>
    /// <param name="path">path to symbols file.</param>
    /// <returns>Pdb file name or null if non-existent.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private static string? GetSymbolsFileName(string? path)
    {
        if (StringEx.IsNullOrEmpty(path) || path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
        {
            if (EqtTrace.IsWarningEnabled)
            {
                EqtTrace.Warning("Path is either null or invalid. Path = '{0}'", path);
            }

            return null;
        }

        string pdbFile = Path.ChangeExtension(path, ".pdb");
        if (File.Exists(pdbFile))
        {
            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("Pdb file found for path '{0}'", path);
            }

            return pdbFile;
        }

        return null;
    }
}

#endif
