// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Base class for attributes that specify to expect an exception from a unit test
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class ExpectedExceptionBaseAttribute : Attribute
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectedExceptionBaseAttribute"/> class with a default no-exception message
        /// </summary>
        protected ExpectedExceptionBaseAttribute()
            : this(string.Empty)
        {
            this.SpecifiedNoExceptionMessage = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpectedExceptionBaseAttribute"/> class with a no-exception message
        /// </summary>
        /// <param name="noExceptionMessage">
        /// Message to include in the test result if the test fails due to not throwing an
        /// exception
        /// </param>
        protected ExpectedExceptionBaseAttribute(string noExceptionMessage)
        {
            this.SpecifiedNoExceptionMessage =
                noExceptionMessage == null ?
                    string.Empty :
                    noExceptionMessage.Trim();
        }

        #endregion

        #region Properties

        // TODO: Test Context needs to be put in here for source compat.

        /// <summary>
        /// Gets the message to include in the test result if the test fails due to not throwing an exception
        /// </summary>
        protected internal virtual string NoExceptionMessage
        {
            get
            {
                Debug.Assert(this.SpecifiedNoExceptionMessage != null, "'noExceptionMessage' is null");

                if (string.IsNullOrEmpty(this.SpecifiedNoExceptionMessage))
                {
                    // Provide a default message when none was provided by a derived class
                    return GetDefaultNoExceptionMessage(this.GetType().FullName);
                }

                return this.SpecifiedNoExceptionMessage;
            }
        }

        /// <summary>
        /// Gets the message to include in the test result if the test fails due to not throwing an exception
        /// </summary>
        protected string SpecifiedNoExceptionMessage { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the default no-exception message
        /// </summary>
        /// <param name="expectedExceptionAttributeTypeName">The ExpectedException attribute type name</param>
        /// <returns>The default no-exception message</returns>
        internal static string GetDefaultNoExceptionMessage(string expectedExceptionAttributeTypeName)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.UTF_TestMethodNoExceptionDefault,
                expectedExceptionAttributeTypeName);
        }

        /// <summary>
        /// Determines whether the exception is expected. If the method returns, then it is
        /// understood that the exception was expected. If the method throws an exception, then it
        /// is understood that the exception was not expected, and the thrown exception's message
        /// is included in the test result. The <see cref="Assert"/> class can be used for
        /// convenience. If <see cref="Assert.Inconclusive()"/> is used and the assertion fails,
        /// then the test outcome is set to Inconclusive.
        /// </summary>
        /// <param name="exception">The exception thrown by the unit test</param>
        protected internal abstract void Verify(Exception exception);

        /// <summary>
        /// Rethrow the exception if it is an AssertFailedException or an AssertInconclusiveException
        /// </summary>
        /// <param name="exception">The exception to rethrow if it is an assertion exception</param>
        protected void RethrowIfAssertException(Exception exception)
        {
            if (exception is AssertFailedException)
            {
                throw new AssertFailedException(exception.Message);
            }
            else if (exception is AssertInconclusiveException)
            {
                throw new AssertInconclusiveException(exception.Message);
            }
        }

        #endregion
    }
}
