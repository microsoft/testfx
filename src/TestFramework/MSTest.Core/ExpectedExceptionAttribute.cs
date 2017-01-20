// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Attribute that specifies to expect an exception of the specified type
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ExpectedExceptionAttribute : ExpectedExceptionBaseAttribute
    {
        #region Constructors

        /// <summary>
        /// Initializes the expected type
        /// </summary>
        /// <param name="exceptionType">Type of the expected exception</param>
        public ExpectedExceptionAttribute(Type exceptionType)
            : this(exceptionType, string.Empty)
        {
        }

        /// <summary>
        /// Initializes the expected type and the message to include when no exception is thrown by
        /// the test
        /// </summary>
        /// <param name="exceptionType">Type of the expected exception</param>
        /// <param name="noExceptionMessage">
        /// Message to include in the test result if the test fails due to not throwing an exception
        /// </param>
        public ExpectedExceptionAttribute(Type exceptionType, string noExceptionMessage)
            : base(noExceptionMessage)
        {
            if (exceptionType == null)
            {
                throw new ArgumentNullException("exceptionType");
            }

            if (!typeof(Exception).GetTypeInfo().IsAssignableFrom(exceptionType.GetTypeInfo()))
            {
                throw new ArgumentException(
                        FrameworkMessages.UTF_ExpectedExceptionTypeMustDeriveFromException,
                        "exceptionType");
            }

            this.ExceptionType = exceptionType;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Verifies that the type of the exception thrown by the unit test is expected
        /// </summary>
        /// <param name="exception">The exception thrown by the unit test</param>
        protected internal override void Verify(Exception exception)
        {
            Debug.Assert(exception != null, "'exception' is null");

            Type thrownExceptionType = exception.GetType();
            if (this.AllowDerivedTypes)
            {
                if (!this.ExceptionType.GetTypeInfo().IsAssignableFrom(thrownExceptionType.GetTypeInfo()))
                {
                    // If the exception is an AssertFailedException or an AssertInconclusiveException, then rethrow it to
                    // preserve the test outcome and error message
                    this.RethrowIfAssertException(exception);

                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.UTF_TestMethodWrongExceptionDerivedAllowed,
                        thrownExceptionType.FullName,
                        this.ExceptionType.FullName,
                        UtfHelper.GetExceptionMsg(exception));
                    throw new Exception(message);
                }
            }
            else
            {
                if (thrownExceptionType != this.ExceptionType)
                {
                    // If the exception is an AssertFailedException or an AssertInconclusiveException, then rethrow it to
                    // preserve the test outcome and error message
                    this.RethrowIfAssertException(exception);

                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.UTF_TestMethodWrongException,
                        thrownExceptionType.FullName,
                        this.ExceptionType.FullName,
                        UtfHelper.GetExceptionMsg(exception));
                    throw new Exception(message);
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the message to include in the test result if the test fails due to not throwing an exception
        /// </summary>
        protected internal override string NoExceptionMessage
        {
            get
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.UTF_TestMethodNoException,
                    this.ExceptionType.FullName,
                    this.SpecifiedNoExceptionMessage);
            }
        }

        /// <summary>
        /// Gets a value indicating the Type of the expected exception
        /// </summary>
        public Type ExceptionType
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to allow types derived from the type of the expected exception to
        /// qualify as expected
        /// </summary>
        public bool AllowDerivedTypes
        {
            get;
            set;
        }

        #endregion
    }

    /// <summary>
    /// Provides helper functionality for the unit test framework
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
        Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    internal static class UtfHelper
    {
        /// <summary>
        /// Gets the exception messages, including the messages for all inner exceptions
        /// recursively
        /// </summary>
        /// <param name="ex">Exception to get messages for</param>
        /// <returns>string with error message information</returns>
        internal static string GetExceptionMsg(Exception ex)
        {
            Debug.Assert(ex != null, "exception is null");

            StringBuilder result = new StringBuilder();
            bool first = true;
            for (Exception curException = ex;
                 curException != null;
                 curException = curException.InnerException)
            {
                // Get the exception message. Need to check for errors because the Message property
                // may have been overridden by the exception type in user code.
                string msg;
                try
                {
                    msg = curException.Message;
                }
                catch
                {
                    msg = string.Format(
                        CultureInfo.CurrentCulture,
                        FrameworkMessages.UTF_FailedToGetExceptionMessage,
                        curException.GetType());
                }

                result.Append(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}{1}: {2}",
                        first ? String.Empty : " ---> ",
                        curException.GetType(),
                        msg));
                first = false;
            }

            return result.ToString();
        }
    }
}
