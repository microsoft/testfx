// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test cleanup attribute marks methods that are executed after every test marked with a <see cref="TestMethodAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestCleanupAttribute : Attribute;
