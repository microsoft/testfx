// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Operations on the TestContext object that is implemented differently for each platform.
/// </summary>
public interface ITestContext
{
    /// <summary>
    /// Gets the inner test context object.
    /// </summary>
    TestContext Context { get; }

    /// <summary>
    /// Returns whether property with parameter name is present.
    /// </summary>
    /// <param name="propertyName"> The property Name. </param>
    /// <param name="propertyValue"> The property Value. </param>
    /// <returns> True if the property is present. </returns>
    bool TryGetPropertyValue(string propertyName, out object? propertyValue);

    /// <summary>
    /// Adds the parameter name/value pair to property bag.
    /// </summary>
    /// <param name="propertyName"> The property Name. </param>
    /// <param name="propertyValue"> The property Value. </param>
    void AddProperty(string propertyName, string propertyValue);

    /// <summary>
    /// Sets the outcome of a Test Method in the TestContext.
    /// </summary>
    /// <param name="outcome"> The outcome. </param>
    void SetOutcome(UnitTestOutcome outcome);

    /// <summary>
    /// Set data row for particular run of TestMethod.
    /// </summary>
    /// <param name="dataRow">data row.</param>
    void SetDataRow(object? dataRow);

    /// <summary>
    /// Set connection for TestContext.
    /// </summary>
    /// <param name="dbConnection">db Connection.</param>
    void SetDataConnection(object? dbConnection);

    /// <summary>
    /// Gets the attached Result files.
    /// </summary>
    /// <returns>
    /// The list of result files.
    /// </returns>
    IList<string>? GetResultFiles();

    /// <summary>
    /// Gets the diagnostics messages written to TestContext.WriteLine().
    /// </summary>
    /// <returns>The test context messages added so far.</returns>
    string? GetDiagnosticMessages();

    /// <summary>
    /// Clears the previous testContext writeline messages.
    /// </summary>
    void ClearDiagnosticMessages();
}
