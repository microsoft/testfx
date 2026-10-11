// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test initialize attribute.
/// </summary>
/// <remarks>
/// The method is an instance method in a test class, or a static function in an F# test module.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class TestInitializeAttribute : Attribute;
