// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// InternalTestFailureException class. Used to indicate internal failure for a test case.
/// </summary>
/// <remarks>
/// This class is only added to preserve source compatibility with the V1 framework.
/// For all practical purposes either use AssertFailedException/AssertInconclusiveException.
/// </remarks>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
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

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected InternalTestFailureException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        // Do not remove this as unused, it is used by BinaryFormatter when communicating between tested VisualStudio instance,
        // and the UI testing framework that tests it. Don't attempt testing this in the repository using BinaryFormatter will trigger
        // many compliance issues.
    }
}
