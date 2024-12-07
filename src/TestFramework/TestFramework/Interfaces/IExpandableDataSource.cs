// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Defines whether the data source can be expanded or not.
/// The expansion represents the capability to treat each data row as its own test which will impact test results
/// and UI representation of the test.
/// </summary>
public interface IExpandableDataSource
{
    bool ExpandDataSource { get; }
}
