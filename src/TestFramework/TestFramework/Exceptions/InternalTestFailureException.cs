// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || NETSTANDARD2_0
using System.ComponentModel;
using System.Runtime.Serialization;
#endif

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// InternalTestFailureException class. Used to indicate internal failure for a test case.
/// </summary>
/// <remarks>
/// This class is only added to preserve source compatibility with the V1 framework.
/// For all practical purposes either use AssertFailedException/AssertInconclusiveException.
/// </remarks>
#if RELEASE
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
#endif
[Serializable]
public class InternalTestFailureException : UnitTestAssertException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
    /// </summary>
    /// <param name="msg"> The exception message. </param>
    /// <param name="ex"> The exception. </param>
    public InternalTestFailureException(string msg, Exception ex)
        : base(msg, ex)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
    /// </summary>
    /// <param name="msg"> The exception message. </param>
    public InternalTestFailureException(string msg)
        : base(msg)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
    /// </summary>
    public InternalTestFailureException()
        : base()
    {
    }

#if NETFRAMEWORK || NETSTANDARD2_0
    /// <summary>
    /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
    [Obsolete(Constants.LegacyFormatterImplementationMessage)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected InternalTestFailureException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
#endif
}
