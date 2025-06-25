// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved - The warning is for ValueTask.
/// <summary>
/// This attribute is used to mark test methods.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>
/// <description>
/// When using other attributes like <see cref="DataRowAttribute" /> or <see cref="DynamicDataAttribute" />,
/// the use of <see cref="TestMethodAttribute" /> is still required.
/// </description>
/// </item>
/// <item>
/// <description>
/// Test methods must be:
/// <list type="bullet">
/// <item><description>public, or if <see cref="DiscoverInternalsAttribute"/> is used then it can be internal.</description></item>
/// <item><description>not static</description></item>
/// <item><description>not generic</description></item>
/// <item><description>not abstract</description></item>
/// <item><description>return type is either <see langword="void"/>, <see cref="Task"/>, or <see cref="ValueTask"/>. If <see langword="void"/>, then it shouldn't be <see langword="async"/>.</description></item>
/// </list>
/// </description>
/// </item>
/// </list>
/// </remarks>
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved// XML comment has cref attribute that could not be resolved
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class TestMethodAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMethodAttribute"/> class.
    /// </summary>
    public TestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
    {
        DeclaringFilePath = callerFilePath;
        DeclaringLineNumber = callerLineNumber <= 0 ? null : callerLineNumber;
    }

    /// <summary>
    /// Gets display name for the test.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the strategy for unfolding parameterized tests.
    /// </summary>
    public TestDataSourceUnfoldingStrategy UnfoldingStrategy { get; init; } = TestDataSourceUnfoldingStrategy.Auto;

    /// <summary>
    /// Gets the file path that declares the attribute.
    /// </summary>
    public string DeclaringFilePath { get; }

    /// <summary>
    /// Gets the line number within <see cref="DeclaringFilePath"/> that declares the attribute.
    /// </summary>
    public int? DeclaringLineNumber { get; }

    /// <summary>
    /// Executes a test method.
    /// </summary>
    /// <param name="testMethod">The test method to execute.</param>
    /// <returns>An array of TestResult objects that represent the outcome(s) of the test.</returns>
    /// <remarks>Extensions can override this method to customize running a TestMethod.</remarks>
    public virtual async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        => [await testMethod.InvokeAsync(null).ConfigureAwait(false)];
}
