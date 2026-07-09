// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;

/// <summary>
/// Fully-qualified metadata names of MSTest attributes the PoC generator understands.
/// </summary>
internal static class MSTestAttributeNames
{
    public const string UnitTestingNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";

    public const string TestClass = UnitTestingNamespace + ".TestClassAttribute";
    public const string TestMethod = UnitTestingNamespace + ".TestMethodAttribute";
    public const string TestInitialize = UnitTestingNamespace + ".TestInitializeAttribute";
    public const string TestCleanup = UnitTestingNamespace + ".TestCleanupAttribute";
    public const string ClassInitialize = UnitTestingNamespace + ".ClassInitializeAttribute";
    public const string ClassCleanup = UnitTestingNamespace + ".ClassCleanupAttribute";
    public const string AssemblyInitialize = UnitTestingNamespace + ".AssemblyInitializeAttribute";
    public const string AssemblyCleanup = UnitTestingNamespace + ".AssemblyCleanupAttribute";
    public const string DataRow = UnitTestingNamespace + ".DataRowAttribute";
    public const string DynamicData = UnitTestingNamespace + ".DynamicDataAttribute";
    public const string TestCategory = UnitTestingNamespace + ".TestCategoryAttribute";
    public const string TestProperty = UnitTestingNamespace + ".TestPropertyAttribute";
    public const string Priority = UnitTestingNamespace + ".PriorityAttribute";
    public const string Owner = UnitTestingNamespace + ".OwnerAttribute";
    public const string Description = UnitTestingNamespace + ".DescriptionAttribute";
    public const string Ignore = UnitTestingNamespace + ".IgnoreAttribute";
    public const string Timeout = UnitTestingNamespace + ".TimeoutAttribute";
    public const string DoNotParallelize = UnitTestingNamespace + ".DoNotParallelizeAttribute";
    public const string ExpectedException = UnitTestingNamespace + ".ExpectedExceptionAttribute";
    public const string DeploymentItem = UnitTestingNamespace + ".DeploymentItemAttribute";
}
