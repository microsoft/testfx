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

    internal class DeploymentUtility
    {
        private const string TestAssemblyConfigFileExtension = ".config";
        private const string NetAppConfigFile = "App.Config";

        /// <summary>
        /// Prefix for deployment folder to avoid confusions with other folders (like trx attachments).
        /// </summary>
        private const string DeploymentFolderPrefix = "Deploy";

        private DeploymentItemUtility deploymentItemUtility;
        private FileUtility fileUtility;
        private AssemblyUtility assemblyUtility;

        public DeploymentUtility()
            : this(new DeploymentItemUtility(new ReflectionUtility()), new AssemblyUtility(), new FileUtility())
        {
        }

        public DeploymentUtility(DeploymentItemUtility deploymentItemUtility, AssemblyUtility assemblyUtility, FileUtility fileUtility)
        {
            this.deploymentItemUtility = deploymentItemUtility;
            this.assemblyUtility = assemblyUtility;
            this.fileUtility = fileUtility;
        }

        public bool Deploy(IEnumerable<TestCase> tests, string source, IRunContext runContext, ITestExecutionRecorder testExecutionRecorder, TestRunDirectories runDirectories)
        {
            IList<DeploymentItem> deploymentItems = this.deploymentItemUtility.GetDeploymentItems(tests);

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

            this.fileUtility.CreateDirectoryIfNotExists(rootDeploymentDirectory);
            this.fileUtility.CreateDirectoryIfNotExists(inDirectory);
            this.fileUtility.CreateDirectoryIfNotExists(outDirectory);
            this.fileUtility.CreateDirectoryIfNotExists(inMachineDirectory);

            return result;
        }

        internal string GetConfigFile(string testSource)
        {
            string configFile = null;

            if (this.fileUtility.DoesFileExist(testSource + TestAssemblyConfigFileExtension))
            {
                // Path to config file cannot be bad: storage is already checked, and extension is valid.
                configFile = testSource + TestAssemblyConfigFileExtension;
            }
            else
            {
                var netAppConfigFile = Path.Combine(Path.GetDirectoryName(testSource), NetAppConfigFile);
                if (this.fileUtility.DoesFileExist(netAppConfigFile))
                {
                    configFile = netAppConfigFile;
                }
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

        /// <summary>
        /// Does the deployment of parameter deployment items & the testSource to the parameter directory.
        /// </summary>
        /// <param name="deploymentItems">The deployment item.</param>
        /// <param name="testSource">The test source.</param>
        /// <param name="deploymentDirectory">The deployment directory.</param>
        /// <param name="resultsDirectory">Root results directory</param>
        /// <returns>Returns a list of deployment warnings</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private IEnumerable<string> Deploy(IList<DeploymentItem> deploymentItems, string testSource, string deploymentDirectory, string resultsDirectory)
        {
            Validate.IsFalse(string.IsNullOrWhiteSpace(deploymentDirectory), "Deployment directory is null or empty");
            Validate.IsTrue(this.fileUtility.DoesDirectoryExist(deploymentDirectory), $"Deployment directory {deploymentDirectory} does not exist");
            Validate.IsFalse(string.IsNullOrWhiteSpace(testSource), "TestSource directory is null/empty");
            Validate.IsTrue(this.fileUtility.DoesFileExist(testSource), $"TestSource {testSource} does not exist.");

            testSource = Path.GetFullPath(testSource);
            var warnings = new List<string>();

            if (MSTestSettingsProvider.Settings.DeployTestSourceDependencies)
            {
                EqtTrace.Info("Adding the references and satellite assemblies to the deploymentitems list");

                // Get the referenced assemblies.
                this.ProcessNewStorage(testSource, deploymentItems, warnings);

                // Get the satellite assemblies
                var satelliteItems = this.GetSatellites(deploymentItems, testSource, warnings);
                foreach (var satelliteItem in satelliteItems)
                {
                    this.deploymentItemUtility.AddDeploymentItem(deploymentItems, satelliteItem);
                }
            }
            else
            {
                EqtTrace.Info("Adding the test source directory to the deploymentitems list");
                this.deploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(Path.GetDirectoryName(testSource)));
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
                foreach (var deploymentItemFile in deploymentItemFiles)
                {
                    Debug.Assert(Path.IsPathRooted(deploymentItemFile), "File " + deploymentItemFile + " is not rooted");

                    // List of files to deploy, by default, just itemFile.
                    var filesToDeploy = new List<string>(1);
                    filesToDeploy.Add(deploymentItemFile);

                    // Find dependencies of test deployment items and deploy them at the same time as master file.
                    if (deploymentItem.OriginType == DeploymentItemOriginType.PerTestDeployment &&
                        this.assemblyUtility.IsAssemblyExtension(Path.GetExtension(deploymentItemFile)))
                    {
                        this.AddDependenciesOfDeploymentItem(deploymentItemFile, filesToDeploy, warnings);
                    }

                    foreach (var fileToDeploy in filesToDeploy)
                    {
                        Debug.Assert(Path.IsPathRooted(fileToDeploy), "File " + fileToDeploy + " is not rooted");

                        // Ignore the test platform files.
                        var tempFile = Path.GetFileName(fileToDeploy);
                        var assemblyName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                        if (tempFile.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string relativeDestination;
                        if (itemIsDirectory)
                        {
                            // Deploy into subdirectory of deployment (Out) dir.
                            Debug.Assert(fileToDeploy.StartsWith(fullPathToDeploymentItemSource, StringComparison.Ordinal), "Somehow source is outside original dir.");
                            relativeDestination = this.fileUtility.TryConvertPathToRelative(fileToDeploy, fullPathToDeploymentItemSource);
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
                            destination = this.fileUtility.CopyFileOverwrite(fileToDeploy, destination, out string warning);
                            if (!string.IsNullOrEmpty(warning))
                            {
                                warnings.Add(warning);
                            }

                            if (string.IsNullOrEmpty(destination))
                            {
                                continue;
                            }

                            // We clear the attributes so that e.g. you can write to the copies of files originally under SCC.
                            this.fileUtility.SetAttributes(destination, FileAttributes.Normal);

                            // Deploy PDB for line number info in stack trace.
                            this.fileUtility.FindAndDeployPdb(destination, relativeDestination, fileToDeploy, destToSource);
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
                    } // foreach fileToDeploy.
                } // foreach itemFile.
            }

            return warnings;
        }

        private void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings)
        {
            var dependencies = new List<DeploymentItem>();

            this.AddDependencies(deploymentItemFile, null, dependencies, warnings);

            foreach (var dependencyItem in dependencies)
            {
                Debug.Assert(Path.IsPathRooted(dependencyItem.SourcePath), "Path of the dependency " + dependencyItem.SourcePath + " is not rooted.");

                // Add dependencies to filesToDeploy.
                filesToDeploy.Add(dependencyItem.SourcePath);
            }
        }

        /// <summary>
        /// Process test storage and add dependant assemblies to dependencyDeploymentItems.
        /// </summary>
        /// <param name="testSource">The test source.</param>
        /// <param name="configFile">The config file.</param>
        /// <param name="deploymentItems">Deployment items.</param>
        /// <param name="warnings">Warnigns.</param>
        private void AddDependencies(string testSource, string configFile, IList<DeploymentItem> deploymentItems, IList<string> warnings)
        {
            Debug.Assert(!string.IsNullOrEmpty(testSource), "testSource should not be null or empty.");

            // config file can be null.
            Debug.Assert(deploymentItems != null, "deploymentItems should not be null.");
            Debug.Assert(Path.IsPathRooted(testSource), "path should be rooted.");

            // Note: if this is not an assembly we simply return empty array, also:
            //       we do recursive search and report missing.
            IList<string> warningList;
            string[] references = this.assemblyUtility.GetFullPathToDependentAssemblies(testSource, configFile, out warningList);
            if (warningList != null && warningList.Count > 0)
            {
                warnings = warnings.Concat(warningList).ToList();
            }

            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("DeploymentManager: Source:{0} has following references", testSource);
            }

            foreach (string reference in references)
            {
                DeploymentItem deploymentItem = new DeploymentItem(reference, string.Empty, DeploymentItemOriginType.Dependency);
                this.deploymentItemUtility.AddDeploymentItem(deploymentItems, deploymentItem);

                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("DeploymentManager: Reference:{0} ", reference);
                }
            }
        }

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
        private string[] GetFullPathToFilesCorrespondingToDeploymentItem(DeploymentItem deploymentItem, string testSource, string resultsDirectory, IList<string> warnings, out bool isDirectory)
        {
            Debug.Assert(deploymentItem != null, "deploymentItem should not be null.");
            Debug.Assert(!string.IsNullOrEmpty(testSource), "testsource should not be null or empty.");

            try
            {
                isDirectory = this.IsDeploymentItemSourceADirectory(deploymentItem, testSource, out string directory);
                if (isDirectory)
                {
                    return this.fileUtility.AddFilesFromDirectory(
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

        private bool IsDeploymentItemSourceAFile(string deploymentItemSourcePath, string testSource, out string file)
        {
            file = this.GetFullPathToDeploymentItemSource(deploymentItemSourcePath, testSource);

            return this.fileUtility.DoesFileExist(file);
        }

        private bool IsDeploymentItemSourceADirectory(DeploymentItem deploymentItem, string testSource, out string resultDirectory)
        {
            resultDirectory = null;

            string directory = this.GetFullPathToDeploymentItemSource(deploymentItem.SourcePath, testSource);
            directory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (this.fileUtility.DoesDirectoryExist(directory))
            {
                resultDirectory = directory;
                return true;
            }

            return false;
        }

        private IEnumerable<DeploymentItem> GetSatellites(IEnumerable<DeploymentItem> deploymentItems, string testSource, IList<string> warnings)
        {
            List<DeploymentItem> satellites = new List<DeploymentItem>();
            foreach (DeploymentItem item in deploymentItems)
            {
                // We do not care about deployment items which are directories because in that case we deploy all files underneath anyway.
                string path = null;
                try
                {
                    path = this.GetFullPathToDeploymentItemSource(item.SourcePath, testSource);
                    path = Path.GetFullPath(path);

                    if (string.IsNullOrEmpty(path) || !this.assemblyUtility.IsAssemblyExtension(Path.GetExtension(path))
                        || !this.fileUtility.DoesFileExist(path) || !this.assemblyUtility.IsAssembly(path))
                    {
                        continue;
                    }
                }
                catch (ArgumentException ex)
                {
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                }
                catch (SecurityException ex)
                {
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                }
                catch (IOException ex)
                {
                    // This covers PathTooLongException.
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                }
                catch (NotSupportedException ex)
                {
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                }

                // Note: now Path operations with itemPath should not result in any exceptions.
                // path is already canonicalized.

                // If we cannot access satellite due to security, etc, we report warning.
                try
                {
                    string itemDir = Path.GetDirectoryName(path).TrimEnd(
                        new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                    List<string> itemSatellites = this.assemblyUtility.GetSatelliteAssemblies(path);
                    foreach (string satellite in itemSatellites)
                    {
                        Debug.Assert(!string.IsNullOrEmpty(satellite), "DeploymentManager.DoDeployment: got empty satellite!");
                        Debug.Assert(
                            satellite.IndexOf(itemDir, StringComparison.OrdinalIgnoreCase) == 0,
                            "DeploymentManager.DoDeployment: Got satellite that does not start with original item path");

                        string satelliteDir = Path.GetDirectoryName(satellite).TrimEnd(
                            new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

                        Debug.Assert(!string.IsNullOrEmpty(satelliteDir), "DeploymentManager.DoDeployment: got empty satellite dir!");
                        Debug.Assert(satelliteDir.Length > itemDir.Length + 1, "DeploymentManager.DoDeployment: wrong satellite dir lenght!");

                        string localeDir = satelliteDir.Substring(itemDir.Length + 1);
                        Debug.Assert(!string.IsNullOrEmpty(localeDir), "DeploymentManager.DoDeployment: got empty dir name for satellite dir!");

                        string relativeOutputDir = Path.Combine(item.RelativeOutputDirectory, localeDir);

                        // Now finally add the item!
                        DeploymentItem satelliteItem = new DeploymentItem(satellite, relativeOutputDir, DeploymentItemOriginType.Satellite);
                        this.deploymentItemUtility.AddDeploymentItem(satellites, satelliteItem);
                    }
                }
                catch (ArgumentException ex)
                {
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                    string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorGettingSatellite, item, ex.GetType(), ex.GetExceptionMessage());
                    warnings.Add(warning);
                }
                catch (SecurityException ex)
                {
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                    string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorGettingSatellite, item, ex.GetType(), ex.GetExceptionMessage());
                    warnings.Add(warning);
                }
                catch (IOException ex)
                {
                    // This covers PathTooLongException.
                    EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, "DeploymentManager.GetSatellites: {0}", ex);
                    string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorGettingSatellite, item, ex.GetType(), ex.GetExceptionMessage());
                    warnings.Add(warning);
                }
            }

            return satellites;
        }

        private string GetFullPathToDeploymentItemSource(string deploymentItemSourcePath, string testSource)
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
        private bool IsOutputDirectoryValid(DeploymentItem deploymentItem, string deploymentDirectory, IList<string> warnings)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private void ProcessNewStorage(string testSource, IList<DeploymentItem> deploymentItems, IList<string> warnings)
        {
            // Add deployment items and process .config files only for storages we have not processed before.
            string errorMessage;
            if (!this.deploymentItemUtility.IsValidDeploymentItem(testSource, string.Empty, out errorMessage))
            {
                warnings.Add(errorMessage);
                return;
            }

            this.deploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(testSource, string.Empty, DeploymentItemOriginType.TestStorage));

            // Deploy .config file if exists, only for assemlbies, i.e. DLL and EXE.
            // First check <TestStorage>.config, then if not found check for App.Config
            // and deploy AppConfig to <TestStorage>.config.
            if (this.assemblyUtility.IsAssemblyExtension(Path.GetExtension(testSource)))
            {
                var configFile = this.AddTestSourceConfigFileIfExists(testSource, deploymentItems);

                // Deal with test dependencies: update dependencyDeploymentItems and missingDependentAssemblies.
                try
                {
                    // We look for dependent assemblies only for DLL and EXE's.
                    this.AddDependencies(testSource, configFile, deploymentItems, warnings);
                }
                catch (Exception e)
                {
                    string warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToDeployDependencies, testSource, e);
                    warnings.Add(warning);
                }
            }
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

        /// <summary>
        /// Get root deployment directory
        /// </summary>
        /// <param name="baseDirectory">The base directory.</param>
        /// <returns>Root deployment directory.</returns>
        private string GetRootDeploymentDirectory(string baseDirectory)
        {
            string dateTimeSufix = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", DateTimeFormatInfo.InvariantInfo);
            string directoryName = string.Format(CultureInfo.CurrentCulture, Resource.TestRunName, DeploymentFolderPrefix, Environment.UserName, dateTimeSufix);
            directoryName = this.fileUtility.ReplaceInvalidFileNameCharacters(directoryName);

            return this.fileUtility.GetNextIterationDirectoryName(baseDirectory, directoryName);
        }

        private string AddTestSourceConfigFileIfExists(string testSource, IList<DeploymentItem> deploymentItems)
        {
            string configFile = this.GetConfigFile(testSource);

            if (string.IsNullOrEmpty(configFile) == false)
            {
                this.deploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(configFile));
            }

            return configFile;
        }
    }
}
