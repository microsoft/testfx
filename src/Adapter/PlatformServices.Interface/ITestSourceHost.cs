// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
    using System;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

    /// <summary>
    /// A host that loads the test source.This can be in isolation for desktop using an AppDomain or just loading the source in the current context.
    /// </summary>
    public interface ITestSourceHost : IDisposable
    {
        /// <summary>
        /// Creates an instance of a given type in the test source host.
        /// </summary>
        /// <param name="type"> The type that needs to be created in the host. </param>
        /// <param name="args">The arguments to pass to the constructor. 
        /// This array of arguments must match in number, order, and type the parameters of the constructor to invoke. 
        /// Pass in null for a constructor with no arguments.
        /// </param>
        /// <returns> An instance of the type created in the host. </returns>
        /// <remarks> If a type is to be created in isolation then it needs to be a MarshalByRefObject. </remarks>
        object CreateInstanceForType(Type type, object[] args);
    }
}
