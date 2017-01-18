// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Xml;
    
    internal class XmlUtilities
    {
        private const string XmlNamespace = "urn:schemas-microsoft-com:asm.v1";

        /// <summary>
        /// Adds assembly redirection and converts the resulting config file to a byte array.
        /// </summary>
        /// <param name="configFile"> The config File. </param>
        /// <returns> A byte array of the config file with the redirections added. </returns>
        internal byte[] AddAssemblyRedirection(string configFile, AssemblyName assemblyName, string oldVersion, string newVersion)
        {
            var doc = this.GetXmlDocument(configFile);

            var configurationElement = FindOrCreateElement(doc, doc, "configuration");
            var assemblyBindingSection = FindOrCreateAssemblyBindingSection(doc, configurationElement);
            AddAssemblyBindingRedirect(doc, assemblyBindingSection, assemblyName, oldVersion , newVersion);
            using (var ms = new MemoryStream())
            {
                doc.Save(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        ///  Gets the Xml document from the config file. This is virtual for unit testing.
        /// </summary>
        /// <param name="configFile"></param>
        /// <returns></returns>
        internal virtual XmlDocument GetXmlDocument(string configFile)
        {
            var doc = new XmlDocument();
            if (!string.IsNullOrEmpty(configFile?.Trim()))
            {
                using (var xmlReader = new XmlTextReader(configFile))
                {
                    xmlReader.DtdProcessing = DtdProcessing.Prohibit;
                    xmlReader.XmlResolver = null;
                    doc.Load(xmlReader);
                }
            }

            return doc;
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
