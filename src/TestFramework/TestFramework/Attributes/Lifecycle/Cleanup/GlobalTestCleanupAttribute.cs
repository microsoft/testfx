// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A global test cleanup attribute that applies to every test method in the assembly.
/// The method to which this attribute is applied must be public, static, non-generic, has a single parameter of type TestContext, and either returns void or a Task.
/// </summary>
/// <remarks>
/// Multiple methods with this attribute in the assembly is allowed, but there is no guarantee of the order in which they will be executed.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GlobalTestCleanupAttribute : Attribute;
