// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System
{
    using System.Runtime.InteropServices;
    using System.Security;

    /// <summary>
    /// Enables access to objects across application domain boundaries in applications that support remoting.
    /// This is just a dummy implementation so that we can mark types as remote - able in the Portable Adapter.
    /// This would be Type Forwarded to the actual implementation in the desktop platform service only.
    /// </summary>
    [ComVisible(true)]
    public abstract class MarshalByRefObject
    {
        /// <summary>
        /// Returns object to be used for controlling lifetime, null means infinite lifetime.
        /// </summary>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        [SecurityCritical]
        public abstract object InitializeLifetimeService();
    }
}
