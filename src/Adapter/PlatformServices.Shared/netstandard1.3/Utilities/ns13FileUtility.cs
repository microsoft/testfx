// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    internal class FileUtility
    {
        private AssemblyUtility assemblyUtility;

        public FileUtility()
        {
            this.assemblyUtility = new AssemblyUtility();
        }

        public virtual void CreateDirectoryIfNotExists(string directory)
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
        public string ReplaceInvalidFileNameCharacters(string fileName)
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
        /// <returns>A unique directory name.</returns>
        public virtual string GetNextIterationDirectoryName(string parentDirectoryName, string originalDirectoryName)
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
        public virtual string CopyFileOverwrite(string source, string destination, out string warning)
        {
            Debug.Assert(!string.IsNullOrEmpty(source), "source should not be null.");
            Debug.Assert(!string.IsNullOrEmpty(destination), "destination should not be null.");

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
        public string FindAndDeployPdb(string destinationFile, string relativeDestination, string sourceFile, Dictionary<string, string> destToSource)
        {
            Debug.Assert(!string.IsNullOrEmpty(destinationFile), "destination should not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(relativeDestination), "relative destination path should not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(sourceFile), "sourceFile should not be null or empty.");
            Debug.Assert(destToSource != null, "destToSource should not be null.");

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
                relativePdbDestination = Path.Combine(Path.GetDirectoryName(relativeDestination), Path.GetFileName(pdbDestination));
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

        public virtual List<string> AddFilesFromDirectory(string directoryPath, bool ignoreIOExceptions)
        {
            return this.AddFilesFromDirectory(directoryPath, null, ignoreIOExceptions);
        }

        public virtual List<string> AddFilesFromDirectory(string directoryPath, Func<string, bool> ignoreDirectory, bool ignoreIOExceptions)
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
                if (ignoreDirectory != null && ignoreDirectory(subDirectoryPath))
                {
                    continue;
                }

                var subDirectoryContents = this.AddFilesFromDirectory(subDirectoryPath, ignoreDirectory, true);
                if (subDirectoryContents?.Count > 0)
                {
                    fileContents.AddRange(subDirectoryContents);
                }
            }

            return fileContents;
        }

        public string TryConvertPathToRelative(string path, string rootDir)
        {
            Debug.Assert(!string.IsNullOrEmpty(path), "path should not be null or empty.");
            Debug.Assert(!string.IsNullOrEmpty(rootDir), "rootDir should not be null or empty.");

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public virtual void DeleteDirectories(string filePath)
        {
            Validate.IsFalse(string.IsNullOrWhiteSpace(filePath), "Invalid filePath provided");
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

        public virtual bool DoesDirectoryExist(string deploymentDirectory)
        {
            return Directory.Exists(deploymentDirectory);
        }

        public virtual bool DoesFileExist(string testSource)
        {
            return File.Exists(testSource);
        }

        public virtual void SetAttributes(string path, FileAttributes fileAttributes)
        {
            File.SetAttributes(path, fileAttributes);
        }

        public virtual string[] GetFilesInADirectory(string directoryPath)
        {
            return Directory.GetFiles(directoryPath);
        }

        public virtual string[] GetDirectoriesInADirectory(string directoryPath)
        {
            return Directory.GetDirectories(directoryPath);
        }

        /// <summary>
        /// Returns either PDB file name from inside compiled binary or null if this cannot be done.
        /// Does not throw.
        /// </summary>
        /// <param name="path">path to symbols file.</param>
        /// <returns>Pdb file name or null if non-existent.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private string GetSymbolsFileName(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
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
}
