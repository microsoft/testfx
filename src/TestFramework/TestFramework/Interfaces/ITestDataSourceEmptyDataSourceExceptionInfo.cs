// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal interface ITestDataSourceEmptyDataSourceExceptionInfo
{
    /// <summary>
    /// Returns the method/property name accessed by this data source.
    /// For example, for DynamicDataAttribute, that will be attribute argument.
    /// </summary>
    string? GetPropertyOrMethodNameForEmptyDataSourceException();

    /// <summary>
    /// Returns the type name on which the method/property accessed by this data source exists.
    /// </summary>
    string? GetPropertyOrMethodContainerTypeNameForEmptyDataSourceException(MethodInfo testMethodInfo);
}
