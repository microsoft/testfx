// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// The universal test source host.
    /// </summary>
    public class TestSourceHost : ITestSourceHost
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestSourceHost"/> class.
        /// </summary>
        /// <param name="sourceFileName"> The source file name. </param>
        /// <param name="runSettings"> The run-settings provided for this session. </param>
        /// <param name="frameworkHandle"> The handle to the test platform. </param>
        public TestSourceHost(string sourceFileName, IRunSettings runSettings, IFrameworkHandle frameworkHandle)
        {
        }

        public void SetupHost()
        {
            // Do nothing.
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
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do nothing.
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
