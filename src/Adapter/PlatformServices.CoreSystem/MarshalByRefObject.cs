// Copyright (c) Microsoft. All rights reserved.

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
    }
}
