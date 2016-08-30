// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

    using ISettingsProvider = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ISettingsProvider;
    using ObjectModel.Utilities;

    /// <summary>
    /// Class to read settings from the runsettings xml for the desktop.
    /// </summary>
    public class MSTestSettingsProvider : ISettingsProvider
    {
        /// <summary>
        /// Member variable for Adapter settings
        /// </summary>
        private static MSTestAdapterSettings settings;
        /// <summary>
        /// Property to get loaded settings 
        /// </summary>
        public static MSTestAdapterSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    settings = new MSTestAdapterSettings();
                }
                return settings;
            }
        }

        /// <summary>
        /// Reset the settings to its default.
        /// Used for testing purposes.
        /// </summary>
        internal static void Reset()
        {
            settings = null;
        }

        /// <summary>
        /// Load the settings from the reader.
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        public void Load(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");

            settings = MSTestAdapterSettings.ToSettings(reader);
        }

        public IDictionary<string, object> GetProperties(string source)
        {
            return TestDeployment.GetDeploymentInformation(source);
        }

        public const string SettingsName = "MSTestV2";
    }

    /// <summary>
    /// Adapter Settings for the run
    /// </summary>
    public class MSTestAdapterSettings
    {
        /// <summary>
        ///  It contains a list of path with property recursive or non recursive.
        /// </summary>
        private List<RecursiveDirectoryPath> searchDirectories;

        /// <summary>
        ///  Specifies whether deployment is enable or not.
        /// </summary>
        public bool DeploymentEnabled { get; private set; }

        /// <summary>
        /// Speccifies whether deployment directory has to be deleted after test run.
        /// </summary>
        public bool DeleteDeploymentDirectoryAfterTestRunIsComplete { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestAdapterSettings"/> class.
        /// </summary>
        public MSTestAdapterSettings()
        {
            DeleteDeploymentDirectoryAfterTestRunIsComplete = true;
            DeploymentEnabled = true;
            this.searchDirectories = new List<RecursiveDirectoryPath>();
        }

        /// <summary>
        /// Convert the parameter xml to TestSettings
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        /// <returns>An instance of the <see cref="MSTestAdapterSettings"/> class</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Reviewed. Suppression is OK here.")]
        public static MSTestAdapterSettings ToSettings(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");

            // Expected format of the xml is: - 
            //
            // <MSTestV2>
            //     <DeploymentEnabled>true</DeploymentEnabled>
            //     <DeleteDeploymentDirectoryAfterTestRunIsComplete>true</DeleteDeploymentDirectoryAfterTestRunIsComplete>
            //     <AssemblyResolution>
            //          <Directory path= "% HOMEDRIVE %\direvtory "includeSubDirectories = "true" />
            //          <Directory path= "C:\windows" includeSubDirectories = "false" />
            //          <Directory path= ".\DirectoryName" />  ...// by default includeSubDirectories is false
            //     </AssemblyResolution>
            // </MSTestV2>
            
            MSTestAdapterSettings settings = MSTestSettingsProvider.Settings;
            bool empty = reader.IsEmptyElement;
            reader.Read();

            if (!empty)
            {
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
                            if (bool.TryParse(reader.ReadInnerXml(), out result))
                            {
                                settings.DeploymentEnabled = result;
                            }
                            break;
                        case "DELETEDEPLOYMENTDIRECTORYAFTERTESTRUNISCOMPLETE":
                            if (bool.TryParse(reader.ReadInnerXml(), out result))
                            {
                                settings.DeleteDeploymentDirectoryAfterTestRunIsComplete = result;
                            }
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return settings;
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
                    if (String.Compare("Directory", reader.Name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        string recursiveAttribute = reader.GetAttribute("includeSubDirectories");

                        // read the path specified 
                        string path = reader.GetAttribute("path");

                        if (!string.IsNullOrEmpty(path))
                        {
                            // Do we have to look in sub directory for dependent dll.
                            var includeSubDirectories = String.Compare(recursiveAttribute, "true", StringComparison.OrdinalIgnoreCase) == 0;
                            this.searchDirectories.Add(new RecursiveDirectoryPath(path, includeSubDirectories));
                        }
                    }
                    else
                    {
                        string message = String.Format(CultureInfo.CurrentCulture, Resource.InvalidSettingsXmlElement, reader.Name, "AssemblyResolution");
                        throw new SettingsException(message);
                    }

                    // Move to the next element under tag AssemblyResolution
                    reader.Read();
                }
            }

            // go to the end of the element. 
            reader.ReadEndElement();
        }

        /// <summary>
        /// Returns the array of path with recursive property true/false from comma separated paths
        /// for ex: paths = c:\a\b;e:\balh\foo;%SystemDrive%\SomeDirectory and recursive = true
        /// it will return an list {{c:\a\b, true}, {e:\balh\foo, true}, {c:\somedirectory, true}}
        /// </summary>
        /// <param name="baseDirectory">the base directory for relative path.</param>
        public List<RecursiveDirectoryPath> GetDirectoryListWithRecursiveProperty(string baseDirectory)
        {
            List<RecursiveDirectoryPath> directoriesList = new List<RecursiveDirectoryPath>();

            foreach (RecursiveDirectoryPath recPath in this.searchDirectories)
            {
                // If path has environment variable, then resolve it
                string directorypath = ResolveEnvironmentVariableAndReturnFullPathIfExist(recPath.DirectoryPath, baseDirectory);

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
                path = Environment.ExpandEnvironmentVariables(path);

                // If the path is a relative path, expand it relative to the base directory
                if (!Path.IsPathRooted(path))
                {
                    if (!String.IsNullOrEmpty(baseDirectory))
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
                    // This will cleanup the path converting any "..\" to the appropariate value 
                    // and convert any alternative directory seperators to "\"
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

                if (Directory.Exists(path))
                {
                    return path;
                }

                // generate warning that path doesnot exist.
                EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, string.Format("The Directory: {0}, does not exist.", path));
            }

            return null;
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
    }
}
