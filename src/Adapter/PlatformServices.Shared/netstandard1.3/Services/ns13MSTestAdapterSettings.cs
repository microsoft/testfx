﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    public class MSTestAdapterSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestAdapterSettings"/> class.
        /// </summary>
        public MSTestAdapterSettings()
        {
            this.DeleteDeploymentDirectoryAfterTestRunIsComplete = true;
            this.DeploymentEnabled = true;
            this.DeployTestSourceDependencies = true;
            this.SearchDirectories = new List<RecursiveDirectoryPath>();
        }

        /// <summary>
        ///  Gets a value indicating whether deployment is enable or not.
        /// </summary>
        public bool DeploymentEnabled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether deployment directory has to be deleted after test run.
        /// </summary>
        public bool DeleteDeploymentDirectoryAfterTestRunIsComplete { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the test source references are to deployed
        /// </summary>
        public bool DeployTestSourceDependencies { get; private set; }

        /// <summary>
        ///  Gets list of paths recursive or non recursive paths.
        /// </summary>
        protected List<RecursiveDirectoryPath> SearchDirectories { get; private set; }

        /// <summary>
        /// Convert the parameter xml to TestSettings
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        /// <returns>An instance of the <see cref="MSTestAdapterSettings"/> class</returns>
        public static MSTestAdapterSettings ToSettings(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");

            // Expected format of the xml is: -
            //
            // <MSTestV2>
            //     <DeploymentEnabled>true</DeploymentEnabled>
            //     <DeployTestSourceDependencies>true</DeployTestSourceDependencies>
            //     <DeleteDeploymentDirectoryAfterTestRunIsComplete>true</DeleteDeploymentDirectoryAfterTestRunIsComplete>
            //     <AssemblyResolution>
            //          <Directory path= "% HOMEDRIVE %\direvtory "includeSubDirectories = "true" />
            //          <Directory path= "C:\windows" includeSubDirectories = "false" />
            //          <Directory path= ".\DirectoryName" />  ...// by default includeSubDirectories is false
            //     </AssemblyResolution>
            // </MSTestV2>
            MSTestAdapterSettings settings = MSTestSettingsProvider.Settings;

            if (!reader.IsEmptyElement)
            {
                reader.Read();

                while (reader.NodeType == XmlNodeType.Element)
                {
                    bool result;
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "ASSEMBLYRESOLUTION":
                            {
                                settings.ReadAssemblyResolutionPath(reader);
                                break;
                            }

                        case "DEPLOYMENTENABLED":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.DeploymentEnabled = result;
                                }

                                break;
                            }

                        case "DEPLOYTESTSOURCEDEPENDENCIES":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.DeployTestSourceDependencies = result;
                                }

                                break;
                            }

                        case "DELETEDEPLOYMENTDIRECTORYAFTERTESTRUNISCOMPLETE":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.DeleteDeploymentDirectoryAfterTestRunIsComplete = result;
                                }

                                break;
                            }

                        default:
                            {
                                reader.Skip();
                                break;
                            }
                    }
                }
            }

            return settings;
        }

        public static bool IsAppDomainCreationDisabled(string settingsXml)
        {
            bool disableAppDomain = false;
            if (!string.IsNullOrEmpty(settingsXml))
            {
                StringReader stringReader = new StringReader(settingsXml);
                XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

                if (reader.ReadToFollowing("DisableAppDomain"))
                {
                    bool.TryParse(reader.ReadInnerXml(), out disableAppDomain);
                }
            }

            return disableAppDomain;
        }

        /// <summary>
        /// Returns the array of path with recursive property true/false from comma separated paths
        /// for ex: paths = c:\a\b;e:\balh\foo;%SystemDrive%\SomeDirectory and recursive = true
        /// it will return an list {{c:\a\b, true}, {e:\balh\foo, true}, {c:\somedirectory, true}}
        /// </summary>
        /// <param name="baseDirectory">the base directory for relative path.</param>
        /// <returns>RecursiveDirectoryPath information.</returns>
        public List<RecursiveDirectoryPath> GetDirectoryListWithRecursiveProperty(string baseDirectory)
        {
            List<RecursiveDirectoryPath> directoriesList = new List<RecursiveDirectoryPath>();

            foreach (RecursiveDirectoryPath recPath in this.SearchDirectories)
            {
                // If path has environment variable, then resolve it
                string directorypath = this.ResolveEnvironmentVariableAndReturnFullPathIfExist(recPath.DirectoryPath, baseDirectory);

                if (!string.IsNullOrEmpty(directorypath))
                {
                    directoriesList.Add(new RecursiveDirectoryPath(directorypath, recPath.IncludeSubDirectories));
                }
            }

            return directoriesList;
        }

        /// <summary>
        /// Gets the full path and expands any environment variables contained in the path.
        /// </summary>
        /// <param name="path">The path to be expanded.</param>
        /// <param name="baseDirectory">The base directory for the path which is not rooted path</param>
        /// <returns>The expanded path.</returns>
        internal string ResolveEnvironmentVariableAndReturnFullPathIfExist(string path, string baseDirectory)
        {
            // Trim begining and trailing white space from the path.
            path = path.Trim(' ', '\t');

            if (!string.IsNullOrEmpty(path))
            {
                string warningMessage = null;

                // Expand any environment variables in the path.
                path = this.ExpandEnvironmentVariables(path);

                // If the path is a relative path, expand it relative to the base directory
                if (!Path.IsPathRooted(path))
                {
                    if (!string.IsNullOrEmpty(baseDirectory))
                    {
                        path = Path.Combine(baseDirectory, path);
                    }
                    else
                    {
                        warningMessage = string.Format("The Directory: {0}, has following problem: {1}", path, "This is not an absolute path. A base directory should be provided for this to be used as a relative path.");

                        if (EqtTrace.IsWarningEnabled)
                        {
                            EqtTrace.Warning(warningMessage);
                        }

                        return null;
                    }
                }

                try
                {
                    // Get the full path.
                    // This will cleanup the path converting any "..\" to the appropriate value
                    // and convert any alternative directory separators to "\"
                    path = Path.GetFullPath(path);
                }
                catch (Exception e)
                {
                    warningMessage = e.Message;
                }

                if (!string.IsNullOrEmpty(warningMessage))
                {
                    warningMessage = string.Format("The Directory: {0}, has following problem: {1}", path, warningMessage);

                    if (EqtTrace.IsWarningEnabled)
                    {
                        EqtTrace.Warning(warningMessage);
                    }

                    return null;
                }

                if (this.DoesDirectoryExist(path))
                {
                    return path;
                }

                // generate warning that path does not exist.
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, string.Format("The Directory: {0}, does not exist.", path));
            }

            return null;
        }

        /// <summary>
        /// Verifies if a directory exists.
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>True if directory exists.</returns>
        /// <remarks>Only present for unit testing scenarios.</remarks>
        protected virtual bool DoesDirectoryExist(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// Expands any environment variables in the path provided.
        /// </summary>
        /// <param name="path">path</param>
        /// <returns>expanded string</returns>
        /// <remarks>Only present for unit testing scenarios.</remarks>
        protected virtual string ExpandEnvironmentVariables(string path)
        {
            return Environment.ExpandEnvironmentVariables(path);
        }

        private void ReadAssemblyResolutionPath(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");

            // Expected format of the xml is: -
            //
            // <AssemblyResolution>
            //     <Directory path= "% HOMEDRIVE %\direvtory "includeSubDirectories = "true" />
            //     <Directory path= "C:\windows" includeSubDirectories = "false" />
            //     <Directory path= ".\DirectoryName" />  ...// by default includeSubDirectories is false
            // </AssemblyResolution>
            bool empty = reader.IsEmptyElement;
            reader.Read();

            if (!empty)
            {
                while (reader.NodeType == XmlNodeType.Element)
                {
                    if (string.Equals("Directory", reader.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        string recursiveAttribute = reader.GetAttribute("includeSubDirectories");

                        // read the path specified
                        string path = reader.GetAttribute("path");

                        if (!string.IsNullOrEmpty(path))
                        {
                            // Do we have to look in sub directory for dependent dll.
                            var includeSubDirectories = string.Equals(recursiveAttribute, "true", StringComparison.OrdinalIgnoreCase);
                            this.SearchDirectories.Add(new RecursiveDirectoryPath(path, includeSubDirectories));
                        }
                    }
                    else
                    {
                        string message = string.Format(CultureInfo.CurrentCulture, Resource.InvalidSettingsXmlElement, reader.Name, "AssemblyResolution");
                        throw new SettingsException(message);
                    }

                    // Move to the next element under tag AssemblyResolution
                    reader.Read();
                }
            }

            // go to the end of the element.
            reader.ReadEndElement();
        }
    }
}
