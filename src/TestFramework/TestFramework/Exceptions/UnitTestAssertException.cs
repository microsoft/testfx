// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || NETSTANDARD2_0
using System.ComponentModel;
using System.Runtime.Serialization;
#endif

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

#if NETFRAMEWORK || NETSTANDARD2_0
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestAssertException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    [Obsolete(Constants.LegacyFormatterImplementationMessage)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected UnitTestAssertException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
#endif
}
