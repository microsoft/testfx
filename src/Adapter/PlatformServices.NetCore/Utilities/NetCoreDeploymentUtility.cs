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

        protected override void ProcessNewStorage(string testSource, IList<DeploymentItem> deploymentItems, IList<string> warnings)
        {
        }

        protected override IEnumerable<DeploymentItem> GetSatellites(IEnumerable<DeploymentItem> deploymentItems, string testSource, IList<string> warnings)
        {
            return null;
        }

        protected override void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings)
        {
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
