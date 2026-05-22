// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents a single test that is planned to run (i.e. has been discovered and passed the active
/// filter) in the current test run. Returned by <see cref="ITestRunInfo.PlannedTests"/>.
/// </summary>
/// <remarks>
/// Instances are immutable snapshots produced at the start of test execution for the assembly.
/// They are intended for read-only inspection (for example to decide whether expensive setup is
/// needed in <c>[AssemblyInitialize]</c>); they do not carry execution-time state such as outcome,
/// exceptions, or data-driven rows.
/// </remarks>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public sealed class PlannedTest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlannedTest"/> class.
    /// </summary>
    /// <param name="fullyQualifiedTestClassName">The fully-qualified name of the class declaring the test method.</param>
    /// <param name="testName">The simple name of the test method.</param>
    /// <param name="testDisplayName">The display name of the test, when set; otherwise <see langword="null"/>.</param>
    /// <param name="assemblyPath">The full path of the assembly that declares the test, on disk.</param>
    /// <param name="managedTypeName">The ECMA-335 managed type name for the declaring type, when available.</param>
    /// <param name="managedMethodName">The ECMA-335 managed method name (encoding parameter types) for the test method, when available.</param>
    /// <param name="declaringFilePath">The source file that declares the test, captured at compile-time by <see cref="TestMethodAttribute"/>, when available.</param>
    /// <param name="declaringLineNumber">The line number within <paramref name="declaringFilePath"/> at which the test is declared, when available.</param>
    /// <param name="testCategories">The categories applied via <see cref="TestCategoryAttribute"/>.</param>
    /// <param name="testProperties">The name/value pairs applied via <see cref="TestPropertyAttribute"/>. Multiple entries with the same name are preserved.</param>
    public PlannedTest(
        string fullyQualifiedTestClassName,
        string testName,
        string? testDisplayName,
        string assemblyPath,
        string? managedTypeName,
        string? managedMethodName,
        string? declaringFilePath,
        int? declaringLineNumber,
        IReadOnlyCollection<string> testCategories,
        IReadOnlyCollection<KeyValuePair<string, string>> testProperties)
    {
        FullyQualifiedTestClassName = fullyQualifiedTestClassName ?? throw new ArgumentNullException(nameof(fullyQualifiedTestClassName));
        TestName = testName ?? throw new ArgumentNullException(nameof(testName));
        TestDisplayName = testDisplayName;
        AssemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
        ManagedTypeName = managedTypeName;
        ManagedMethodName = managedMethodName;
        DeclaringFilePath = declaringFilePath;
        DeclaringLineNumber = declaringLineNumber;

        IReadOnlyCollection<string> nonNullTestCategories = testCategories ?? throw new ArgumentNullException(nameof(testCategories));
        IReadOnlyCollection<KeyValuePair<string, string>> nonNullTestProperties = testProperties ?? throw new ArgumentNullException(nameof(testProperties));

        string[] copiedTestCategories = [.. nonNullTestCategories];
        KeyValuePair<string, string>[] copiedTestProperties = [.. nonNullTestProperties];
        TestCategories = copiedTestCategories;
        TestProperties = copiedTestProperties;
    }

    /// <summary>
    /// Gets the fully-qualified name of the class declaring the test method.
    /// Mirrors <see cref="TestContext.FullyQualifiedTestClassName"/>.
    /// </summary>
    public string FullyQualifiedTestClassName { get; }

    /// <summary>
    /// Gets the simple name of the test method. Mirrors <see cref="TestContext.TestName"/>.
    /// </summary>
    public string TestName { get; }

    /// <summary>
    /// Gets the display name of the test, when one was set; otherwise <see langword="null"/>.
    /// Mirrors <see cref="TestContext.TestDisplayName"/>.
    /// </summary>
    public string? TestDisplayName { get; }

    /// <summary>
    /// Gets the full path of the assembly that declares the test, on disk.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the ECMA-335 managed type name for the declaring type, when available.
    /// </summary>
    public string? ManagedTypeName { get; }

    /// <summary>
    /// Gets the ECMA-335 managed method name (encoding parameter types) for the test method,
    /// when available. Use this for unambiguous identification of overloaded methods.
    /// </summary>
    public string? ManagedMethodName { get; }

    /// <summary>
    /// Gets the source file path that declares the test. This value is captured at compile time
    /// by <see cref="TestMethodAttribute"/>; it can be <see langword="null"/> when the test is
    /// declared via a custom test-method attribute that does not propagate the caller information.
    /// </summary>
    public string? DeclaringFilePath { get; }

    /// <summary>
    /// Gets the line number within <see cref="DeclaringFilePath"/> at which the test is declared,
    /// when available; otherwise <see langword="null"/>.
    /// </summary>
    public int? DeclaringLineNumber { get; }

    /// <summary>
    /// Gets the categories applied to this test via <see cref="TestCategoryAttribute"/> at the
    /// method, class, or assembly level.
    /// </summary>
    public IReadOnlyCollection<string> TestCategories { get; }

    /// <summary>
    /// Gets the name/value pairs applied to this test via <see cref="TestPropertyAttribute"/>.
    /// Multiple attributes with the same name are preserved as separate entries.
    /// </summary>
    public IReadOnlyCollection<KeyValuePair<string, string>> TestProperties { get; }
}
