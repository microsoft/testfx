// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    /// <summary>
    /// Attribute that specifies to expect an exception of the specified type
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    [Obsolete("ExpectedExceptionAttribute is deprecated. Please use Assert.ThrowsException<>() instead.")]
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

            if (!exceptionType.GetTypeInfo().IsSubclassOf(typeof(Exception)))
            {
                throw new ArgumentException(
                        FrameworkMessages.UTF_ExpectedExceptionTypeMustDeriveFromException,
                        "exceptionType");
            }

            this.ExceptionType = exceptionType;
        }

        #endregion

        #region Properties

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
}
