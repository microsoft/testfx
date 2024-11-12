// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test method attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class TestMethodAttribute : Attribute
{
    private protected readonly struct ScopedCultureDisposable : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUICulture;

        public ScopedCultureDisposable(string cultureName)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUICulture = CultureInfo.CurrentUICulture;

            var newCulture = new CultureInfo(cultureName);
            // TODO: Should we set both? Should we have different attribute? Same attribute with two arguments?
            CultureInfo.CurrentCulture = newCulture;
            CultureInfo.CurrentUICulture = newCulture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUICulture;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodAttribute"/> class.
    /// </summary>
    public TestMethodAttribute()
    : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodAttribute"/> class.
    /// </summary>
    /// <param name="displayName">
    /// Display name for the test.
    /// </param>
    public TestMethodAttribute(string? displayName) => DisplayName = displayName;

    /// <summary>
    /// Gets display name for the test.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Executes a test method.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
    /// <remarks>Extensions can override this method to customize running a TestMethod.</remarks>
    public virtual TestResult[] Execute(ITestMethod testMethod)
    {
        using (SetCultureForTest(testMethod))
        {
            return [testMethod.Invoke(null)];
        }
    }

    // TestMethodInfo isn't accessible here :/
    // Can we add TestCultureName to the *public* interface?
    // Or should we introduce an internal interface ITestMethod2 : ITestMethod :/
    private protected static ScopedCultureDisposable? SetCultureForTest(ITestMethod testMethod)
        => testMethod is TestMethodInfo testMethodInfo && testMethodInfo.TestCultureName is { } culture
            ? new ScopedCultureDisposable(culture)
            : (ScopedCultureDisposable?)null;
}
