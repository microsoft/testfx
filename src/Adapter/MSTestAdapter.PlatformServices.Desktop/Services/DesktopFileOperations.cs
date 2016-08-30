// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// This service is responsible for any file based operations.
    /// </summary>
    public class FileOperations : IFileOperations
    {
        /// <summary>
        /// Loads an assembly into the current context.
        /// </summary>
        /// <param name="assemblyFileName">The name of the assembly.</param>
        /// <returns>A handle to the loaded assembly.</returns>
        public Assembly LoadAssembly(string assemblyFileName)
        {
            // .NetCore currently does not understand ReflectionOnlyLoadFrom. So adding that for desktop would need changes in the core adapter to offload
            // some reflection logic to the platform service apart from assembly resolution logic for dependant dlls where we search for test methods as well.
            return Assembly.LoadFrom(assemblyFileName);
        }

        /// <summary>
        /// Verify if a file exists in the current context.
        /// </summary>
        /// <param name="assemblyFileName"> The assembly file name. </param>
        /// <returns> true if the file exists. </returns>
        public bool DoesFileExist(string assemblyFileName)
        {
            return (SafeInvoke<bool>(() => File.Exists(assemblyFileName)) as bool?) ?? false;
        }

        /// <summary>
        /// Creates a Navigation session for the source file. 
        /// This is used to get file path and line number information for its components.
        /// </summary>
        /// <param name="source"> The source file. </param>
        /// <returns> A Navigation session instance for the current platform. </returns>
        public object CreateNavigationSession(string source)
        {
            var messageFormatOnException =
                string.Join("MSTestDiscoverer:DiaSession: Could not create diaSession for source:", source, ". Reason:{0}");
            return SafeInvoke<DiaSession>(() => new DiaSession(source), messageFormatOnException) as DiaSession;
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
            fileName = null;
            minLineNumber = -1;

            var diasession = navigationSession as DiaSession;
            var navigationData = diasession?.GetNavigationData(className, methodName);

            if (navigationData != null)
            {
                minLineNumber = navigationData.MinLineNumber;
                fileName = navigationData.FileName;
            }
        }

        /// <summary>
        /// Dispose's the navigation session instance.
        /// </summary>
        /// <param name="navigationSession"> The navigation session. </param>
        public void DisposeNavigationSession(object navigationSession)
        {
            var diasession = navigationSession as DiaSession;
            diasession?.Dispose();
        }

        /// <summary>
        /// Gets the full file path of an assembly file.
        /// </summary>
        /// <param name="assemblyFileName"> The file name. </param>
        /// <returns> The full file path. </returns>
        public string GetFullFilePath(string assemblyFileName)
        {
            return (SafeInvoke<string>(() => Path.GetFullPath(assemblyFileName)) as string) ?? assemblyFileName;
        }

        private static object SafeInvoke<T>(Func<T> action, string messageFormatOnException = null)
        {
            try
            {
                return action.Invoke();
            }
            catch (Exception exception)
            {
                if (string.IsNullOrEmpty(messageFormatOnException))
                {
                    messageFormatOnException = "{0}";
                }

                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, messageFormatOnException, exception.Message);
            }

            return null;
        }
    }
}
