// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Optional capability for a custom <see cref="ITestDataSource"/> to provide richer information when the data source
/// returns no rows. When a data source implements this interface, MSTest uses the returned member and type names to
/// build a more actionable exception message instead of the generic <c>GetData returned empty collection</c> message.
/// </summary>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public interface ITestDataSourceEmptyDataSourceExceptionInfo
{
    /// <summary>
    /// Returns the method/property name accessed by this data source.
    /// For example, for <see cref="DynamicDataAttribute"/>, that will be the attribute argument.
    /// </summary>
    /// <returns>The name of the member that produced the (empty) data, or <see langword="null"/> if unknown.</returns>
    string? GetPropertyOrMethodNameForEmptyDataSourceException();

    /// <summary>
    /// Returns the type name on which the method/property accessed by this data source exists.
    /// </summary>
    /// <param name="testMethodInfo">The test method that the data source is attached to.</param>
    /// <returns>The name of the type that declares the member that produced the (empty) data, or <see langword="null"/> if unknown.</returns>
    string? GetPropertyOrMethodContainerTypeNameForEmptyDataSourceException(MethodInfo testMethodInfo);
}
