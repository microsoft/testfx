// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using global::System;
    using global::System.IO;
    using global::System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// The file operations.
    /// </summary>
    public class FileOperations : IFileOperations
    {
        /// <summary>
        /// Loads an assembly.
        /// </summary>
        /// <param name="assemblyName"> The assembly name. </param>
        /// <param name="isReflectionOnly">
        /// Indicates whether this should be a reflection only load.
        /// </param>
        /// <returns> The <see cref="Assembly"/>. </returns>
        public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
            return Assembly.Load(new AssemblyName(fileNameWithoutExtension));
        }

        /// <summary>
        /// Gets the path to the .DLL of the assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>Path to the .DLL of the assembly.</returns>
        public string GetAssemblyPath(Assembly assembly)
        {
            return assembly.Location;
        }

        /// <summary>
        ///  Verifies if file exists in context.
        /// </summary>
        /// <param name="assemblyFileName"> The assembly file name. </param>
        /// <returns> The <see cref="bool"/>. </returns>
        public bool DoesFileExist(string assemblyFileName)
        {
            var fileExists = false;

            try
            {
                var fileNameWithoutPath = Path.GetFileName(assemblyFileName);
                var searchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileNameWithoutPath).AsTask();
                searchTask.Wait();
                fileExists = searchTask.Result != null;
            }
            catch (Exception)
            {
                // ignore
            }

            return fileExists;
        }

        /// <summary>
        /// Creates a Navigation session for the source file.
        /// This is used to get file path and line number information for its components.
        /// </summary>
        /// <param name="source"> The source file. </param>
        /// <returns> A Navigation session instance for the current platform.
        /// <see cref="INavigationSession"/>.
        /// </returns>
        public object CreateNavigationSession(string source)
        {
            return DiaSessionOperations.CreateNavigationSession(source);
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
            DiaSessionOperations.GetNavigationData(navigationSession, className, methodName, out minLineNumber, out fileName);
        }

        /// <summary>
        /// Dispose's the navigation session instance.
        /// </summary>
        /// <param name="navigationSession"> The navigation session. </param>
        public void DisposeNavigationSession(object navigationSession)
        {
            DiaSessionOperations.DisposeNavigationSession(navigationSession);
        }

        /// <summary>
        /// Gets the full file path of an assembly file.
        /// </summary>
        /// <param name="assemblyFileName"> The assembly file name. </param>
        /// <returns> The full file path
        /// <see cref="string"/>.
        /// </returns>
        public string GetFullFilePath(string assemblyFileName)
        {
            return (SafeInvoke<string>(() => Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, assemblyFileName)) as string) ?? assemblyFileName;
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

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
