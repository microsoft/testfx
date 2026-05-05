// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo
{
    /// <summary>
    /// Sets the <see cref="TestContext"/> on <paramref name="classInstance"/>.
    /// </summary>
    /// <param name="classInstance">
    /// Reference to instance of TestClass.
    /// </param>
    /// <param name="result">
    /// Reference to instance of <see cref="TestResult"/>.
    /// </param>
    /// <returns>
    /// True if there no exceptions during set context operation.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private bool SetTestContext(object classInstance, TestResult result)
    {
        DebugEx.Assert(classInstance != null, "classInstance != null");
        DebugEx.Assert(result != null, "result != null");

        try
        {
            if (Parent.TestContextProperty != null && Parent.TestContextProperty.CanWrite)
            {
                Parent.TestContextProperty.SetValue(classInstance, TestContext);
            }

            return true;
        }
        catch (Exception ex)
        {
            Exception realException = ex.GetRealException();
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestContextSetError,
                TestClassName,
                realException.GetFormattedExceptionMessage());

            result.Outcome = UnitTestOutcome.Failed;
            StackTraceInformation? stackTraceInfo = realException.GetStackTraceInformation();
            result.TestFailureException = new TestFailedException(UnitTestOutcome.Failed, errorMessage, stackTraceInfo);
        }

        return false;
    }

    /// <summary>
    /// Creates an instance of TestClass. The TestMethod is invoked on this instance.
    /// </summary>
    /// <returns>
    /// An instance of the TestClass.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private object? CreateTestClassInstance()
        => Parent.Constructor.Invoke(Parent.IsParameterlessConstructor ? null : [TestContext]);
}
