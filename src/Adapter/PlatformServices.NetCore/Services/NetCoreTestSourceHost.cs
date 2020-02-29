// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

    /// <summary>
    /// A host that loads the test source
    /// </summary>
    public class TestSourceHost : ITestSourceHost
    {
        private string sourceFileName;
        private string currentDirectory = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestSourceHost"/> class.
        /// </summary>
        /// <param name="sourceFileName"> The source file name. </param>
        /// <param name="runSettings"> The run-settings provided for this session. </param>
        /// <param name="frameworkHandle"> The handle to the test platform. </param>
        public TestSourceHost(string sourceFileName, IRunSettings runSettings, IFrameworkHandle frameworkHandle)
        {
            this.sourceFileName = sourceFileName;

            // Set the environment context.
            this.SetContext(sourceFileName);
        }

        /// <summary>
        /// Setup the isolation host.
        /// </summary>
        public void SetupHost()
        {
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.ResetContext();
        }

        /// <summary>
        /// Creates an instance of a given type in the test source host.
        /// </summary>
        /// <param name="type"> The type that needs to be created in the host. </param>
        /// <param name="args">The arguments to pass to the constructor.
        /// This array of arguments must match in number, order, and type the parameters of the constructor to invoke.
        /// Pass in null for a constructor with no arguments.
        /// </param>
        /// <returns>  An instance of the type created in the host.
        /// <see cref="object"/>.
        /// </returns>
        public object CreateInstanceForType(Type type, object[] args)
        {
            return Activator.CreateInstance(type, args);
        }

        /// <summary>
        /// Sets context required for running tests.
        /// </summary>
        /// <param name="source">
        /// source parameter used for setting context
        /// </param>
        private void SetContext(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            Exception setWorkingDirectoryException = null;
            this.currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(source));
            }
            catch (IOException ex)
            {
                setWorkingDirectoryException = ex;
            }
            catch (System.Security.SecurityException ex)
            {
                setWorkingDirectoryException = ex;
            }

            if (setWorkingDirectoryException != null)
            {
                EqtTrace.Error("MSTestExecutor.SetWorkingDirectory: Failed to set the working directory to '{0}'. {1}", Path.GetDirectoryName(source), setWorkingDirectoryException);
            }
        }

        /// <summary>
        /// Resets the context as it was before calling SetContext()
        /// </summary>
        private void ResetContext()
        {
            if (!string.IsNullOrEmpty(this.currentDirectory))
            {
                Directory.SetCurrentDirectory(this.currentDirectory);
            }
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
