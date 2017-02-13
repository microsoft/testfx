// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// Base class for Framework Exceptions.
    /// </summary>
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
        protected UnitTestAssertException(string msg, Exception ex) : base(msg, ex)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestAssertException"/> class.
        /// </summary>
        /// <param name="msg"> The message. </param>
        protected UnitTestAssertException(string msg) : base(msg)
        {
        }
    }

    /// <summary>
    /// AssertFailedException class. Used to indicate failure for a test case
    /// </summary>
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
        public AssertFailedException(string msg) : base(msg)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertFailedException"/> class.
        /// </summary>
        public AssertFailedException() : base()
        {
        }

    }

    /// <summary>
    /// The assert inconclusive exception.
    /// </summary>
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
    
    /// <summary>
    /// InternalTestFailureException class. Used to indicate internal failure for a test case
    /// </summary>
    /// <remarks> 
    /// This class is only added to preserve source compatibility with the V1 framework. 
    /// For all practical purposes either use AssertFailedException/AssertInconclusiveException.
    /// </remarks>
    public class InternalTestFailureException : UnitTestAssertException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
        /// </summary>
        /// <param name="msg"> The exception message. </param>
        /// <param name="ex"> The exception. </param>
        public InternalTestFailureException(string msg, Exception ex) : base(msg, ex)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
        /// </summary>
        /// <param name="msg"> The exception message. </param>
        public InternalTestFailureException(string msg) : base(msg)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalTestFailureException"/> class.
        /// </summary>
        public InternalTestFailureException() : base()
        {
        }
    }
}
