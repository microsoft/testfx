// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal interface IDynamicDataOperations
{
    IEnumerable<object[]> GetData(Type? dynamicDataDeclaringType, DynamicDataSourceType dynamicDataSourceType, string dynamicDataSourceName, MethodInfo methodInfo);

    string? GetDisplayName(string? displayName, Type? dynamicDataDisplayNameDeclaringType, MethodInfo methodInfo, object?[]? data);
}
