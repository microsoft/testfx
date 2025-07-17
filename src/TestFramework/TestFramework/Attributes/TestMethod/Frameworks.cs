// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// An enum that is used with <see cref="FrameworkConditionAttribute"/> to control which .NET frameworks a test method or test class supports or doesn't support.
/// </summary>
[Flags]
public enum Frameworks
{
    /// <summary>
    /// Represents .NET Framework (the full framework on Windows).
    /// </summary>
    NetFramework = 1 << 0,

    /// <summary>
    /// Represents .NET Core 1.x, 2.x, and 3.x.
    /// </summary>
    NetCore = 1 << 1,

    /// <summary>
    /// Represents .NET 5 and later versions (unified platform).
    /// </summary>
    Net = 1 << 2,

    /// <summary>
    /// Represents .NET 6 and later versions.
    /// </summary>
    Net6OrGreater = 1 << 3,

    /// <summary>
    /// Represents .NET 7 and later versions.
    /// </summary>
    Net7OrGreater = 1 << 4,

    /// <summary>
    /// Represents .NET 8 and later versions.
    /// </summary>
    Net8OrGreater = 1 << 5,

    /// <summary>
    /// Represents .NET 9 and later versions.
    /// </summary>
    Net9OrGreater = 1 << 6,
}