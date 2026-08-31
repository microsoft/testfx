// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A sentinel type that matches any value in the same test method parameter position.
/// </summary>
/// <remarks>
/// Use this with <see langword="typeof"/> when specifying exclusions, for example:
/// <c>[ExcludeTestCase(typeof(AnyDataValue), false)]</c>.
/// </remarks>
public static class AnyDataValue
{
}
