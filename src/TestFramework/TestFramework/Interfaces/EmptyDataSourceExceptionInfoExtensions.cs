// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class EmptyDataSourceExceptionInfoExtensions
{
    internal static ArgumentException GetExceptionForEmptyDataSource(this ITestDataSource dataSource, MethodInfo testMethodInfo)
        => dataSource is ITestDataSourceEmptyDataSourceExceptionInfo info
            ? info.GetExceptionForEmptyDataSource(testMethodInfo)
            : new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    FrameworkMessages.DynamicDataIEnumerableEmpty,
                    "GetData",
                    dataSource.GetType().Name));

    private static ArgumentException GetExceptionForEmptyDataSource(this ITestDataSourceEmptyDataSourceExceptionInfo info, MethodInfo testMethodInfo)
        => new(
            string.Format(
                CultureInfo.InvariantCulture,
                FrameworkMessages.DynamicDataIEnumerableEmpty,
                info.GetPropertyOrMethodNameForEmptyDataSourceException(),
                info.GetPropertyOrMethodContainerTypeNameForEmptyDataSourceException(testMethodInfo)));
}
