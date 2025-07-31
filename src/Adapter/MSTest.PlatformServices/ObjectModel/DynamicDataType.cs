// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.PlatformServices.ObjectModel;

/// <summary>
/// The kind of dynamic data.
/// </summary>
internal enum DynamicDataType : int
{
    /// <summary>
    /// Not a dynamic data.
    /// </summary>
    None = 0,

    /// <summary>
    /// Dynamic data from <see cref="Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource"/>.
    /// </summary>
    ITestDataSource = 1,
}
