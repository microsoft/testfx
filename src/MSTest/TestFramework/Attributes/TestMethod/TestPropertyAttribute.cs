// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test property attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class TestPropertyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestPropertyAttribute"/> class.
    /// </summary>
    /// <param name="name">
    /// The name.
    /// </param>
    /// <param name="value">
    /// The value.
    /// </param>
    public TestPropertyAttribute(string name, string value)
    {
        // NOTE : DONT THROW EXCEPTIONS FROM HERE IT WILL CRASH GetCustomAttributes() call
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }
}
