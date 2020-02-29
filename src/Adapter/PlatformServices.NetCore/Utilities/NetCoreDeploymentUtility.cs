// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

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
        /// add deployment items based on MSTestSettingsProvider.Settings.DeployTestSourceDependencies. This property is ignored in net core.
        /// </summary>
        /// <param name="testSource">The test source.</param>
        /// <param name="deploymentItems">Deployment Items.</param>
        /// <param name="warnings">Warnings.</param>
        public override void AddDeploymentItemsBasedOnMsTestSetting(string testSource, IList<DeploymentItem> deploymentItems, List<string> warnings)
        {
            // It should add items from bin\debug but since deployment items in netcore are run from bin\debug only, so no need to implement it
        }

        /// <summary>
        /// Get root deployment directory
        /// </summary>
        /// <param name="baseDirectory">The base directory.</param>
        /// <returns>Root deployment directory.</returns>
        public override string GetRootDeploymentDirectory(string baseDirectory)
        {
            string dateTimeSufix = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", DateTimeFormatInfo.InvariantInfo);
            string directoryName = string.Format(CultureInfo.CurrentCulture, Resource.TestRunName, DeploymentFolderPrefix, Environment.GetEnvironmentVariable("USERNAME") ?? Environment.GetEnvironmentVariable("USER"), dateTimeSufix);
            directoryName = this.FileUtility.ReplaceInvalidFileNameCharacters(directoryName);

            return this.FileUtility.GetNextIterationDirectoryName(baseDirectory, directoryName);
        }

        /// <summary>
        /// Find dependencies of test deployment items
        /// </summary>
        /// <param name="deploymentItemFile">Deployment Item File</param>
        /// <param name="filesToDeploy">Files to Deploy</param>
        /// <param name="warnings">Warnings</param>
        protected override void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings)
        {
            // Its implemented only in full framework project as dependent files are not fetched in netcore.
        }
    }
}
