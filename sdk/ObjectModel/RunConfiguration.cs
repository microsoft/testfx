// ---------------------------------------------------------------------------
// <copyright file="RunConfiguration.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Stores information about RunConfiguration.
// </summary>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

#if !SILVERLIGHT
using System.Xml.XPath;
#endif

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    public enum Architecture
    {
        Default,
        X86,
        X64,
        ARM,
        AnyCPU
    }

    public enum FrameworkVersion
    {
        None,
        Framework35,
        Framework40,
        Framework45
    }

    /// <summary>
    /// Stores information about a test settings.
    /// </summary>
    public class RunConfiguration : TestRunSettings
    {
        #region Constructor

        /// <summary>
        /// Initializes with the name of the test case.
        /// </summary>
        /// <param name="name">The name of the test case.</param>
        /// <param name="executorUri">The Uri of the executor to use for running this test.</param>
        public RunConfiguration()
            : base(Constants.RunConfigurationSettingsName)
        {
            // set defaults for target platform, framework version type and results directory.
            this.m_platform = Constants.DefaultPlatform;
            this.m_framework = Constants.DefaultFramework;
            this.m_resultsDirectory = Constants.DefaultResultsDirectory;
            this.SolutionDirectory = null;
            this.m_treatTestAdapterErrorsAsWarnings = Constants.DefaultTreatTestAdapterErrorsAsWarnings;
            this.m_binariesRoot = null;
            this.m_testAdaptersPaths = null;
            this.m_maxCpuCount = Constants.DefaultCpuCount;
            this.m_disableAppDomain = false;
        }

        #endregion

        #region Properties

        public string SolutionDirectory
        {
            get;
            set;
        }

        public string ResultsDirectory
        {
            get
            {
                return m_resultsDirectory;
            }

            set
            {
                m_resultsDirectory = value;
                ResultsDirectorySet = true;
            }
        }

        /// <summary>
        /// Parallel execution option. Should be non-negative integer.
        /// </summary>
        public int MaxCpuCount
        {
            get
            {
                return m_maxCpuCount;
            }
            set
            {
                m_maxCpuCount = value;
                MaxCpuCountSet = true;
            }
        }

        /// <summary>
        /// Disable App domain creation. 
        /// </summary>
        public bool DisableAppDomain
        {
            get
            {
                return m_disableAppDomain;
            }
            set
            {
                m_disableAppDomain = value;
                DisableAppDomainSet = true;
            }
        }

        /// <summary>
        /// Target platform this run is targeting. Possible values are x86|x64|arm|anycpu
        /// </summary>
        public Architecture TargetPlatform
        {
            get
            {
                return m_platform;
            }
            set
            {
                m_platform = value;
                TargetPlatformSet = true;
            }
        }

        /// <summary>
        /// Target Framework this run is targeting. Possible values are Framework3.5|Framework4.0|Framework4.5
        /// </summary>
        public FrameworkVersion TargetFrameworkVersion
        {
            get
            {
                return m_framework;
            }

            set
            {
                m_framework = value;
                TargetFrameworkSet = true;
            }
        }

        /// <summary>
        /// Paths at which rocksteady should look for test adapters
        /// </summary>
        public string TestAdaptersPaths
        {
            get
            {
                return m_testAdaptersPaths;
            }

            set
            {
                m_testAdaptersPaths = value;

                if(m_testAdaptersPaths != null)
                {
                    TestAdaptersPathsSet = true;
                }
            }
        }

        /// <summary>
        /// Whether to treat the errors from test adapters as warnings.
        /// </summary>
        public bool TreatTestAdapterErrorsAsWarnings
        {
            get
            {
                return m_treatTestAdapterErrorsAsWarnings;
            }

            set
            {
                m_treatTestAdapterErrorsAsWarnings = value;
            }
        }

        public bool TargetPlatformSet
        {
            get;
            private set;
        }

        public bool MaxCpuCountSet
        {
            get;
            private set;
        }

        public bool DisableAppDomainSet
        {
            get;
            private set;
        }

        public bool TargetFrameworkSet
        {
            get;
            private set;
        }

        public bool TestAdaptersPathsSet
        {
            get;
            private set;
        }

        public bool ResultsDirectorySet
        {
            get;
            private set;
        }

        public string BinariesRoot
        {
            get
            {
                return m_binariesRoot;
            }

            private set
            {
                m_binariesRoot = value;
            }
        }

        #endregion

#if !SILVERLIGHT
        public override XmlElement ToXml()
        {
            XmlDocument doc = new XmlDocument();

            XmlElement root = doc.CreateElement(Constants.RunConfigurationSettingsName);

            XmlElement resultDirectory = doc.CreateElement("ResultsDirectory");
            resultDirectory.InnerXml = this.ResultsDirectory;
            root.AppendChild(resultDirectory);

            XmlElement targetPlatform = doc.CreateElement("TargetPlatform");
            targetPlatform.InnerXml = this.TargetPlatform.ToString();
            root.AppendChild(targetPlatform);

            XmlElement maxCpuCount = doc.CreateElement("MaxCpuCount");
            maxCpuCount.InnerXml = this.MaxCpuCount.ToString();
            root.AppendChild(maxCpuCount);

            XmlElement disableAppDomain = doc.CreateElement("DisableAppDomain");
            disableAppDomain.InnerXml = this.DisableAppDomain.ToString();
            root.AppendChild(disableAppDomain);

            XmlElement targetFrameworkVersion = doc.CreateElement("TargetFrameworkVersion");
            targetFrameworkVersion.InnerXml = this.TargetFrameworkVersion.ToString();
            root.AppendChild(targetFrameworkVersion);

            var testAdaptersPaths = doc.CreateElement("TestAdaptersPaths");
            testAdaptersPaths.InnerXml = this.TestAdaptersPaths;
            root.AppendChild(testAdaptersPaths);

            XmlElement treatTestAdapterErrorsAsWarnings = doc.CreateElement("TreatTestAdapterErrorsAsWarnings");
            treatTestAdapterErrorsAsWarnings.InnerXml = this.TreatTestAdapterErrorsAsWarnings.ToString();
            root.AppendChild(treatTestAdapterErrorsAsWarnings);

            XmlElement BinariesRoot = doc.CreateElement("BinariesRoot");
            BinariesRoot.InnerXml = this.BinariesRoot;
            root.AppendChild(BinariesRoot);

            return root;
        }

#endif
        /// <summary>
        /// Loads RunConfiguration from XmlReader.
        /// </summary>
        /// <param name="reader">XmlReader having run configuration node.</param>
        /// <returns></returns>
        public static RunConfiguration FromXml(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");
            RunConfiguration runConfiguration = new RunConfiguration();
            bool empty = reader.IsEmptyElement;

            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
            
            // Process the fields in Xml elements
            reader.Read();
            if (!empty)
            {
                while (reader.NodeType == XmlNodeType.Element)
                {
                    string elementName = reader.Name;
                    switch (elementName)
                    {
                        case "ResultsDirectory":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                            runConfiguration.ResultsDirectory = reader.ReadElementContentAsString();
                            break;

                        case "MaxCpuCount":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);

                            string cpuCount = reader.ReadElementContentAsString();
                            int count;
                            if (!int.TryParse(cpuCount, out count) || count < 0)
                            {
                                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, cpuCount, elementName));
                            }

                            runConfiguration.MaxCpuCount = count;
                            break;

                        case "DisableAppDomain":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);

                            string appContainerCheck = reader.ReadElementContentAsString();
                            bool disableAppDomainCheck;
                            if (!bool.TryParse(appContainerCheck, out disableAppDomainCheck))
                            {
                                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, appContainerCheck, elementName));
                            }
                            runConfiguration.DisableAppDomain = disableAppDomainCheck;
                            break;

                        case "TargetPlatform":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                            Architecture archType;
                            string value = reader.ReadElementContentAsString();
                            try
                            {
                                archType = (Architecture) Enum.Parse(typeof(Architecture), value, true);
                                if(archType != Architecture.X64 && archType != Architecture.X86 && archType != Architecture.ARM)
                                {
                                    throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                      Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, value, elementName));
                                }

                            }
                            catch(ArgumentException)
                            {
                                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, value, elementName));
                            }
                            runConfiguration.TargetPlatform = archType;
                            break;

                        case "TargetFrameworkVersion":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                            FrameworkVersion frameworkType;
                            value = reader.ReadElementContentAsString();
                            try
                            {
                                frameworkType = (FrameworkVersion)Enum.Parse(typeof(FrameworkVersion), value, true);
                                if(frameworkType != FrameworkVersion.Framework35 && frameworkType != FrameworkVersion.Framework40 &&
                                    frameworkType != FrameworkVersion.Framework45)
                                {
                                    throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                        Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, value, elementName));
                                }

                            }
                            catch (ArgumentException)
                            {
                                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, value, elementName));
                            }
                            runConfiguration.TargetFrameworkVersion = frameworkType;
                            break;

                        case "TestAdaptersPaths":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                            runConfiguration.TestAdaptersPaths = reader.ReadElementContentAsString();
                            break;

                        case "TreatTestAdapterErrorsAsWarnings":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                            bool treatTestAdapterErrorsAsWarnings = false;

                            value = reader.ReadElementContentAsString();

                            try
                            {
                                treatTestAdapterErrorsAsWarnings = bool.Parse(value);
                            }
                            catch (ArgumentException)
                            {
                                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, value, elementName));
                            }
                            catch (FormatException)
                            {
                                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsIncorrectValue, Constants.RunConfigurationSettingsName, value, elementName));
                            }

                            runConfiguration.TreatTestAdapterErrorsAsWarnings = treatTestAdapterErrorsAsWarnings;
                            break;

                        case "SolutionDirectory":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                            string solutionDirectory = reader.ReadElementContentAsString();

#if !SILVERLIGHT
                            solutionDirectory = Environment.ExpandEnvironmentVariables(solutionDirectory);
                            if (string.IsNullOrEmpty(solutionDirectory) || !Directory.Exists(solutionDirectory))
#else
                            if (string.IsNullOrEmpty(solutionDirectory))
#endif
                            {
                                if (EqtTrace.IsErrorEnabled)
                                {
                                    EqtTrace.Error(string.Format(CultureInfo.CurrentCulture, Resources.SolutionDirectoryNotExists, solutionDirectory));
                                }

                                solutionDirectory = null;
                            }

                            runConfiguration.SolutionDirectory = solutionDirectory;

                            break;

                        case "BinariesRoot":
                            XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                            runConfiguration.BinariesRoot = reader.ReadElementContentAsString();
                            break;

                        default:
                            throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsXmlElement, Constants.RunConfigurationSettingsName, reader.Name));

                    }
                }
                reader.ReadEndElement();
            }
            return runConfiguration; ;

        }

        /// <summary>
        /// Platform architecture which rocksteady should use for discovery/execution
        /// </summary>
        private Architecture m_platform;

        /// <summary>
        /// Maximum number of cores that the engine can use to run tests in parallel
        /// </summary>
        private int m_maxCpuCount;

        /// <summary>
        /// Specifies whether user wants to disable app-container check. 
        /// Default is false.
        /// </summary>
        private bool m_disableAppDomain;

        /// <summary>
        /// .Net framework which rocksteady should use for discovery/execution
        /// </summary>
        private FrameworkVersion m_framework;

        /// <summary>
        /// Directory in which rocksteady/adapter should keep their run specific data. 
        /// </summary>
        private string m_resultsDirectory;

        /// <summary>
        /// Paths at which rocksteady should look for test adapters
        /// </summary>
        private string m_testAdaptersPaths;

        /// <summary>
        /// Whether to treat the errors from test adapters as warnings.
        /// </summary>
        private bool m_treatTestAdapterErrorsAsWarnings;

        /// <summary>
        /// Build bin root directory 
        /// </summary>
        private string m_binariesRoot;

    }
}
