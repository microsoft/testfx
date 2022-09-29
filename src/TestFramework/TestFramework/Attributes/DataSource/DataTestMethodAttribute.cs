// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

/// <summary>
/// Attribute for data driven test where data can be specified in-line.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class DataTestMethodAttribute : TestMethodAttribute
{
}
