// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

/*
 * /!\ WARNING /!\
 * DO NOT USE EQTTRACE IN THIS CLASS AS IT WILL CAUSE LOAD ISSUE BECAUSE OF THE APPDOMAIN
 * ASSEMBLY RESOLVER SETUP.
 */

/// <summary>
/// Utility function for Assembly related info
/// The caller is supposed to create AppDomain and create instance of given class in there.
/// </summary>
internal sealed class AssemblyLoadWorker : MarshalByRefObject
{
    private readonly IAssemblyUtility _assemblyUtility;

    public AssemblyLoadWorker()
        : this(new AssemblyUtility())
    {
    }

    internal AssemblyLoadWorker(IAssemblyUtility assemblyUtility) => _assemblyUtility = assemblyUtility;

    /// <summary>
    /// Returns the full path to the dependent assemblies of the parameter managed assembly recursively.
    /// It does not report GAC assemblies.
    /// </summary>
    /// <param name="assemblyPath"> Path to the assembly file to load from. </param>
    /// <param name="warnings"> The warnings. </param>
    /// <returns> Full path to dependent assemblies. </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public IReadOnlyCollection<string> GetFullPathToDependentAssemblies(string assemblyPath, out IList<string> warnings)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(assemblyPath), "assemblyPath");

        warnings = new List<string>();
        Assembly? assembly;
        try
        {
            // First time we load in LoadFromContext to avoid issues.
            assembly = _assemblyUtility.ReflectionOnlyLoadFrom(assemblyPath);
        }
        catch (Exception ex)
        {
            warnings.Add(ex.Message);
            return Array.Empty<string>(); // Otherwise just return no dependencies.
        }

        DebugEx.Assert(assembly != null, "assembly");

        List<string> result = [];
        HashSet<string> visitedAssemblies =
        [
            assembly.FullName,
        ];

        ProcessChildren(assembly, result, visitedAssemblies, warnings);

        return result;
    }

    /// <summary>
    /// initialize the lifetime service.
    /// </summary>
    /// <returns> The <see cref="object"/>. </returns>
    public override object? InitializeLifetimeService() =>
        // Infinite.
        null;

    /// <summary>
    /// Get the target dotNet framework string for the assembly.
    /// </summary>
    /// <param name="path">Path of the assembly file.</param>
    /// <returns> String representation of the target dotNet framework e.g. .NETFramework,Version=v4.0. </returns>
    internal string GetTargetFrameworkVersionStringFromPath(string path, out string? errorMessage)
    {
        errorMessage = null;
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            Assembly a = _assemblyUtility.ReflectionOnlyLoadFrom(path);
            return GetTargetFrameworkStringFromAssembly(a);
        }
        catch (BadImageFormatException)
        {
            errorMessage = "AssemblyHelper:GetTargetFrameworkVersionString() caught BadImageFormatException. Falling to native binary.";
        }
        catch (Exception ex)
        {
            errorMessage = $"AssemblyHelper:GetTargetFrameworkVersionString() Returning default. Unhandled exception: {ex}.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Get the target dot net framework string for the assembly.
    /// </summary>
    /// <param name="assembly">Assembly from which target framework has to find.</param>
    /// <returns>String representation of the target dot net framework e.g. .NETFramework,Version=v4.0. </returns>
    private static string GetTargetFrameworkStringFromAssembly(Assembly assembly)
    {
        string dotNetVersion = string.Empty;
        foreach (CustomAttributeData data in CustomAttributeData.GetCustomAttributes(assembly))
        {
            if (!(data?.NamedArguments?.Count > 0))
            {
                continue;
            }

            Type declaringType = data.NamedArguments[0].MemberInfo.DeclaringType;
            if (declaringType == null)
            {
                continue;
            }

            string attributeName = declaringType.FullName;
            if (string.Equals(
                attributeName,
                Constants.TargetFrameworkAttributeFullName,
                StringComparison.OrdinalIgnoreCase))
            {
                dotNetVersion = data.ConstructorArguments[0].Value.ToString();
                break;
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
    private void ProcessChildren(Assembly assembly, IList<string> result, ISet<string> visitedAssemblies, IList<string> warnings)
    {
        DebugEx.Assert(assembly != null, "assembly");

        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
        {
            GetDependentAssembliesInternal(reference.FullName, result, visitedAssemblies, warnings);
        }

        // Take care of .netmodule's.
        Module[] modules;
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

                if (!visitedAssemblies.Add(m.Name))
                {
                    // The assembly was already in the set, meaning that we already visited it.
                    continue;
                }

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
    private void GetDependentAssembliesInternal(string assemblyString, IList<string> result, ISet<string> visitedAssemblies, IList<string> warnings)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(assemblyString), "assemblyString");

        if (!visitedAssemblies.Add(assemblyString))
        {
            // The assembly was already in the hashset, so we already visited it.
            return;
        }

        Assembly? assembly;
        try
        {
            string postPolicyAssembly = AppDomain.CurrentDomain.ApplyPolicy(assemblyString);
            DebugEx.Assert(!StringEx.IsNullOrEmpty(postPolicyAssembly), "postPolicyAssembly");

            assembly = _assemblyUtility.ReflectionOnlyLoad(postPolicyAssembly);
            visitedAssemblies.Add(assembly.FullName);   // Just in case.
        }
        catch (Exception ex)
        {
            string warning = string.Format(CultureInfo.CurrentCulture, Resource.MissingDeploymentDependency, assemblyString, ex.Message);
            warnings.Add(warning);
            return;
        }

        result.Add(assembly.Location);

        ProcessChildren(assembly, result, visitedAssemblies, warnings);
    }
}

#endif
