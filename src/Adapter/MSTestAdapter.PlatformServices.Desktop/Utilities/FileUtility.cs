// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Extensions;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

    internal class FileUtility
    {
        private AssemblyUtility assemblyUtility;

        internal FileUtility()
        {
            this.assemblyUtility = new AssemblyUtility();
        }

        internal virtual void CreateDirectoryIfNotExists(string directory)
        {
            Debug.Assert(!string.IsNullOrEmpty(directory), "directory");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);    // Creates subdir chain if necessary.
            }
        }

        /// <summary>
        /// Replaces the invalid file/path characters from the parameter file name with '_'
        /// </summary>
        /// <param name="fileName"> The file Name. </param>
        /// <returns> The fileName devoid of any invalid characters. </returns>
        internal string ReplaceInvalidFileNameCharacters(string fileName)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileName), "fileName");

            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, ch) => current.Replace(ch, '_'));
        }

        /// <summary>
        /// Checks whether directory with specified name exists in the specified directory.
        /// If it exits, adds [1],[2]... to the directory name and checks again.
        /// Returns full directory name (full path) of the iteration when the file does not exist.
        /// </summary>
        /// <param name="parentDirectoryName">The directory where to check.</param>
        /// <param name="originalDirectoryName">The original directory (that we would add [1],[2],.. in the end of if needed) name to check.</param>
        internal virtual string GetNextIterationDirectoryName(
            string parentDirectoryName,
            string originalDirectoryName)
        {
            Debug.Assert(!string.IsNullOrEmpty(parentDirectoryName), "parentDirectoryName");
            Debug.Assert(!string.IsNullOrEmpty(originalDirectoryName), "originalDirectoryName");

            uint iteration = 0;
            do
            {
                string tryMe;
                if (iteration == 0)
                {
                    tryMe = originalDirectoryName;
                }
                else
                {
                    tryMe = string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", originalDirectoryName, iteration.ToString(CultureInfo.InvariantCulture));
                }

                string tryMePath = Path.Combine(parentDirectoryName, tryMe);
                
                if (!File.Exists(tryMePath) && !Directory.Exists(tryMePath))
                {
                    return tryMePath;
                }

                ++iteration;
            } while (iteration != uint.MaxValue);

            // Return the original path in case file does not exist and let it fail. 
            return Path.Combine(parentDirectoryName, originalDirectoryName);
        }

        /// <summary>
        /// Copies source file to destination file.
        /// Returns destination on full success, 
        ///     returns empty string on error when specified to continue the run on error,
        ///     throw on error when specified to abort the run on error.
        /// </summary>
        /// <param name="source">Path to source file.</param>
        /// <param name="destination">Path to destination file.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal virtual string CopyFileOverwrite(string source, string destination, out string warning)
        {
            Debug.Assert(!string.IsNullOrEmpty(source));
            Debug.Assert(!string.IsNullOrEmpty(destination));

            try
            {
                string destinationDirectory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(destinationDirectory) && File.Exists(source) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                // Overwrite
                Debug.Assert(Path.IsPathRooted(source), "DeploymentManager: source path " + source + " must be rooted!");
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
        /// Returns deployed destination pdb file path if everything is Ok, otherwise null.
        /// </returns>
        /// <param name="destinationFile">The file we need to find PDBs for (we care only about binaries).</param>
        /// <param name="relativeDestination">Destination relative to the root of deployment dir.</param>
        /// <param name="sourceFile">Original file of destinationFile, i.e. the file copied to deployment dir.</param>
        /// <param name="destToSource">destToSource map.</param>
        internal string FindAndDeployPdb(string destinationFile, string relativeDestination, string sourceFile, Dictionary<string, string> destToSource)
        {
            Debug.Assert(!string.IsNullOrEmpty(destinationFile));
            Debug.Assert(!string.IsNullOrEmpty(relativeDestination));
            Debug.Assert(!string.IsNullOrEmpty(sourceFile));
            Debug.Assert(destToSource != null);

            if (!this.assemblyUtility.IsAssemblyExtension(Path.GetExtension(destinationFile)))
            {
                return null;
            }

            string pdbSource = this.GetSymbolsFileName(sourceFile);
            if (string.IsNullOrEmpty(pdbSource))
            {
                return null;
            }

            string pdbDestination = null;
            string relativePdbDestination = null;

            try
            {
                pdbDestination = Path.Combine(Path.GetDirectoryName(destinationFile), Path.GetFileName(pdbSource));
                relativePdbDestination = Path.Combine(
                    Path.GetDirectoryName(relativeDestination), Path.GetFileName(pdbDestination));
            }
            catch (ArgumentException ex)
            {
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "Error while trying to locate pdb for deployed assembly '{0}': {1}", destinationFile, ex);
                return null;
            }

            // If already processed, do nothing.
            if (!destToSource.ContainsKey(relativePdbDestination))
            {
                if (this.DoesFileExist(pdbSource))
                {
                    string warning;
                    pdbDestination = this.CopyFileOverwrite(pdbSource, pdbDestination, out warning);
                    if (!string.IsNullOrEmpty(pdbDestination))
                    {
                        destToSource.Add(relativePdbDestination, pdbSource);
                        return pdbDestination;
                    }
                }
            }
            else if (!string.Equals(pdbSource, destToSource[relativePdbDestination], StringComparison.OrdinalIgnoreCase))
            {
                EqtTrace.WarningIf(
                        EqtTrace.IsWarningEnabled,
                        "Conflict during copiyng PDBs for line number info: '{0}' and '{1}' are from different origins although they might be the same.",
                        pdbSource,
                        destToSource[relativePdbDestination]);
            }

            return null;
        }

        internal virtual List<string> AddFilesFromDirectory(string directoryPath, bool ignoreIOExceptions)
        {
            var fileContents = new List<string>();

            try
            {
                var files = this.GetFilesInADirectory(directoryPath);
                fileContents.AddRange(files);
            }
            catch (IOException)
            {
                if (!ignoreIOExceptions)
                {
                    throw;
                }
            }

            foreach (var subDirectoryPath in this.GetDirectoriesInADirectory(directoryPath))
            {
                var subDirectoryContents = this.AddFilesFromDirectory(subDirectoryPath, true);
                if (subDirectoryContents?.Count > 0)
                {
                    fileContents.AddRange(subDirectoryContents);
                }
            }

            return fileContents;
        }

        internal string TryConvertPathToRelative(string path, string rootDir)
        {
            Debug.Assert(!string.IsNullOrEmpty(path));
            Debug.Assert(!string.IsNullOrEmpty(rootDir));

            if (Path.IsPathRooted(path) && path.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(rootDir.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            return path;
        }

        /// <summary>
        /// The function goes among the subdirectories of the specified one and clears all of
        /// them. 
        /// </summary>
        /// <param name="filePath">The root directory to clear.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal virtual void DeleteDirectories(string filePath)
        {
            Debug.Assert(filePath != null, "filePath");

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

        internal virtual bool DoesDirectoryExist(string deploymentDirectory)
        {
            return Directory.Exists(deploymentDirectory);
        }

        internal virtual bool DoesFileExist(string testSource)
        {
            return File.Exists(testSource);
        }

        internal virtual void SetAttributes(string path, FileAttributes fileAttributes)
        {
            File.SetAttributes(path, fileAttributes);
        }

        internal virtual string[] GetFilesInADirectory(string directoryPath)
        {
            return Directory.GetFiles(directoryPath);
        }

        internal virtual string[] GetDirectoriesInADirectory(string directoryPath)
        {
            return Directory.GetDirectories(directoryPath);
        }

        /// <summary>
        /// Returns either PDB file name from inside compiled binary or null if this cannot be done.
        /// Does not throw.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private string GetSymbolsFileName(string path)
        {
            try
            {
                return DiaHelper.GetSymbolsFileName(path);
            }
            catch (Exception ex)
            {
                // If we can't get a pdb, it's unfortunate, but not fatal.  If
                // anything figure out a way to add a warning to the test for the
                // deployment.  Previously we were tracking specific exceptions,
                // and then EqtException and ComException started getting thrown.
                // This was breaking xcopy deployment where DIA is not installed.
                if (EqtTrace.IsWarningEnabled)
                {
                    EqtTrace.Warning("Error while trying to get pdb for assembly '{0}': {1}", path, ex);
                }
            }

            return null;
        }
    }
}
