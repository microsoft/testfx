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
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

#pragma warning disable SA1649 // File name must match first type name

    internal abstract class DeploymentUtilityBase
    {
        protected const string TestAssemblyConfigFileExtension = ".config";
        protected const string NetAppConfigFile = "App.Config";

        /// <summary>
        /// Prefix for deployment folder to avoid confusions with other folders (like trx attachments).
        /// </summary>
        protected const string DeploymentFolderPrefix = "Deploy";

        public DeploymentUtilityBase()
            : this(new DeploymentItemUtility(new ReflectionUtility()), new AssemblyUtility(), new FileUtility())
        {
        }

        public DeploymentUtilityBase(DeploymentItemUtility deploymentItemUtility, AssemblyUtility assemblyUtility, FileUtility fileUtility)
        {
            this.DeploymentItemUtility = deploymentItemUtility;
            this.AssemblyUtility = assemblyUtility;
            this.FileUtility = fileUtility;
        }

        protected FileUtility FileUtility { get; set; }

        protected DeploymentItemUtility DeploymentItemUtility { get; set; }

        protected AssemblyUtility AssemblyUtility { get; set; }

        public bool Deploy(IEnumerable<TestCase> tests, string source, IRunContext runContext, ITestExecutionRecorder testExecutionRecorder, TestRunDirectories runDirectories)
        {
            IList<DeploymentItem> deploymentItems = this.DeploymentItemUtility.GetDeploymentItems(tests);

            // we just deploy source if there are no deployment items for current source but there are deployment items for other sources
            return this.Deploy(source, runContext, testExecutionRecorder, deploymentItems, runDirectories);
        }

        /// <summary>
        /// Create deployment directories
        /// </summary>
        /// <param name="runContext">The run context.</param>
        /// <returns>TestRunDirectories instance.</returns>
        public TestRunDirectories CreateDeploymentDirectories(IRunContext runContext)
        {
            var resultsDirectory = this.GetTestResultsDirectory(runContext);
            var rootDeploymentDirectory = this.GetRootDeploymentDirectory(resultsDirectory);

            var result = new TestRunDirectories(rootDeploymentDirectory);
            var inDirectory = result.InDirectory;
            var outDirectory = result.OutDirectory;
            var inMachineDirectory = result.InMachineNameDirectory;

            this.FileUtility.CreateDirectoryIfNotExists(rootDeploymentDirectory);
            this.FileUtility.CreateDirectoryIfNotExists(inDirectory);
            this.FileUtility.CreateDirectoryIfNotExists(outDirectory);
            this.FileUtility.CreateDirectoryIfNotExists(inMachineDirectory);

            return result;
        }

        public abstract IEnumerable<string> Deploy(IList<DeploymentItem> deploymentItems, string testSource, string deploymentDirectory, string resultsDirectory);

        internal string GetConfigFile(string testSource)
        {
            string configFile = null;

            if (this.FileUtility.DoesFileExist(testSource + TestAssemblyConfigFileExtension))
            {
                // Path to config file cannot be bad: storage is already checked, and extension is valid.
                configFile = testSource + TestAssemblyConfigFileExtension;
            }
            else
            {
                var netAppConfigFile = Path.Combine(Path.GetDirectoryName(testSource), NetAppConfigFile);
                if (this.FileUtility.DoesFileExist(netAppConfigFile))
                {
                    configFile = netAppConfigFile;
                }
            }

            return configFile;
        }

        protected abstract string GetRootDeploymentDirectory(string baseDirectory);

        /// <summary>
        /// Get files corresponding to parameter deployment item.
        /// </summary>
        /// <param name="deploymentItem">Deployment Item.</param>
        /// <param name="testSource">The test source.</param>
        /// <param name="resultsDirectory">Results directory which should be skipped for deployment</param>
        /// <param name="warnings">Warnings.</param>
        /// <param name="isDirectory">Is this a directory.</param>
        /// <returns>Paths to items to deploy.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        protected string[] GetFullPathToFilesCorrespondingToDeploymentItem(DeploymentItem deploymentItem, string testSource, string resultsDirectory, IList<string> warnings, out bool isDirectory)
        {
            Debug.Assert(deploymentItem != null, "deploymentItem should not be null.");
            Debug.Assert(!string.IsNullOrEmpty(testSource), "testsource should not be null or empty.");

            try
            {
                isDirectory = this.IsDeploymentItemSourceADirectory(deploymentItem, testSource, out string directory);
                if (isDirectory)
                {
                    return this.FileUtility.AddFilesFromDirectory(
                        directory, (deployDirectory) => string.Equals(deployDirectory, resultsDirectory, StringComparison.OrdinalIgnoreCase), false).ToArray();
                }

                if (this.IsDeploymentItemSourceAFile(deploymentItem.SourcePath, testSource, out string fileName))
                {
                    return new[] { fileName };
                }

                // If file/directory is not found, then try removing the prefix and see if it is present.
                string fileOrDirNameOnly = Path.GetFileName(deploymentItem.SourcePath.TrimEnd(
                            new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }));
                if (this.IsDeploymentItemSourceAFile(fileOrDirNameOnly, testSource, out fileName))
                {
                    return new[] { fileName };
                }

                string message = string.Format(CultureInfo.CurrentCulture, Resource.CannotFindFile, fileName);
                throw new FileNotFoundException(message, fileName);
            }
            catch (Exception e)
            {
                warnings.Add(string.Format(
                    CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToGetFileForDeploymentItem, deploymentItem, e.GetType(), e.Message));
            }

            isDirectory = false;
            return null;
        }

        protected string GetFullPathToDeploymentItemSource(string deploymentItemSourcePath, string testSource)
        {
            if (Path.IsPathRooted(deploymentItemSourcePath))
            {
                return deploymentItemSourcePath;
            }

            return Path.Combine(Path.GetDirectoryName(testSource), deploymentItemSourcePath);
        }

        /// <summary>
        /// Validate the output directory for the parameter deployment item.
        /// </summary>
        /// <param name="deploymentItem">The deployment item.</param>
        /// <param name="deploymentDirectory">The deployment directory.</param>
        /// <param name="warnings">Warnings.</param>
        /// <returns>True if valid.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        protected bool IsOutputDirectoryValid(DeploymentItem deploymentItem, string deploymentDirectory, IList<string> warnings)
        {
            Debug.Assert(deploymentItem != null, "deploymentItem should not be null.");
            Debug.Assert(!string.IsNullOrEmpty(deploymentDirectory), "deploymentDirectory should not be null or empty.");
            Debug.Assert(warnings != null, "warnings should not be null.");

            // Check that item.output dir does not go outside deployment Out dir, otherwise you can erase any file!
            string outputDir = deploymentDirectory;
            try
            {
                outputDir = Path.GetFullPath(Path.Combine(deploymentDirectory, deploymentItem.RelativeOutputDirectory));

                // convert the short path to full length path (like joe~1.dom to joe.domain) and the comparison
                // startsWith in the next loop will work for the matching paths.
                deploymentDirectory = Path.GetFullPath(deploymentDirectory);
            }
            catch (Exception e)
            {
                string warning = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.DeploymentErrorFailedToAccesOutputDirectory,
                    deploymentItem.SourcePath,
                    outputDir,
                    e.GetType(),
                    e.GetExceptionMessage());

                warnings.Add(warning);
                return false;
            }

            if (!outputDir.StartsWith(deploymentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                string warning = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.DeploymentErrorBadDeploymentItem,
                    deploymentItem.SourcePath,
                    deploymentItem.RelativeOutputDirectory);
                warnings.Add(warning);

                return false;
            }

            return true;
        }

        protected string AddTestSourceConfigFileIfExists(string testSource, IList<DeploymentItem> deploymentItems)
        {
            string configFile = this.GetConfigFile(testSource);

            if (string.IsNullOrEmpty(configFile) == false)
            {
                this.DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(configFile));
            }

            return configFile;
        }

        /// <summary>
        /// Log the parameter warnings on the parameter logger
        /// </summary>
        /// <param name="testExecutionRecorder">Execution recorder.</param>
        /// <param name="warnings">Warnings.</param>
        private static void LogWarnings(ITestExecutionRecorder testExecutionRecorder, IEnumerable<string> warnings)
        {
            if (warnings == null)
            {
                return;
            }

            Debug.Assert(testExecutionRecorder != null, "Logger should not be null");

            // log the warnings
            foreach (string warning in warnings)
            {
                testExecutionRecorder.SendMessage(TestMessageLevel.Warning, warning);
            }
        }

        private bool Deploy(string source, IRunContext runContext, ITestExecutionRecorder testExecutionRecorder, IList<DeploymentItem> deploymentItems, TestRunDirectories runDirectories)
        {
            ValidateArg.NotNull(runDirectories, "runDirectories");
            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("MSTestExecutor: Found that deployment items for source {0} are: ", source);
                foreach (var item in deploymentItems)
                {
                    EqtTrace.Info("MSTestExecutor: SourcePath: - {0}", item.SourcePath);
                }
            }

            // Do the deployment.
            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "MSTestExecutor: Using deployment directory {0} for source {1}.", runDirectories.OutDirectory, source);
            var warnings = this.Deploy(new List<DeploymentItem>(deploymentItems), source, runDirectories.OutDirectory, this.GetTestResultsDirectory(runContext));

            // Log warnings
            LogWarnings(testExecutionRecorder, warnings);
            return deploymentItems != null && deploymentItems.Count > 0;
        }

        private bool IsDeploymentItemSourceAFile(string deploymentItemSourcePath, string testSource, out string file)
        {
            file = this.GetFullPathToDeploymentItemSource(deploymentItemSourcePath, testSource);

            return this.FileUtility.DoesFileExist(file);
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

        /// <summary>
        /// Get the parent test results directory where deployment will be done.
        /// </summary>
        /// <param name="runContext">The run context.</param>
        /// <returns>The test results directory.</returns>
        private string GetTestResultsDirectory(IRunContext runContext)
        {
            var resultsDirectory = (!string.IsNullOrEmpty(runContext?.TestRunDirectory)) ?
                runContext.TestRunDirectory : null;

            if (string.IsNullOrEmpty(resultsDirectory))
            {
                resultsDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), TestRunDirectories.DefaultDeploymentRootDirectory));
            }

            return resultsDirectory;
        }
    }
}
