// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    /// <summary>
    /// The file operations.
    /// </summary>
    public class FileOperations : IFileOperations
    {
        /// <summary>
        /// Loads an assembly.
        /// </summary>
        /// <param name="assemblyName"> The assembly name. </param>
        /// <returns> The <see cref="Assembly"/>. </returns>
        public Assembly LoadAssembly(string assemblyName, bool isReflectionOnly)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assemblyName);
            return Assembly.Load(new AssemblyName(fileNameWithoutExtension));
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
                fileExists = (searchTask.Result != null);
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
            // Navigation session for UWP is handled by Desktop Adapter
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
            // Initiate values and bail out.
            fileName = null;
            minLineNumber = -1;
        }

        /// <summary>
        /// Dispose's the navigation session instance.
        /// </summary>
        /// <param name="navigationSession"> The navigation session. </param>
        public void DisposeNavigationSession(object navigationSession)
        {
            // Do nothing.
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
            return assemblyFileName;
        }
    }
}
