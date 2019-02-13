// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Utility function for Assembly related info
    /// The caller is supposed to create AppDomain and create instance of given class in there.
    /// </summary>
    internal class AssemblyLoadWorker : MarshalByRefObject
    {
        private IAssemblyUtility assemblyUtility;

        public AssemblyLoadWorker()
            : this(new AssemblyUtility())
        {
        }

        internal AssemblyLoadWorker(IAssemblyUtility assemblyUtility)
        {
            this.assemblyUtility = assemblyUtility;
        }

        /// <summary>
        /// Returns the full path to the dependent assemblies of the parameter managed assembly recursively.
        /// It does not report GAC assemblies.
        /// </summary>
        /// <param name="assemblyPath"> Path to the assembly file to load from. </param>
        /// <param name="warnings"> The warnings. </param>
        /// <returns> Full path to dependent assemblies. </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        public string[] GetFullPathToDependentAssemblies(string assemblyPath, out IList<string> warnings)
        {
            Debug.Assert(!string.IsNullOrEmpty(assemblyPath), "assemblyPath");

            warnings = new List<string>();
            Assembly assembly = null;
            try
            {
                // First time we load in LoadFromContext to avoid issues.
                assembly = this.assemblyUtility.ReflectionOnlyLoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                warnings.Add(ex.Message);
                return new string[0]; // Otherwise just return no dependencies.
            }

            Debug.Assert(assembly != null, "assembly");

            List<string> result = new List<string>();
            List<string> visitedAssemblies = new List<string>();

            visitedAssemblies.Add(assembly.FullName);

            this.ProcessChildren(assembly, result, visitedAssemblies, warnings);

            return result.ToArray();
        }

        /// <summary>
        /// initialize the lifetime service.
        /// </summary>
        /// <returns> The <see cref="object"/>. </returns>
        public override object InitializeLifetimeService()
        {
            // Infinite.
            return null;
        }

        /// <summary>
        /// Get the target dotNet framework string for the assembly
        /// </summary>
        /// <param name="path">Path of the assembly file</param>
        /// <returns> String representation of the the target dotNet framework e.g. .NETFramework,Version=v4.0 </returns>
        internal string GetTargetFrameworkVersionStringFromPath(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    Assembly a = this.assemblyUtility.ReflectionOnlyLoadFrom(path);
                    return this.GetTargetFrameworkStringFromAssembly(a);
                }
                catch (BadImageFormatException)
                {
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error("AssemblyHelper:GetTargetFrameworkVersionString() caught BadImageFormatException. Falling to native binary.");
                    }
                }
                catch (Exception ex)
                {
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error("AssemblyHelper:GetTargetFrameworkVersionString() Returning default. Unhandled exception: {0}.", ex);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get the target dot net framework string for the assembly
        /// </summary>
        /// <param name="assembly">Assembly from which target framework has to find</param>
        /// <returns>String representation of the the target dot net framework e.g. .NETFramework,Version=v4.0 </returns>
        private string GetTargetFrameworkStringFromAssembly(Assembly assembly)
        {
            string dotNetVersion = string.Empty;
            foreach (CustomAttributeData data in CustomAttributeData.GetCustomAttributes(assembly))
            {
                if (data?.NamedArguments?.Count > 0)
                {
                    var declaringType = data.NamedArguments[0].MemberInfo.DeclaringType;
                    if (declaringType != null)
                    {
                        string attributeName = declaringType.FullName;
                        if (string.Equals(
                            attributeName,
                            PlatformServices.Constants.TargetFrameworkAttributeFullName,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            dotNetVersion = data.ConstructorArguments[0].Value.ToString();
                            break;
                        }
                    }
                }
            }

            return dotNetVersion;
        }

        /// <summary>
        /// Processes references, modules, satellites.
        /// Fills parameter results.
        /// </summary>
        /// <param name="assembly"> The assembly. </param>
        /// <param name="result"> The result. </param>
        /// <param name="visitedAssemblies"> The visited Assemblies. </param>
        /// <param name="warnings"> The warnings. </param>
        private void ProcessChildren(Assembly assembly, IList<string> result, IList<string> visitedAssemblies, IList<string> warnings)
        {
            Debug.Assert(assembly != null, "assembly");
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                this.GetDependentAssembliesInternal(reference.FullName, result, visitedAssemblies, warnings);
            }

            // Take care of .netmodule's.
            var modules = new Module[0];
            try
            {
                modules = assembly.GetModules();
            }
            catch (FileNotFoundException e)
            {
                string warning = string.Format(CultureInfo.CurrentCulture, Resource.MissingDeploymentDependency, e.FileName, e.Message);
                warnings.Add(warning);
                return;
            }

            // Assembly.GetModules() returns all modules including main one.
            if (modules.Length > 1)
            {
                // The modules must be in the same directory as assembly that references them.
                foreach (Module m in modules)
                {
                    // Module.Name ~ MyModule.netmodule. Module.FullyQualifiedName ~ C:\dir\MyModule.netmodule.
                    string shortName = m.Name;

                    // Note that "MyModule" may contain dots:
                    int dotIndex = shortName.LastIndexOf('.');
                    if (dotIndex > 0)
                    {
                        shortName = shortName.Substring(0, dotIndex);
                    }

                    if (string.Equals(shortName, assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
                    {
                        // This is main assembly module.
                        continue;
                    }

                    if (visitedAssemblies.Contains(m.Name))
                    {
                        continue;
                    }

                    visitedAssemblies.Add(m.Name);

                    if (!File.Exists(m.FullyQualifiedName))
                    {
                        string warning = string.Format(CultureInfo.CurrentCulture, Resource.MissingDeploymentDependencyWithoutReason, m.FullyQualifiedName);
                        warnings.Add(warning);
                        continue;
                    }

                    result.Add(m.FullyQualifiedName);
                }
            }
        }

        /// <summary>
        /// Loads in Load Context. Fills private members.
        /// </summary>
        /// <param name="assemblyString"> Full or partial assembly name passed to Assembly.Load. </param>
        /// <param name="result"> The result. </param>
        /// <param name="visitedAssemblies"> The visited Assemblies. </param>
        /// <param name="warnings"> The warnings. </param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        private void GetDependentAssembliesInternal(string assemblyString, IList<string> result, IList<string> visitedAssemblies, IList<string> warnings)
        {
            Debug.Assert(!string.IsNullOrEmpty(assemblyString), "assemblyString");

            if (visitedAssemblies.Contains(assemblyString))
            {
                return;
            }

            visitedAssemblies.Add(assemblyString);

            Assembly assembly = null;
            try
            {
                string postPolicyAssembly = AppDomain.CurrentDomain.ApplyPolicy(assemblyString);
                Debug.Assert(!string.IsNullOrEmpty(postPolicyAssembly), "postPolicyAssembly");

                assembly = this.assemblyUtility.ReflectionOnlyLoad(postPolicyAssembly);
                visitedAssemblies.Add(assembly.FullName);   // Just in case.
            }
            catch (Exception ex)
            {
                string warning = string.Format(CultureInfo.CurrentCulture, Resource.MissingDeploymentDependency, assemblyString, ex.Message);
                warnings.Add(warning);
                return;
            }

            // As soon as we find GAC or internal assembly we do not look further.
            if (assembly.GlobalAssemblyCache)
            {
                return;
            }

            result.Add(assembly.Location);

            this.ProcessChildren(assembly, result, visitedAssemblies, warnings);
        }
    }
}
