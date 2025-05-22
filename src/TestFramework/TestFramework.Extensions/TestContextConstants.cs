// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Used to store information that is provided to unit tests.
/// </summary>
internal static class TestContextConstants
{
    internal static readonly string FullyQualifiedTestClassNameLabel = nameof(TestContext.FullyQualifiedTestClassName);
    internal static readonly string ManagedTypeLabel = nameof(TestContext.ManagedType);
    internal static readonly string ManagedMethodLabel = nameof(TestContext.ManagedMethod);
    internal static readonly string TestNameLabel = nameof(TestContext.TestName);
#if WINDOWS_UWP || WIN_UI
    internal static readonly string TestRunDirectoryLabel = "TestRunDirectory";
    internal static readonly string DeploymentDirectoryLabel = "DeploymentDirectory";
    internal static readonly string ResultsDirectoryLabel = "ResultsDirectory";
    internal static readonly string TestRunResultsDirectoryLabel = "TestRunResultsDirectory";
    internal static readonly string TestResultsDirectoryLabel = "TestResultsDirectory";
    internal static readonly string TestDirLabel = "TestDir";
    internal static readonly string TestDeploymentDirLabel = "TestDeploymentDir";
    internal static readonly string TestLogsDirLabel = "TestLogsDir";
#else
    internal static readonly string TestRunDirectoryLabel = nameof(TestContext.TestRunDirectory);
    internal static readonly string DeploymentDirectoryLabel = nameof(TestContext.DeploymentDirectory);
    internal static readonly string ResultsDirectoryLabel = nameof(TestContext.ResultsDirectory);
    internal static readonly string TestRunResultsDirectoryLabel = nameof(TestContext.TestRunResultsDirectory);
    internal static readonly string TestResultsDirectoryLabel = nameof(TestContext.TestResultsDirectory);
    [Obsolete("Remove when related property is removed.")]
    internal static readonly string TestDirLabel = nameof(TestContext.TestDir);
    [Obsolete("Remove when related property is removed.")]
    internal static readonly string TestDeploymentDirLabel = nameof(TestContext.TestDeploymentDir);
    [Obsolete("Remove when related property is removed.")]
    internal static readonly string TestLogsDirLabel = nameof(TestContext.TestLogsDir);
#endif
}
