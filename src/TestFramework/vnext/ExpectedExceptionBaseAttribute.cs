// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// Base class for attributes that specify to expect an exception from a unit test
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    [Obsolete("ExpectedExceptionBaseAttribute is going to be depricated in next update. Please dont use it.")]
    public abstract class ExpectedExceptionBaseAttribute : Attribute
    {
        #region Fields

        /// <summary>
        /// Message to include in the test result if the test fails due to not throwing an exception
        /// </summary>
        private string noExceptionMessage;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes with a default no-exception message
        /// </summary>
        protected ExpectedExceptionBaseAttribute()
            : this(string.Empty)
        {
            this.noExceptionMessage = string.Empty;
        }

        /// <summary>
        /// Initializes the no-exception message
        /// </summary>
        /// <param name="noExceptionMessage">
        /// Message to include in the test result if the test fails due to not throwing an
        /// exception
        /// </param>
        protected ExpectedExceptionBaseAttribute(string noExceptionMessage)
        {
            this.noExceptionMessage =
                noExceptionMessage == null ?
                    string.Empty :
                    noExceptionMessage.Trim();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the noMessageException as it is initialized. This method is used by
        /// ExpectedExceptionAttribute so that it can use the base class' field instead of having a
        /// field of its own, to get the message requested by the user.
        /// </summary>
        /// <returns>No Exception Message string</returns>
        internal string GetNoExceptionMessage()
        {
            return this.noExceptionMessage;
        }

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
