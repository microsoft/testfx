// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Xml;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;


    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    // TODO:Write Unit Tests
    /// <summary>
    /// Utilities for AppDomain
    /// </summary>
    internal static class AppDomainUtilities
    {
        private const string XmlNamespace = "urn:schemas-microsoft-com:asm.v1";
        private static Version defaultVersion = new Version();
        private static Version version45 = new Version("4.5");
        private const string ObjectModelVersionBuiltAgainst = "14.0.0.0";

        /// <summary>
        /// Set the target framework for app domain setup if target framework of dll is > 4.5
        /// </summary>
        /// <param name="setup">AppdomainSetup for app domain creation</param>
        /// <param name="testSource">path of test file</param>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
        internal static void SetAppDomainFrameworkVersionBasedOnTestSource(AppDomainSetup setup, string testSource)
        {
            string assemblyVersionString = GetTargetFrameworkVersionString(testSource);
            if (GetTargetFrameworkVersionFromVersionString(assemblyVersionString).CompareTo(version45) > 0)
            {
                PropertyInfo pInfo = typeof(AppDomainSetup).GetProperty(PlatformServices.Constants.TargetFrameworkName);
                if (pInfo != null)
                {
                    pInfo.SetValue(setup, assemblyVersionString, null);
                }
            }
        }

        /// <summary>
        /// Get target framework version string from the given dll
        /// </summary>
        /// <param name="path">
        /// The path of the dll
        /// </param>
        /// <returns>
        /// Framework string
        /// </returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly",
            Justification = "Reviewed. Suppression is OK here.")]
        internal static string GetTargetFrameworkVersionString(string path)
        {
            AppDomainSetup appDomainSetup = new AppDomainSetup();
            appDomainSetup.ApplicationBase = Path.GetDirectoryName(Path.GetFullPath(path));
            appDomainSetup.LoaderOptimization = LoaderOptimization.MultiDomainHost;

            if (File.Exists(path))
            {
                AppDomain appDomain = null;

                try
                {
                    appDomain = AppDomain.CreateDomain("Framework Version String Domain", null, appDomainSetup);

                    // This is done so that ObjectModel is loaded first in the new AppDomain before any other adapter assembly is used
                    Type typeOfSettingsException = typeof(SettingsException);
                    appDomain.CreateInstanceFromAndUnwrap(
                            typeOfSettingsException.Assembly.Location,
                            typeOfSettingsException.FullName,
                            false,
                            BindingFlags.Default,
                            null,
                            null,
                            null,
                            null);

                    Type typeOfAssemblyLoadWorker = typeof(AssemblyLoadWorker);

                    var assemblyLoadWorker =
                        (AssemblyLoadWorker)
                        appDomain.CreateInstanceFromAndUnwrap(
                            typeOfAssemblyLoadWorker.Assembly.Location,
                            typeOfAssemblyLoadWorker.FullName,
                            false,
                            BindingFlags.Default,
                            null,
                            null,
                            null,
                            null);

                    return assemblyLoadWorker.GetTargetFrameworkVersionStringFromPath(path);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    if (appDomain != null)
                    {
                        AppDomain.Unload(appDomain);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get the Version for the target framework version string
        /// </summary>
        /// <param name="version">Target framework string</param>
        /// <returns>Framework Version</returns>
        internal static Version GetTargetFrameworkVersionFromVersionString(string version)
        {
            if (version.Length > PlatformServices.Constants.DotNetFrameWorkStringPrefix.Length + 1)
            {
                string versionPart = version.Substring(PlatformServices.Constants.DotNetFrameWorkStringPrefix.Length + 1);
                return new Version(versionPart);
            }

            return defaultVersion;
        }

        /// <summary>
        /// Set configuration file on the parameter appDomain.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal static void SetConfigurationFile(AppDomainSetup appDomainSetup, string testSource)
        {
            var configFile = new DeploymentUtility().GetConfigFile(testSource);
            
            if (!string.IsNullOrEmpty(configFile))
            {
                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("UnitTestAdapter: Using configuration file {0} for testSource {1}.", configFile, testSource);
                }
                appDomainSetup.ConfigurationFile = Path.GetFullPath(configFile);

                try
                {
                    // Add redirection of the built 14.0 Object Model assembly to the current version if that is not 14.0
                    var currentVersionOfObjectModel = typeof(TestCase).Assembly.GetName().Version.ToString();
                    if (!string.Equals(currentVersionOfObjectModel, ObjectModelVersionBuiltAgainst))
                    {
                        var configurationBytes = AddObjectModelRedirectAndConvertToByteArray(configFile);
                        appDomainSetup.SetConfigurationBytes(configurationBytes);
                    }
                }
                catch (Exception ex)
                {
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error("Exception hit while adding binding redirects to test source config file. Exception : {0}", ex);
                    }
                }
            }
            else
            {
                // Use the current domains configuration setting.
                appDomainSetup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            }
        }


        /// <summary>
        /// Adds UTF assembly redirect and convert it to a byte array
        /// </summary>
        /// <param name="configFile"> The config File. </param>
        /// <returns> The <see cref="byte"/> array. </returns>
        private static byte[] AddObjectModelRedirectAndConvertToByteArray(string configFile)
        {
            XmlDocument doc = new XmlDocument();
            if (!string.IsNullOrEmpty(configFile?.Trim()))
            {
                using (var xmlReader = new XmlTextReader(configFile))
                {
                    xmlReader.DtdProcessing = DtdProcessing.Prohibit;
                    xmlReader.XmlResolver = null;
                    doc.Load(xmlReader);
                }
            }
            var configurationElement = FindOrCreateElement(doc, doc, "configuration");
            var assemblyBindingSection = FindOrCreateAssemblyBindingSection(doc, configurationElement);
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            assemblyName.Name = "Microsoft.VisualStudio.TestPlatform.ObjectModel";
            var currentVersion = typeof(TestCase).Assembly.GetName().Version.ToString();
            AddAssemblyBindingRedirect(doc, assemblyBindingSection, assemblyName, ObjectModelVersionBuiltAgainst, currentVersion);
            using (var ms = new MemoryStream())
            {
                doc.Save(ms);
                return ms.ToArray();
            }
        }

        private static XmlElement FindOrCreateElement(XmlDocument doc, XmlNode parent, string name)
        {
            var ret = parent[name];

            if (ret != null)
            {
                return ret;
            }

            ret = doc.CreateElement(name, parent.NamespaceURI);
            parent.AppendChild(ret);
            return ret;
        }

        private static XmlElement FindOrCreateAssemblyBindingSection(XmlDocument doc, XmlElement configurationElement)
        {
            // Each section must be created with the xmlns specified so that
            // we don't end up with xmlns="" on each element.

            // Find or create the runtime section (this one should not have an xmlns on it).
            var runtimeSection = FindOrCreateElement(doc, configurationElement, "runtime");

            // Use the assemblyBinding section if it exists; otherwise, create one.
            var assemblyBindingSection = runtimeSection["assemblyBinding"];
            if (assemblyBindingSection != null)
            {
                return assemblyBindingSection;
            }
            assemblyBindingSection = doc.CreateElement("assemblyBinding", XmlNamespace);
            runtimeSection.AppendChild(assemblyBindingSection);
            return assemblyBindingSection;
        }

        /// <summary>
        /// Add an assembly binding redirect entry to the config file.
        /// </summary>
        /// <param name="doc"> The doc. </param>
        /// <param name="assemblyBindingSection"> The assembly Binding Section. </param>
        /// <param name="assemblyName"> The assembly Name. </param>
        /// <param name="fromVersion"> The from Version. </param>
        /// <param name="toVersion"> The to Version. </param>
        private static void AddAssemblyBindingRedirect(XmlDocument doc, XmlElement assemblyBindingSection,
            AssemblyName assemblyName,
            string fromVersion,
            string toVersion)
        {
            Debug.Assert(assemblyName != null);
            if (assemblyName == null)
            {
                throw new ArgumentNullException("assemblyName");
            }


            // Convert the public key token into a string.
            StringBuilder publicKeyTokenString = null;
            var publicKeyToken = assemblyName.GetPublicKeyToken();
            if (null != publicKeyToken)
            {
                publicKeyTokenString = new StringBuilder(publicKeyToken.GetLength(0) * 2);
                for (var i = 0; i < publicKeyToken.GetLength(0); i++)
                {
                    publicKeyTokenString.AppendFormat(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0:x2}",
                        new object[] { publicKeyToken[i] });
                }
            }

            // Get the culture as a string.
            var cultureString = assemblyName.CultureInfo.ToString();
            if (string.IsNullOrEmpty(cultureString))
            {
                cultureString = "neutral";
            }

            // Add the dependentAssembly section.
            var dependentAssemblySection = doc.CreateElement("dependentAssembly", XmlNamespace);
            assemblyBindingSection.AppendChild(dependentAssemblySection);

            // Add the assemblyIdentity element.
            var assemblyIdentityElement = doc.CreateElement("assemblyIdentity", XmlNamespace);
            assemblyIdentityElement.SetAttribute("name", assemblyName.Name);
            if (null != publicKeyTokenString)
            {
                assemblyIdentityElement.SetAttribute("publicKeyToken", publicKeyTokenString.ToString());
            }
            assemblyIdentityElement.SetAttribute("culture", cultureString);
            dependentAssemblySection.AppendChild(assemblyIdentityElement);

            var bindingRedirectElement = doc.CreateElement("bindingRedirect", XmlNamespace);
            bindingRedirectElement.SetAttribute("oldVersion", fromVersion);
            bindingRedirectElement.SetAttribute("newVersion", toVersion);
            dependentAssemblySection.AppendChild(bindingRedirectElement);
        }
    }
}
