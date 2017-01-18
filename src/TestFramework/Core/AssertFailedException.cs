// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// Base class for Framework Exceptions, provides localization trick so that messages are in HA locale.
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
}
