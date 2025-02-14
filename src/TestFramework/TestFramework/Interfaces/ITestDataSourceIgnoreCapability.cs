// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies the capability of a test data source to be ignored and define the ignore reason.
/// </summary>
public interface ITestDataSourceIgnoreCapability
{
    /// <summary>
    /// Gets or sets a reason to ignore the test data source. Setting the property to non-null and non-empty string value will ignore the test data source.
    /// </summary>
    string? IgnoreMessage { get; set; }
}
