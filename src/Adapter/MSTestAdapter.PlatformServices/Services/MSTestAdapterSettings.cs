// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Globalization;
using System.Xml;

using Microsoft.Testing.Platform.Configurations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class MSTestAdapterSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MSTestAdapterSettings"/> class.
    /// </summary>
    public MSTestAdapterSettings()
    {
        DeleteDeploymentDirectoryAfterTestRunIsComplete = true;
        DeploymentEnabled = true;
        DeployTestSourceDependencies = true;
        SearchDirectories = [];
        Configuration = null;
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
    /// Gets a value indicating whether the test source references are to deployed.
    /// </summary>
    public bool DeployTestSourceDependencies { get; private set; }

    /// <summary>
    ///  Gets list of paths recursive or non recursive paths.
    /// </summary>
    protected List<RecursiveDirectoryPath> SearchDirectories { get; private set; }

    internal static IConfiguration? Configuration { get; set; }

    /// <summary>
    /// Convert the parameter xml to TestSettings.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    /// <returns>An instance of the <see cref="MSTestAdapterSettings"/> class.</returns>
    public static MSTestAdapterSettings ToSettings(XmlReader reader)
    {
        Guard.NotNull(reader);

        // Expected format of the xml is: -
        //
        // <MSTestV2>
        //     <DeploymentEnabled>true</DeploymentEnabled>
        //     <DeployTestSourceDependencies>true</DeployTestSourceDependencies>
        //     <ConsiderFixturesAsSpecialTests>true</ConsiderFixturesAsSpecialTests>
        //     <DeleteDeploymentDirectoryAfterTestRunIsComplete>true</DeleteDeploymentDirectoryAfterTestRunIsComplete>
        //     <AssemblyResolution>
        //          <Directory path= "% HOMEDRIVE %\directory "includeSubDirectories = "true" />
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

    private static void ParseBooleanSetting(IConfiguration configuration, string key, Action<bool> setSetting)
    {
        if (configuration[$"mstest:{key}"] is not string value)
        {
            return;
        }

        if (bool.TryParse(value, out bool result))
        {
            setSetting(result);
        }
    }

    internal static MSTestAdapterSettings ToSettings(IConfiguration configuration)
    {
        // Expected format of the json is: -
        //
        // "mstest" : {
        //  "deployment" : {
        //       "enabled": true / false,
        //       "deployTestSourceDependencies": true / false,
        //       "deleteDeploymentDirectoryAfterTestRunIsComplete": true / false
        //  },
        //  ... remaining settings
        // }
        var settings = new MSTestAdapterSettings();
        Configuration = configuration;
        ParseBooleanSetting(configuration, "deployment:enabled", value => settings.DeploymentEnabled = value);
        ParseBooleanSetting(configuration, "deployment:deployTestSourceDependencies", value => settings.DeployTestSourceDependencies = value);
        ParseBooleanSetting(configuration, "deployment:deleteDeploymentDirectoryAfterTestRunIsComplete", value => settings.DeleteDeploymentDirectoryAfterTestRunIsComplete = value);

        settings.ReadAssemblyResolutionPath(configuration);

        return settings;
    }

    public static bool IsAppDomainCreationDisabled(string? settingsXml)
    {
        // Expected format of the json is: -
        // "mstest" : {
        //  "execution": {
        //    "disableAppDomain": true,
        //  }
        // }
        if (StringEx.IsNullOrEmpty(settingsXml) && Configuration is null)
        {
            return false;
        }

        bool disableAppDomain = false;

        if (!StringEx.IsNullOrEmpty(settingsXml))
        {
            StringReader stringReader = new(settingsXml);
            var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
            disableAppDomain = reader.ReadToFollowing("DisableAppDomain") &&
                bool.TryParse(reader.ReadInnerXml(), out bool result) && result;
        }

        string? isAppDomainDisabled = Configuration?["mstest:execution:disableAppDomain"];
        if (!StringEx.IsNullOrEmpty(isAppDomainDisabled))
        {
            disableAppDomain = bool.TryParse(isAppDomainDisabled, out bool result) && result;
        }

        return disableAppDomain;
    }

    /// <summary>
    /// Returns the array of path with recursive property true/false from comma separated paths
    /// for ex: paths = c:\a\b;e:\balh\foo;%SystemDrive%\SomeDirectory and recursive = true
    /// it will return an list {{c:\a\b, true}, {e:\balh\foo, true}, {c:\somedirectory, true}}.
    /// </summary>
    /// <param name="baseDirectory">the base directory for relative path.</param>
    /// <returns>RecursiveDirectoryPath information.</returns>
    public List<RecursiveDirectoryPath> GetDirectoryListWithRecursiveProperty(string baseDirectory)
    {
        List<RecursiveDirectoryPath> directoriesList = [];

        foreach (RecursiveDirectoryPath recPath in SearchDirectories)
        {
            // If path has environment variable, then resolve it
            string? directoryPath = ResolveEnvironmentVariableAndReturnFullPathIfExist(recPath.DirectoryPath, baseDirectory);

            if (!StringEx.IsNullOrEmpty(directoryPath))
            {
                directoriesList.Add(new RecursiveDirectoryPath(directoryPath, recPath.IncludeSubDirectories));
            }
        }

        return directoriesList;
    }

    /// <summary>
    /// Gets the full path and expands any environment variables contained in the path.
    /// </summary>
    /// <param name="path">The path to be expanded.</param>
    /// <param name="baseDirectory">The base directory for the path which is not rooted path.</param>
    /// <returns>The expanded path.</returns>
    internal string? ResolveEnvironmentVariableAndReturnFullPathIfExist(string path, string baseDirectory)
    {
        // Trim beginning and trailing white space from the path.
        path = path.Trim(' ', '\t');

        if (StringEx.IsNullOrEmpty(path))
        {
            return null;
        }

        string? warningMessage = null;

        // Expand any environment variables in the path.
        path = ExpandEnvironmentVariables(path);

        // If the path is a relative path, expand it relative to the base directory
        if (!Path.IsPathRooted(path))
        {
            if (!StringEx.IsNullOrEmpty(baseDirectory))
            {
                path = Path.Combine(baseDirectory, path);
            }
            else
            {
                warningMessage = $"The Directory: {path}, has following problem: {"This is not an absolute path. A base directory should be provided for this to be used as a relative path."}";

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

        if (!StringEx.IsNullOrEmpty(warningMessage))
        {
            warningMessage = $"The Directory: {path}, has following problem: {warningMessage}";

            if (EqtTrace.IsWarningEnabled)
            {
                EqtTrace.Warning(warningMessage);
            }

            return null;
        }

        if (DoesDirectoryExist(path))
        {
            return path;
        }

        // generate warning that path does not exist.
        EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, $"The Directory: {path}, does not exist.");

        return null;
    }

    /// <summary>
    /// Verifies if a directory exists.
    /// </summary>
    /// <param name="path">path.</param>
    /// <returns>True if directory exists.</returns>
    /// <remarks>Only present for unit testing scenarios.</remarks>
    protected virtual bool DoesDirectoryExist(string path) => Directory.Exists(path);

    /// <summary>
    /// Expands any environment variables in the path provided.
    /// </summary>
    /// <param name="path">path.</param>
    /// <returns>expanded string.</returns>
    /// <remarks>Only present for unit testing scenarios.</remarks>
    protected virtual string ExpandEnvironmentVariables(string path) => Environment.ExpandEnvironmentVariables(path);

    private void ReadAssemblyResolutionPath(XmlReader reader)
    {
        Guard.NotNull(reader);

        // Expected format of the xml is: -
        //
        // <AssemblyResolution>
        //     <Directory path= "% HOMEDRIVE %\directory "includeSubDirectories = "true" />
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
                    string? recursiveAttribute = reader.GetAttribute("includeSubDirectories");

                    // read the path specified
                    string? path = reader.GetAttribute("path");

                    if (!StringEx.IsNullOrEmpty(path))
                    {
                        // Do we have to look in sub directory for dependent dll.
                        bool includeSubDirectories = string.Equals(recursiveAttribute, "true", StringComparison.OrdinalIgnoreCase);
                        SearchDirectories.Add(new RecursiveDirectoryPath(path, includeSubDirectories));
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

    private void ReadAssemblyResolutionPath(IConfiguration configuration)
    {
        // Expected format of the json is: -
        //
        // "mstest" : {
        //    "assemblyResolution" : [
        //        { "path" : "..." , includeSubDirectories: "true" } ,
        //        { "path" : "..." , includeSubDirectories: "true" } ,
        //        { "path" : "..." , includeSubDirectories: "true" } ,
        //        ...
        //     ]
        //  ... remaining settings
        // }
        int index = 0;
        while (configuration[$"mstest:assemblyResolution:{index}:path"] is string path)
        {
            if (!StringEx.IsNullOrEmpty(path))
            {
                // Default includeSubDirectories to false if not provided
                bool includeSubDirectories = false;
                ParseBooleanSetting(configuration, $"assemblyResolution:{index++}:includeSubDirectories", value => includeSubDirectories = value);

                SearchDirectories.Add(new RecursiveDirectoryPath(path, includeSubDirectories));
            }
        }
    }
}
#endif
