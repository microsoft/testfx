// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// The file operations.
    /// </summary>
    public class FileOperations : IFileOperations
    {
        private static MethodInfo methodGetNavigationData;
        private static PropertyInfo propertyFileName;
        private static PropertyInfo propertyMinLineNumber;
        private static Type typeDiaSession;
        private static Type typeDiaNavigationData;

        /// <summary>
        /// Initializes static members of the <see cref="FileOperations"/> class.
        /// </summary>
        /// <remarks>Initializes DiaSession.</remarks>
        static FileOperations()
        {
            const string diaSessionTypeName = "Microsoft.VisualStudio.TestPlatform.ObjectModel.DiaSession, Microsoft.VisualStudio.TestPlatform.ObjectModel";
            const string diaNavigationDataTypeName = "Microsoft.VisualStudio.TestPlatform.ObjectModel.DiaNavigationData,  Microsoft.VisualStudio.TestPlatform.ObjectModel";

            Initialize(diaSessionTypeName, diaNavigationDataTypeName);
        }

        /// <summary>
        /// Loads an assembly.
        /// </summary>
        /// <param name="assemblyName"> The assembly name. </param>
        /// <param name="isReflectionOnly">Indicates whether this should be a reflection only load.</param>
        /// <returns> The <see cref="Assembly"/>. </returns>
        /// <exception cref="NotImplementedException"> This is currently not implemented. </exception>
        public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
            return Assembly.Load(new AssemblyName(fileNameWithoutExtension));
        }

        /// <summary>
        /// Gets the path to the .DLL of the assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>Path to the .DLL of the assembly.</returns>
        public string GetAssemblyPath(Assembly assembly)
        {
            return null; // TODO: alternative for assembly.Location?
        }

        /// <summary>
        /// Verifies if file exists in context.
        /// </summary>
        /// <param name="assemblyFileName"> The assembly file name. </param>
        /// <returns> true if file exists. </returns>
        /// <exception cref="NotImplementedException"> This is currently not implemented. </exception>
        public bool DoesFileExist(string assemblyFileName)
        {
            // For projectK these assemblies can be created on the fly which means the file might not exist on disk.
            // Depend on Assembly Load failures intead of this validation.
            return true;
        }

        /// <summary>
        /// Creates a Navigation session for the source file.
        /// This is used to get file path and line number information for its components.
        /// </summary>
        /// <param name="source"> The source file. </param>
        /// <returns> A Navigation session instance for the current platform. </returns>
        public object CreateNavigationSession(string source)
        {
            // Create instance only when DiaSession is found in Object Model.
            if (typeDiaSession != null && typeDiaNavigationData != null)
            {
                var messageFormatOnException = string.Join("MSTestDiscoverer:DiaSession: Could not create diaSession for source:", source, ". Reason:{0}");
                return SafeInvoke(() => Activator.CreateInstance(typeDiaSession, source), messageFormatOnException);
            }

            return null;
        }

        /// <summary>
        /// Get's the navigation data for a navigation session.
        /// </summary>
        /// <param name="navigationSession"> The navigation session. </param>
        /// <param name="className"> The class name. </param>
        /// <param name="methodName"> The method name. </param>
        /// <param name="minLineNumber"> The min line number. </param>
        /// <param name="fileName"> The file name. </param>
        public void GetNavigationData(object navigationSession, string className, string methodName, out int minLineNumber, out string fileName)
        {
            // Set default values.
            fileName = null;
            minLineNumber = -1;

            // Get navigation data only when DiaSession is found in Object Model.
            if (typeDiaSession != null && typeDiaNavigationData != null)
            {
                var messageFormatOnException = string.Join("MSTestDiscoverer:DiaSession: Could not get navigation data for class:", className, ". Reason:{0}");
                var data = SafeInvoke(() => methodGetNavigationData.Invoke(navigationSession, new object[] { className, methodName }), messageFormatOnException);

                if (data != null)
                {
                    fileName = (string)propertyFileName?.GetValue(data);
                    minLineNumber = (int)(propertyMinLineNumber?.GetValue(data) ?? -1);
                }
            }
        }

        /// <summary>
        /// Dispose's the navigation session instance.
        /// </summary>
        /// <param name="navigationSession"> The navigation session. </param>
        public void DisposeNavigationSession(object navigationSession)
        {
            var diaSession = navigationSession as IDisposable;
            diaSession?.Dispose();
        }

        /// <summary>
        /// Gets the full file path of an assembly file.
        /// </summary>
        /// <param name="assemblyFileName"> The file name. </param>
        /// <returns> The full file path. </returns>
        public string GetFullFilePath(string assemblyFileName)
        {
            return assemblyFileName;
        }

        /// <summary>
        /// 1. Initializes DiaSession.
        /// 2. Assists in Unit Testing.
        /// </summary>
        /// <param name="diaSession">Type name of  DiaSession class.</param>
        /// <param name="diaNavigationData">Type name of DiaNavigationData class.</param>
        internal static void Initialize(string diaSession, string diaNavigationData)
        {
            typeDiaSession = Type.GetType(diaSession, false);
            typeDiaNavigationData = Type.GetType(diaNavigationData, false);

            if (typeDiaSession != null && typeDiaNavigationData != null)
            {
                methodGetNavigationData = typeDiaSession.GetRuntimeMethod("GetNavigationData", new[] { typeof(string), typeof(string) });
                propertyFileName = typeDiaNavigationData.GetRuntimeProperty("FileName");
                propertyMinLineNumber = typeDiaNavigationData.GetRuntimeProperty("MinLineNumber");
            }
        }

        private static object SafeInvoke<T>(Func<T> action, string messageFormatOnException = null)
        {
            try
            {
                return action.Invoke();
            }
            catch (Exception)
            {
                // todo : Add EqtTrace
            }

            return null;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
