// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Attribute that specifies to expect an exception of the specified type.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExpectedExceptionAttribute : ExpectedExceptionBaseAttribute
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpectedExceptionAttribute"/> class with the expected type.
    /// </summary>
    /// <param name="exceptionType">Type of the expected exception.</param>
    public ExpectedExceptionAttribute(Type exceptionType)
        : this(exceptionType, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpectedExceptionAttribute"/> class with
    /// the expected type and the message to include when no exception is thrown by the test.
    /// </summary>
    /// <param name="exceptionType">Type of the expected exception.</param>
    /// <param name="noExceptionMessage">
    /// Message to include in the test result if the test fails due to not throwing an exception.
    /// </param>
    public ExpectedExceptionAttribute(Type exceptionType, string noExceptionMessage)
        : base(noExceptionMessage)
    {
        if (exceptionType == null)
        {
            throw new ArgumentNullException(nameof(exceptionType));
        }

        if (!typeof(Exception).IsAssignableFrom(exceptionType))
        {
            throw new ArgumentException(
                    FrameworkMessages.UTF_ExpectedExceptionTypeMustDeriveFromException,
                    nameof(exceptionType));
        }

        ExceptionType = exceptionType;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating the Type of the expected exception.
    /// </summary>
    public Type ExceptionType { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow types derived from the type of the expected exception to
    /// qualify as expected.
    /// </summary>
    public bool AllowDerivedTypes { get; set; }

    /// <summary>
    /// Gets the message to include in the test result if the test fails due to not throwing an exception.
    /// </summary>
    protected internal override string NoExceptionMessage => string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.UTF_TestMethodNoException,
                ExceptionType.FullName,
                SpecifiedNoExceptionMessage);

    #endregion

    #region Methods

    /// <summary>
    /// Verifies that the type of the exception thrown by the unit test is expected.
    /// </summary>
    /// <param name="exception">The exception thrown by the unit test.</param>
    protected internal override void Verify(Exception exception)
    {
        DebugEx.Assert(exception != null, "'exception' is null");

        Type thrownExceptionType = exception.GetType();
        if (AllowDerivedTypes)
        {
            if (!ExceptionType.IsAssignableFrom(thrownExceptionType))
            {
                // If the exception is an AssertFailedException or an AssertInconclusiveException, then re-throw it to
                // preserve the test outcome and error message
                RethrowIfAssertException(exception);

                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.UTF_TestMethodWrongExceptionDerivedAllowed,
                    thrownExceptionType.FullName,
                    ExceptionType.FullName,
                    UtfHelper.GetExceptionMsg(exception));

                // TODO: Change this type to a more specific type
#pragma warning disable CA2201 // Do not raise reserved exception types
                throw new Exception(message);
#pragma warning restore CA2201 // Do not raise reserved exception types
            }
        }
        else
        {
            if (thrownExceptionType != ExceptionType)
            {
                // If the exception is an AssertFailedException or an AssertInconclusiveException, then re-throw it to
                // preserve the test outcome and error message
                RethrowIfAssertException(exception);

                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.UTF_TestMethodWrongException,
                    thrownExceptionType.FullName,
                    ExceptionType.FullName,
                    UtfHelper.GetExceptionMsg(exception));

                // TODO: Change this type to a more specific type
#pragma warning disable CA2201 // Do not raise reserved exception types
                throw new Exception(message);
#pragma warning restore CA2201 // Do not raise reserved exception types
            }
        }
    }

    #endregion
}
