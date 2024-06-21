// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

/// <summary>
/// This capability is used to indicate whether or not the test framework supports trx report generation.
/// By supporting trx generation, the test adapter should ensure that some required properties are available
/// for all the nodes.
/// We expect these properties in the node bag:
/// - 1 <c>trxreport.classname</c>
/// - 0..n <c>trxreport.testcategory</c>
/// And, in case of exception, the following extra properties:
/// - <c>trxreport.exceptionmessage</c>
/// - <c>trxreport.exceptionstacktrace</c>.
/// </summary>
public interface ITrxReportCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Gets a value indicating whether indicates if the test framework supports trx report properties enrichment.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Notifies the test framework that the trx report is enabled and trx report properties should be added to the test nodes.
    /// </summary>
    void Enable();
}
