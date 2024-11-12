// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Attribute to specify the CultureInfo.CurrentCulture and CultureInfo.CurrentUICulture when running the test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TestCultureAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCultureAttribute"/> class.
    /// </summary>
    /// <param name="cultureName">
    /// The culture to be used for the test. For example, "en-US".
    /// </param>
    public TestCultureAttribute(string cultureName) => CultureName = cultureName;

    /// <summary>
    /// Gets the owner.
    /// </summary>
    public string CultureName { get; }
}
