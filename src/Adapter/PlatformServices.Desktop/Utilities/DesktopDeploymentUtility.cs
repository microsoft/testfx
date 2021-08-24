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
    using System.Security;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

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

        public override void AddDeploymentItemsBasedOnMsTestSetting(string testSource, IList<DeploymentItem> deploymentItems, List<string> warnings)
        {
            if (MSTestSettingsProvider.Settings.DeployTestSourceDependencies)
            {
                EqtTrace.Info("Adding the references and satellite assemblies to the deployment items list");

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
                EqtTrace.Info("Adding the test source directory to the deployment items list");
                this.DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(Path.GetDirectoryName(testSource)));
            }
        }

        /// <summary>
        /// Get root deployment directory
        /// </summary>
        /// <param name="baseDirectory">The base directory.</param>
        /// <returns>Root deployment directory.</returns>
        public override string GetRootDeploymentDirectory(string baseDirectory)
        {
            string dateTimeSufix = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", DateTimeFormatInfo.InvariantInfo);
            string directoryName = string.Format(CultureInfo.CurrentCulture, Resource.TestRunName, DeploymentFolderPrefix, Environment.UserName, dateTimeSufix);
            directoryName = this.FileUtility.ReplaceInvalidFileNameCharacters(directoryName);

            return this.FileUtility.GetNextIterationDirectoryName(baseDirectory, directoryName);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        protected void ProcessNewStorage(string testSource, IList<DeploymentItem> deploymentItems, IList<string> warnings)
        {
            // Add deployment items and process .config files only for storages we have not processed before.
            if (!this.DeploymentItemUtility.IsValidDeploymentItem(testSource, string.Empty, out var errorMessage))
            {
                warnings.Add(errorMessage);
                return;
            }

            this.DeploymentItemUtility.AddDeploymentItem(deploymentItems, new DeploymentItem(testSource, string.Empty, DeploymentItemOriginType.TestStorage));

            // Deploy .config file if exists, only for assemblies, i.e. DLL and EXE.
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

        protected override void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings)
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

        protected IEnumerable<DeploymentItem> GetSatellites(IEnumerable<DeploymentItem> deploymentItems, string testSource, IList<string> warnings)
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
                        Debug.Assert(satelliteDir.Length > itemDir.Length + 1, "DeploymentManager.DoDeployment: wrong satellite dir length!");

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

        /// <summary>
        /// Process test storage and add dependent assemblies to dependencyDeploymentItems.
        /// </summary>
        /// <param name="testSource">The test source.</param>
        /// <param name="configFile">The config file.</param>
        /// <param name="deploymentItems">Deployment items.</param>
        /// <param name="warnings">Warnings.</param>
        private void AddDependencies(string testSource, string configFile, IList<DeploymentItem> deploymentItems, IList<string> warnings)
        {
            Debug.Assert(!string.IsNullOrEmpty(testSource), "testSource should not be null or empty.");

            // config file can be null.
            Debug.Assert(deploymentItems != null, "deploymentItems should not be null.");
            Debug.Assert(Path.IsPathRooted(testSource), "path should be rooted.");

            // Note: if this is not an assembly we simply return empty array, also:
            //       we do recursive search and report missing.
            string[] references = this.AssemblyUtility.GetFullPathToDependentAssemblies(testSource, configFile, out var warningList);
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
    }
}
