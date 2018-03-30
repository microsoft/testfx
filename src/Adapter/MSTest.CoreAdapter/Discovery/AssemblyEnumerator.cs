// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Text;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

    /// <summary>
    /// Enumerates through all types in the assembly in search of valid test methods.
    /// </summary>
    internal class AssemblyEnumerator : MarshalByRefObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyEnumerator"/> class.
        /// </summary>
        public AssemblyEnumerator()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyEnumerator"/> class.
        /// </summary>
        /// <param name="settings">The settings for the session.</param>
        /// <remarks>Use this constructor when creating this object in a new app domain so the settings for this app domain are set.</remarks>
        public AssemblyEnumerator(MSTestSettings settings)
        {
            // Populate the settings into the domain(Desktop workflow) performing discovery.
            // This would just be resettings the settings to itself in non desktop workflows.
            MSTestSettings.PopulateSettings(settings);
        }

        /// <summary>
        /// Returns object to be used for controlling lifetime, null means infinite lifetime.
        /// </summary>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        [SecurityCritical]
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// Enumerates through all types in the assembly in search of valid test methods.
        /// </summary>
        /// <param name="assemblyFileName"> The assembly file name. </param>
        /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
        /// <returns> A collection of Test Elements. </returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Catching a generic exception since it is a requirement to not abort discovery in case of any errors.")]
        internal ICollection<UnitTestElement> EnumerateAssembly(string assemblyFileName, out ICollection<string> warnings)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(assemblyFileName), "Invalid assembly file name.");

            var warningMessages = new List<string>();
            var tests = new List<UnitTestElement>();

            Assembly assembly;
            if (assemblyFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // We only want to load the source assembly in reflection only context in UWP scenarios where it is always an exe.
                // For normal test assemblies continue loading it in the default context since:
                // 1. There isnt much benefit in terms of Performance loading the assembly in a Reflection Only context during discovery.
                // 2. Loading it in Reflection only context entails a bunch of custom logic to identify custom attributes which is over-kill for normal desktop users.
                assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(
                    assemblyFileName,
                    isReflectionOnly: true);
            }
            else
            {
                assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyFileName, isReflectionOnly: false);
            }

            var types = this.GetTypes(assembly, assemblyFileName, warningMessages);

            foreach (var type in types)
            {
                if (type == null)
                {
                    continue;
                }

                string typeFullName = null;

                try
                {
                    ICollection<string> warningsFromTypeEnumerator;

                    typeFullName = type.FullName;
                    var unitTestCases = this.GetTypeEnumerator(type, assemblyFileName).Enumerate(out warningsFromTypeEnumerator);

                    if (warningsFromTypeEnumerator != null)
                    {
                        warningMessages.AddRange(warningsFromTypeEnumerator);
                    }

                    if (unitTestCases != null)
                    {
                        tests.AddRange(unitTestCases);
                    }
                }
                catch (Exception exception)
                {
                    // If we fail to discover type from a class, then dont abort the discovery
                    // Move to the next type.
                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.CouldNotInspectTypeDuringDiscovery,
                        typeFullName,
                        assemblyFileName,
                        exception.Message);
                    warningMessages.Add(message);

                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                        "AssemblyEnumerator: Exception occured while enumerating type {0}. {1}",
                        typeFullName,
                        exception);
                }
            }

            warnings = warningMessages;
            return tests;
        }

        /// <summary>
        /// Gets the types defined in an assembly.
        /// </summary>
        /// <param name="assembly">The reflected assembly.</param>
        /// <param name="assemblyFileName">The file name of the assembly.</param>
        /// <param name="warningMessages">Contains warnings if any, that need to be passed back to the caller.</param>
        /// <returns>Gets the types defined in the provided assembly.</returns>
        internal Type[] GetTypes(Assembly assembly, string assemblyFileName, ICollection<string> warningMessages)
        {
            var types = new List<Type>();
            try
            {
                types.AddRange(assembly.DefinedTypes.Select(typeinfo => typeinfo.AsType()));
            }
            catch (ReflectionTypeLoadException ex)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(
                    "MSTestExecutor.TryGetTests: Failed to discover tests from {0}. Reason:{1}",
                    assemblyFileName,
                    ex);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning("Exceptions thrown from the Loader :");

                if (ex.LoaderExceptions != null)
                {
                    // If not able to load all type, log a warning and continue with loaded types.
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.TypeLoadFailed,
                        assemblyFileName,
                        this.GetLoadExceptionDetails(ex));

                    warningMessages?.Add(message);

                    foreach (var loaderEx in ex.LoaderExceptions)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning("{0}", loaderEx);
                    }
                }

                return ex.Types;
            }

            return types.ToArray();
        }

        /// <summary>
        /// Formats load exception as multiline string, each line contains load error message.
        /// </summary>
        /// <param name="ex">The exception.</param>
        /// <returns>Returns loader exceptions as a multiline string.</returns>
        internal string GetLoadExceptionDetails(ReflectionTypeLoadException ex)
        {
            Debug.Assert(ex != null, "exception should not be null.");

            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); // Exception -> null.
            var errorDetails = new StringBuilder();

            if (ex.LoaderExceptions != null)
            {
                // Loader exceptions can contain duplicates, leave only unique exceptions.
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    Debug.Assert(loaderException != null, "loader exception should not be null.");
                    var line = string.Format(CultureInfo.CurrentCulture, Resource.EnumeratorLoadTypeErrorFormat, loaderException.GetType(), loaderException.Message);
                    if (!map.ContainsKey(line))
                    {
                        map.Add(line, null);
                        errorDetails.AppendLine(line);
                    }
                }
            }
            else
            {
                errorDetails.AppendLine(ex.Message);
            }

            return errorDetails.ToString();
        }

        /// <summary>
        /// Returns an instance of the <see cref="TypeEnumerator"/> class.
        /// </summary>
        /// <param name="type">The type to enumerate.</param>
        /// <param name="assemblyFileName">The reflected assembly name.</param>
        /// <returns>a TypeEnumerator instance.</returns>
        internal virtual TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName)
        {
            var reflectHelper = new ReflectHelper();
            var typevalidator = new TypeValidator(reflectHelper);
            var testMethodValidator = new TestMethodValidator(reflectHelper);

            return new TypeEnumerator(type, assemblyFileName, reflectHelper, typevalidator, testMethodValidator);
        }
    }
}
