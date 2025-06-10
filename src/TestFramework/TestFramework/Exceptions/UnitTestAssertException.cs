// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Base class for Framework Exceptions.
/// </summary>
[Serializable]
public abstract partial class UnitTestAssertException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAssertException"/> class.
    /// </summary>
    protected UnitTestAssertException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAssertException"/> class.
    /// </summary>
    /// <param name="msg"> The message. </param>
    /// <param name="ex"> The exception. </param>
    protected UnitTestAssertException(string msg, Exception ex)
        : base(msg, ex)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAssertException"/> class.
    /// </summary>
    /// <param name="msg"> The message. </param>
    protected UnitTestAssertException(string msg)
        : base(msg)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAssertException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
#if NET8_0_OR_GREATER
    [Obsolete("Legacy serialization support is deprecated since .NET 8", DiagnosticId = "SYSLIB0051")]
#endif
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected UnitTestAssertException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        // Do not remove this as unused, it is used by BinaryFormatter when communicating between tested VisualStudio instance,
        // and the UI testing framework that tests it. Don't attempt testing this in the repository using BinaryFormatter will trigger
        // many compliance issues.
    }
}
