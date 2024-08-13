// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}
