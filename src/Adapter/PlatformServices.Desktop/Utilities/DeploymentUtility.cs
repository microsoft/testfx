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

            if (MSTestSettingsProvider.Settings.DeployTestSourceDependencies)
            {
                EqtTrace.Info("Adding the references and satellite assemblies to the deploymentitems list");

                // Get the referenced assemblies.
                this.ProcessNewStorage(testSource, deploymentItems, warnings);

                // Get the satellite assemblies
                var satelliteItems = this.GetSatellites(deploymentItems, testSource, warnings);
                foreach (var satelliteItem in satelliteItems)
                {
                    this.DeploymentItemUtility.AddDeploymentItem(deploymentItems, satelliteItem);
                }
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
                foreach (var deploymentItemFile in deploymentItemFiles)
                {
                    Debug.Assert(Path.IsPathRooted(deploymentItemFile), "File " + deploymentItemFile + " is not rooted");

                    // List of files to deploy, by default, just itemFile.
                    var filesToDeploy = new List<string>(1);
                    filesToDeploy.Add(deploymentItemFile);

                    // Find dependencies of test deployment items and deploy them at the same time as master file.
                    if (deploymentItem.OriginType == DeploymentItemOriginType.PerTestDeployment &&
                        this.AssemblyUtility.IsAssemblyExtension(Path.GetExtension(deploymentItemFile)))
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
                    } // foreach fileToDeploy.
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
            string directoryName = string.Format(CultureInfo.CurrentCulture, Resource.TestRunName, DeploymentFolderPrefix, Environment.UserName, dateTimeSufix);
            directoryName = this.FileUtility.ReplaceInvalidFileNameCharacters(directoryName);

            return this.FileUtility.GetNextIterationDirectoryName(baseDirectory, directoryName);
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
            string[] references = this.AssemblyUtility.GetFullPathToDependentAssemblies(testSource, configFile, out warningList);
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
                this.DeploymentItemUtility.AddDeploymentItem(deploymentItems, deploymentItem);

                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("DeploymentManager: Reference:{0} ", reference);
                }
            }
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

                    if (string.IsNullOrEmpty(path) || !this.AssemblyUtility.IsAssemblyExtension(Path.GetExtension(path))
                        || !this.FileUtility.DoesFileExist(path) || !this.AssemblyUtility.IsAssembly(path))
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
                    List<string> itemSatellites = this.AssemblyUtility.GetSatelliteAssemblies(path);
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
                        this.DeploymentItemUtility.AddDeploymentItem(satellites, satelliteItem);
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private void ProcessNewStorage(string testSource, IList<DeploymentItem> deploymentItems, IList<string> warnings)
        {
            // Add deployment items and process .config files only for storages we have not processed before.
            string errorMessage;
            if (!this.DeploymentItemUtility.IsValidDeploymentItem(testSource, string.Empty, out errorMessage))
            {
                warnings.Add(errorMessage);
                return;
            }

            this.DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(testSource, string.Empty, DeploymentItemOriginType.TestStorage));

            // Deploy .config file if exists, only for assemlbies, i.e. DLL and EXE.
            // First check <TestStorage>.config, then if not found check for App.Config
            // and deploy AppConfig to <TestStorage>.config.
            if (this.AssemblyUtility.IsAssemblyExtension(Path.GetExtension(testSource)))
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
    }
}
