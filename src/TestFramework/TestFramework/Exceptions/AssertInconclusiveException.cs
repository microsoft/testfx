﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The assert inconclusive exception.
/// </summary>
[Serializable]
public partial class AssertInconclusiveException : UnitTestAssertException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssertInconclusiveException"/> class.
    /// </summary>
    /// <param name="msg"> The message. </param>
    /// <param name="ex"> The exception. </param>
    public AssertInconclusiveException(string msg, Exception ex)
        : base(msg, ex)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertInconclusiveException"/> class.
    /// </summary>
    /// <param name="msg"> The message. </param>
    public AssertInconclusiveException(string msg)
        : base(msg)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertInconclusiveException"/> class.
    /// </summary>
    public AssertInconclusiveException()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertInconclusiveException"/> class.
    /// </summary>
    /// <param name="info">Serialization info.</param>
    /// <param name="context">Streaming context.</param>
#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected AssertInconclusiveException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
