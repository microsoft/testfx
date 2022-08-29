// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Utilities;

extern alias FrameworkV2Extension;

#if NETCOREAPP
using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
extern alias FrameworkV1;
extern alias FrameworkV2;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestFrameworkV2 = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#endif

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using Moq;

using TestFrameworkV2Extension = FrameworkV2Extension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
#pragma warning disable SA1649 // File name must match first type name
public class DeploymentItemUtilityTests
#pragma warning restore SA1649 // File name must match first type name
{
    internal static readonly TestProperty DeploymentItemsProperty = TestProperty.Register(
        "MSTestDiscoverer.DeploymentItems",
        "DeploymentItems",
        typeof(KeyValuePair<string, string>[]),
        TestPropertyAttributes.Hidden,
        typeof(TestCase));

    private Mock<ReflectionUtility> mockReflectionUtility;
    private DeploymentItemUtility deploymentItemUtility;
    private ICollection<string> warnings;

    private readonly string defaultDeploymentItemPath = @"c:\temp";
    private readonly string defaultDeploymentItemOutputDirectory = "out";

    [TestInitialize]
    public void TestInit()
    {
        mockReflectionUtility = new Mock<ReflectionUtility>();
        deploymentItemUtility = new DeploymentItemUtility(mockReflectionUtility.Object);
        warnings = new List<string>();
    }

    #region GetClassLevelDeploymentItems tests

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReturnEmptyListWhenNoDeploymentItems()
    {
        var deploymentItems = deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), warnings);

        Assert.IsNotNull(deploymentItems);
        Assert.AreEqual(0, deploymentItems.Count);
    }

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReturnADeploymentItem()
    {
        var kvpArray = new[]
                {
                    new KeyValuePair<string, string>(
                        defaultDeploymentItemPath,
                        defaultDeploymentItemOutputDirectory)
                };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), kvpArray);

        var deploymentItems = deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), warnings);
        var expectedDeploymentItems = new DeploymentItem[]
                                          {
                                              new DeploymentItem(
                                                  defaultDeploymentItemPath,
                                                  defaultDeploymentItemOutputDirectory)
                                          };
        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReturnMoreThanOneDeploymentItems()
    {
        var deploymentItemAttributes = new[]
                                           {
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath,
                                                   defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath + "\\temp2",
                                                   defaultDeploymentItemOutputDirectory)
                                           };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), deploymentItemAttributes);

        var deploymentItems =
            deploymentItemUtility.GetClassLevelDeploymentItems(
                typeof(DeploymentItemUtilityTests),
                warnings);

        var expectedDeploymentItems = new DeploymentItem[]
                                          {
                                              new DeploymentItem(
                                                  deploymentItemAttributes[0].Key,
                                                  deploymentItemAttributes[0].Value),
                                              new DeploymentItem(
                                                  deploymentItemAttributes[1].Key,
                                                  deploymentItemAttributes[1].Value)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldNotReturnDuplicateDeploymentItemEntries()
    {
        var deploymentItemAttributes = new[]
                                           {
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath,
                                                   defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath,
                                                   defaultDeploymentItemOutputDirectory)
                                           };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), deploymentItemAttributes);

        var deploymentItems =
            deploymentItemUtility.GetClassLevelDeploymentItems(
                typeof(DeploymentItemUtilityTests),
                warnings);

        var expectedDeploymentItems = new[]
                                          {
                                              new DeploymentItem(
                                                  defaultDeploymentItemPath,
                                                  defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReportWarningsForInvalidDeploymentItems()
    {
        var deploymentItemAttributes = new[]
                                           {
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath,
                                                   defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   null,
                                                   defaultDeploymentItemOutputDirectory)
                                           };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), deploymentItemAttributes);

        var deploymentItems = deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), warnings);

        var expectedDeploymentItems = new DeploymentItem[]
                                          {
                                              new DeploymentItem(
                                                  defaultDeploymentItemPath,
                                                  defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings.ToArray()[0], Resource.DeploymentItemPathCannotBeNullOrEmpty);
    }

    #endregion

    #region GetDeploymentItems tests

    [TestMethod]
    public void GetDeploymentItemsShouldReturnNullOnNoDeploymentItems()
    {
        Assert.IsNull(deploymentItemUtility.GetDeploymentItems(
            typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems"),
            null,
            warnings));
    }

    [TestMethod]
    public void GetDeploymentItemsShouldReturnMethodLevelDeploymentItemsOnly()
    {
        var deploymentItemAttributes = new[]
                                              {
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath,
                                                   defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath + "\\temp2",
                                                   defaultDeploymentItemOutputDirectory)
                                           };
        var memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnNullOnNoDeploymentItems");

        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        var deploymentItems = deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            null,
            warnings);

        CollectionAssert.AreEqual(deploymentItemAttributes, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetDeploymentItemsShouldReturnClassLevelDeploymentItemsOnly()
    {
        // Arrange.
        var classLevelDeploymentItems = new DeploymentItem[]
                                            {
                                                new DeploymentItem(
                                                    defaultDeploymentItemPath,
                                                    defaultDeploymentItemOutputDirectory),
                                                new DeploymentItem(
                                                    defaultDeploymentItemPath + "\\temp2",
                                                    defaultDeploymentItemOutputDirectory)
                                            };

        // Act.
        var deploymentItems = deploymentItemUtility.GetDeploymentItems(
            typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems"),
            classLevelDeploymentItems,
            warnings);

        // Assert.
        var expectedDeploymentItems = new[]
                                          {
                                              new KeyValuePair<string, string>(
                                                  defaultDeploymentItemPath,
                                                  defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  defaultDeploymentItemPath + "\\temp2",
                                                  defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems()
    {
        // Arrange.
        var deploymentItemAttributes = new[]
                                              {
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath,
                                                   defaultDeploymentItemOutputDirectory)
                                           };
        var memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems");
        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        var classLevelDeploymentItems = new[]
                                            {
                                                new DeploymentItem(
                                                    defaultDeploymentItemPath + "\\temp2",
                                                    defaultDeploymentItemOutputDirectory)
                                            };

        // Act.
        var deploymentItems = deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            classLevelDeploymentItems,
            warnings);

        // Assert.
        var expectedDeploymentItems = new KeyValuePair<string, string>[]
                                          {
                                              new KeyValuePair<string, string>(
                                                  defaultDeploymentItemPath,
                                                  defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  defaultDeploymentItemPath + "\\temp2",
                                                  defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItemsWithoutDuplicates()
    {
        // Arrange.
        var deploymentItemAttributes = new[]
                                              {
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath,
                                                   defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   defaultDeploymentItemPath + "\\temp2",
                                                   defaultDeploymentItemOutputDirectory)
                                           };
        var memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems");
        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        var classLevelDeploymentItems = new DeploymentItem[]
                                            {
                                                new DeploymentItem(
                                                    defaultDeploymentItemPath,
                                                    defaultDeploymentItemOutputDirectory),
                                                new DeploymentItem(
                                                    defaultDeploymentItemPath + "\\temp1",
                                                    defaultDeploymentItemOutputDirectory)
                                            };

        // Act.
        var deploymentItems = deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            classLevelDeploymentItems,
            warnings);

        // Assert.
        var expectedDeploymentItems = new KeyValuePair<string, string>[]
                                          {
                                              new KeyValuePair<string, string>(
                                                  defaultDeploymentItemPath,
                                                  defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  defaultDeploymentItemPath + "\\temp2",
                                                  defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  defaultDeploymentItemPath + "\\temp1",
                                                  defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    #endregion

    #region IsValidDeploymentItem tests

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsNull()
    {
        Assert.IsFalse(deploymentItemUtility.IsValidDeploymentItem(null, defaultDeploymentItemOutputDirectory, out var warning));

        StringAssert.Contains(Resource.DeploymentItemPathCannotBeNullOrEmpty, warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsEmpty()
    {
        Assert.IsFalse(deploymentItemUtility.IsValidDeploymentItem(string.Empty, defaultDeploymentItemOutputDirectory, out var warning));

        StringAssert.Contains(Resource.DeploymentItemPathCannotBeNullOrEmpty, warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsNull()
    {
        Assert.IsFalse(deploymentItemUtility.IsValidDeploymentItem(defaultDeploymentItemPath, null, out var warning));

        StringAssert.Contains(Resource.DeploymentItemOutputDirectoryCannotBeNull, warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfSourcePathHasInvalidCharacters()
    {
        Assert.IsFalse(deploymentItemUtility.IsValidDeploymentItem("C:<>", defaultDeploymentItemOutputDirectory, out var warning));

        StringAssert.Contains(
            string.Format(
                Resource.DeploymentItemContainsInvalidCharacters,
                "C:<>",
                defaultDeploymentItemOutputDirectory),
            warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfOutputDirectoryHasInvalidCharacters()
    {
        Assert.IsFalse(deploymentItemUtility.IsValidDeploymentItem(defaultDeploymentItemPath, "<>", out var warning));

        StringAssert.Contains(
            string.Format(
                Resource.DeploymentItemContainsInvalidCharacters,
                defaultDeploymentItemPath,
                "<>"),
            warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsRooted()
    {
        Assert.IsFalse(deploymentItemUtility.IsValidDeploymentItem(defaultDeploymentItemPath, "C:\\temp", out var warning));

        StringAssert.Contains(
           string.Format(
               Resource.DeploymentItemOutputDirectoryMustBeRelative,
               "C:\\temp"),
           warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReturnTrueForAValidDeploymentItem()
    {
        Assert.IsTrue(deploymentItemUtility.IsValidDeploymentItem(defaultDeploymentItemPath, defaultDeploymentItemOutputDirectory, out var warning));

        Assert.IsTrue(string.Empty.Equals(warning));
    }
    #endregion

    #region HasDeployItems tests

    [TestMethod]
    public void HasDeployItemsShouldReturnFalseForNoDeploymentItems()
    {
        TestCase testCase = new("A.C.M", new System.Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemsProperty, null);

        Assert.IsFalse(deploymentItemUtility.HasDeploymentItems(testCase));
    }

    [TestMethod]
    public void HasDeployItemsShouldReturnTrueWhenDeploymentItemsArePresent()
    {
        TestCase testCase = new("A.C.M", new System.Uri("executor://testExecutor"), "A");
        var kvpArray = new[]
                {
                    new KeyValuePair<string, string>(
                        defaultDeploymentItemPath,
                        defaultDeploymentItemOutputDirectory)
                };
        testCase.SetPropertyValue(DeploymentItemsProperty, kvpArray);

        Assert.IsTrue(deploymentItemUtility.HasDeploymentItems(testCase));
    }

    #endregion

    #region private methods

    private void SetupDeploymentItems(MemberInfo memberInfo, KeyValuePair<string, string>[] deploymentItems)
    {
        var deploymentItemAttributes = new List<TestFrameworkV2Extension.DeploymentItemAttribute>();

        foreach (var deploymentItem in deploymentItems)
        {
            deploymentItemAttributes.Add(new TestFrameworkV2Extension.DeploymentItemAttribute(deploymentItem.Key, deploymentItem.Value));
        }

        mockReflectionUtility.Setup(
            ru =>
            ru.GetCustomAttributes(
                memberInfo,
                typeof(TestFrameworkV2Extension.DeploymentItemAttribute))).Returns((object[])deploymentItemAttributes.ToArray());
    }

    #endregion
}
