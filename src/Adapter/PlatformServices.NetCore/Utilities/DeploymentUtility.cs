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
    using System.Reflection;
    using System.Security;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    internal class DeploymentUtility : DeploymentUtilityBase
    {
        public DeploymentUtility()
            : base()
        {
        }

        public DeploymentUtility(DeploymentItemUtility deploymentItemUtility, AssemblyUtility assemblyUtility, FileUtility fileUtility)
            : base(deploymentItemUtility, assemblyUtility, fileUtility)
        {
        }

        /// <summary>
        /// Does the deployment of parameter deployment items & the testSource to the parameter directory.
        /// </summary>
        /// <param name="deploymentItems">The deployment item.</param>
        /// <param name="testSource">The test source.</param>
        /// <param name="deploymentDirectory">The deployment directory.</param>
        /// <param name="resultsDirectory">Root results directory</param>
        /// <returns>Returns a list of deployment warnings</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public override IEnumerable<string> Deploy(IList<DeploymentItem> deploymentItems, string testSource, string deploymentDirectory, string resultsDirectory)
        {
            Validate.IsFalse(string.IsNullOrWhiteSpace(deploymentDirectory), "Deployment directory is null or empty");
            Validate.IsTrue(this.FileUtility.DoesDirectoryExist(deploymentDirectory), $"Deployment directory {deploymentDirectory} does not exist");
            Validate.IsFalse(string.IsNullOrWhiteSpace(testSource), "TestSource directory is null/empty");
            Validate.IsTrue(this.FileUtility.DoesFileExist(testSource), $"TestSource {testSource} does not exist.");

            testSource = Path.GetFullPath(testSource);
            var warnings = new List<string>();

            string errorMessage;
            if (!this.DeploymentItemUtility.IsValidDeploymentItem(testSource, string.Empty, out errorMessage))
            {
                warnings.Add(errorMessage);
            }
            else
            {
                EqtTrace.Info("Adding the test source directory to the deploymentitems list");
                this.DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(Path.GetDirectoryName(testSource)));
            }

            // Maps relative to Out dir destination -> source and used to determine if there are conflicted items.
            var destToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Copy the deployment items. (As deployment item can correspond to directories as well, so each deployment item may map to n files)
            foreach (var deploymentItem in deploymentItems)
            {
                ValidateArg.NotNull(deploymentItem, "deploymentItem should not be null.");

                // Validate the output directory.
                if (!this.IsOutputDirectoryValid(deploymentItem, deploymentDirectory, warnings))
                {
                    continue;
                }

                // Get the files corresponding to this deployment item
                var deploymentItemFiles = this.GetFullPathToFilesCorrespondingToDeploymentItem(deploymentItem, testSource, resultsDirectory, warnings, out bool itemIsDirectory);
                if (deploymentItemFiles == null)
                {
                    continue;
                }

                var fullPathToDeploymentItemSource = this.GetFullPathToDeploymentItemSource(deploymentItem.SourcePath, testSource);

                // Note: source is already rooted.
                foreach (var fileToDeploy in deploymentItemFiles)
                {
                    Debug.Assert(Path.IsPathRooted(fileToDeploy), "File " + fileToDeploy + " is not rooted");

                    Debug.Assert(Path.IsPathRooted(fileToDeploy), "File " + fileToDeploy + " is not rooted");

                    // Ignore the test platform files.
                    var tempFile = Path.GetFileName(fileToDeploy);
                    var assemblyName = Path.GetFileName(this.GetType().GetTypeInfo().Assembly.Location);
                    if (tempFile.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string relativeDestination;
                    if (itemIsDirectory)
                    {
                        // Deploy into subdirectory of deployment (Out) dir.
                        Debug.Assert(fileToDeploy.StartsWith(fullPathToDeploymentItemSource, StringComparison.Ordinal), "Somehow source is outside original dir.");
                        relativeDestination = this.FileUtility.TryConvertPathToRelative(fileToDeploy, fullPathToDeploymentItemSource);
                    }
                    else
                    {
                        // Deploy just to the deployment (Out) dir.
                        relativeDestination = Path.GetFileName(fileToDeploy);
                    }

                    relativeDestination = Path.Combine(deploymentItem.RelativeOutputDirectory, relativeDestination);  // Ignores empty arg1.
                    var destination = Path.Combine(deploymentDirectory, relativeDestination);
                    try
                    {
                        destination = Path.GetFullPath(destination);
                    }
                    catch (Exception e)
                    {
                        var warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToAccessFile, destination, e.GetType(), e.Message);
                        warnings.Add(warning);

                        continue;
                    }

                    if (!destToSource.ContainsKey(relativeDestination))
                    {
                        destToSource.Add(relativeDestination, fileToDeploy);

                        // Now, finally we can copy the file...
                        destination = this.FileUtility.CopyFileOverwrite(fileToDeploy, destination, out string warning);
                        if (!string.IsNullOrEmpty(warning))
                        {
                            warnings.Add(warning);
                        }

                        if (string.IsNullOrEmpty(destination))
                        {
                            continue;
                        }

                        // We clear the attributes so that e.g. you can write to the copies of files originally under SCC.
                        this.FileUtility.SetAttributes(destination, FileAttributes.Normal);

                        // Deploy PDB for line number info in stack trace.
                        this.FileUtility.FindAndDeployPdb(destination, relativeDestination, fileToDeploy, destToSource);
                    }
                    else if (
                        !string.Equals(
                            fileToDeploy,
                            destToSource[relativeDestination],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        EqtTrace.WarningIf(
                            EqtTrace.IsWarningEnabled,
                            "Conflict during copiyng file: '{0}' and '{1}' are from different origins although they might be the same.",
                            fileToDeploy,
                            destToSource[relativeDestination]);
                    }
                } // foreach itemFile.
            }

            return warnings;
        }

        /// <summary>
        /// Get root deployment directory
        /// </summary>
        /// <param name="baseDirectory">The base directory.</param>
        /// <returns>Root deployment directory.</returns>
        protected override string GetRootDeploymentDirectory(string baseDirectory)
        {
            string dateTimeSufix = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", DateTimeFormatInfo.InvariantInfo);
            string directoryName = string.Format(CultureInfo.CurrentCulture, Resource.TestRunName, DeploymentFolderPrefix, Environment.GetEnvironmentVariable("USERNAME") ?? Environment.GetEnvironmentVariable("USER"), dateTimeSufix);
            directoryName = this.FileUtility.ReplaceInvalidFileNameCharacters(directoryName);

            return this.FileUtility.GetNextIterationDirectoryName(baseDirectory, directoryName);
        }

        private bool IsDeploymentItemSourceADirectory(DeploymentItem deploymentItem, string testSource, out string resultDirectory)
        {
            resultDirectory = null;

            string directory = this.GetFullPathToDeploymentItemSource(deploymentItem.SourcePath, testSource);
            directory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (this.FileUtility.DoesDirectoryExist(directory))
            {
                resultDirectory = directory;
                return true;
            }

            return false;
        }
    }
}
