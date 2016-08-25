// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

    /// <summary>
    /// The universal test source host.
    /// </summary>
    public class TestSourceHost : ITestSourceHost
    {
        /// <summary>
        /// Creates an instance of a given type in the test source host.
        /// </summary>
        /// <param name="type"> The type that needs to be created in the host. </param>
        /// <param name="args">The arguments to pass to the constructor. 
        /// This array of arguments must match in number, order, and type the parameters of the constructor to invoke. 
        /// Pass in null for a constructor with no arguments.
        /// </param>
        /// <param name="sourceFileName"> The source. </param>
        /// <param name="runSettings"> The run settings provided for this session. </param>
        /// <returns>  An instance of the type created in the host.
        /// <see cref="object"/>.
        /// </returns>
        public object CreateInstanceForType(Type type, object[] args, string sourceFileName, IRunSettings runSettings)
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
}
