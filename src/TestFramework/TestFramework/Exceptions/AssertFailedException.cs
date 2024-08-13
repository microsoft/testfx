// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// AssertFailedException class. Used to indicate failure for a test case.
/// </summary>
[Serializable]
public partial class AssertFailedException : UnitTestAssertException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssertFailedException"/> class.
    /// </summary>
    /// <param name="msg"> The message. </param>
    /// <param name="ex"> The exception. </param>
    public AssertFailedException(string msg, Exception ex)
        : base(msg, ex)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertFailedException"/> class.
    /// </summary>
    /// <param name="msg"> The message. </param>
    public AssertFailedException(string msg)
        : base(msg)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertFailedException"/> class.
    /// </summary>
    public AssertFailedException()
        : base()
    {
    }
}
