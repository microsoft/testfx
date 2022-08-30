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

    private Mock<ReflectionUtility> _mockReflectionUtility;
    private DeploymentItemUtility _deploymentItemUtility;
    private ICollection<string> _warnings;

    private readonly string _defaultDeploymentItemPath = @"c:\temp";
    private readonly string _defaultDeploymentItemOutputDirectory = "out";

    [TestInitialize]
    public void TestInit()
    {
        _mockReflectionUtility = new Mock<ReflectionUtility>();
        _deploymentItemUtility = new DeploymentItemUtility(_mockReflectionUtility.Object);
        _warnings = new List<string>();
    }

    #region GetClassLevelDeploymentItems tests

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReturnEmptyListWhenNoDeploymentItems()
    {
        var deploymentItems = _deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), _warnings);

        Assert.IsNotNull(deploymentItems);
        Assert.AreEqual(0, deploymentItems.Count);
    }

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReturnADeploymentItem()
    {
        var kvpArray = new[]
                {
                    new KeyValuePair<string, string>(
                        _defaultDeploymentItemPath,
                        _defaultDeploymentItemOutputDirectory)
                };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), kvpArray);

        var deploymentItems = _deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), _warnings);
        var expectedDeploymentItems = new DeploymentItem[]
                                          {
                                              new DeploymentItem(
                                                  _defaultDeploymentItemPath,
                                                  _defaultDeploymentItemOutputDirectory)
                                          };
        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReturnMoreThanOneDeploymentItems()
    {
        var deploymentItemAttributes = new[]
                                           {
                                               new KeyValuePair<string, string>(
                                                   _defaultDeploymentItemPath,
                                                   _defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   _defaultDeploymentItemPath + "\\temp2",
                                                   _defaultDeploymentItemOutputDirectory)
                                           };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), deploymentItemAttributes);

        var deploymentItems =
            _deploymentItemUtility.GetClassLevelDeploymentItems(
                typeof(DeploymentItemUtilityTests),
                _warnings);

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
                                                   _defaultDeploymentItemPath,
                                                   _defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   _defaultDeploymentItemPath,
                                                   _defaultDeploymentItemOutputDirectory)
                                           };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), deploymentItemAttributes);

        var deploymentItems =
            _deploymentItemUtility.GetClassLevelDeploymentItems(
                typeof(DeploymentItemUtilityTests),
                _warnings);

        var expectedDeploymentItems = new[]
                                          {
                                              new DeploymentItem(
                                                  _defaultDeploymentItemPath,
                                                  _defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetClassLevelDeploymentItemsShouldReportWarningsForInvalidDeploymentItems()
    {
        var deploymentItemAttributes = new[]
                                           {
                                               new KeyValuePair<string, string>(
                                                   _defaultDeploymentItemPath,
                                                   _defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   null,
                                                   _defaultDeploymentItemOutputDirectory)
                                           };
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests).GetTypeInfo(), deploymentItemAttributes);

        var deploymentItems = _deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), _warnings);

        var expectedDeploymentItems = new DeploymentItem[]
                                          {
                                              new DeploymentItem(
                                                  _defaultDeploymentItemPath,
                                                  _defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings.ToArray()[0], Resource.DeploymentItemPathCannotBeNullOrEmpty);
    }

    #endregion

    #region GetDeploymentItems tests

    [TestMethod]
    public void GetDeploymentItemsShouldReturnNullOnNoDeploymentItems() => Assert.IsNull(_deploymentItemUtility.GetDeploymentItems(
            typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems"),
            null,
            _warnings));

    [TestMethod]
    public void GetDeploymentItemsShouldReturnMethodLevelDeploymentItemsOnly()
    {
        var deploymentItemAttributes = new[]
                                              {
                                               new KeyValuePair<string, string>(
                                                   _defaultDeploymentItemPath,
                                                   _defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   _defaultDeploymentItemPath + "\\temp2",
                                                   _defaultDeploymentItemOutputDirectory)
                                           };
        var memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnNullOnNoDeploymentItems");

        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        var deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            null,
            _warnings);

        CollectionAssert.AreEqual(deploymentItemAttributes, deploymentItems.ToArray());
    }

    [TestMethod]
    public void GetDeploymentItemsShouldReturnClassLevelDeploymentItemsOnly()
    {
        // Arrange.
        var classLevelDeploymentItems = new DeploymentItem[]
                                            {
                                                new DeploymentItem(
                                                    _defaultDeploymentItemPath,
                                                    _defaultDeploymentItemOutputDirectory),
                                                new DeploymentItem(
                                                    _defaultDeploymentItemPath + "\\temp2",
                                                    _defaultDeploymentItemOutputDirectory)
                                            };

        // Act.
        var deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems"),
            classLevelDeploymentItems,
            _warnings);

        // Assert.
        var expectedDeploymentItems = new[]
                                          {
                                              new KeyValuePair<string, string>(
                                                  _defaultDeploymentItemPath,
                                                  _defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  _defaultDeploymentItemPath + "\\temp2",
                                                  _defaultDeploymentItemOutputDirectory)
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
                                                   _defaultDeploymentItemPath,
                                                   _defaultDeploymentItemOutputDirectory)
                                           };
        var memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems");
        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        var classLevelDeploymentItems = new[]
                                            {
                                                new DeploymentItem(
                                                    _defaultDeploymentItemPath + "\\temp2",
                                                    _defaultDeploymentItemOutputDirectory)
                                            };

        // Act.
        var deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            classLevelDeploymentItems,
            _warnings);

        // Assert.
        var expectedDeploymentItems = new KeyValuePair<string, string>[]
                                          {
                                              new KeyValuePair<string, string>(
                                                  _defaultDeploymentItemPath,
                                                  _defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  _defaultDeploymentItemPath + "\\temp2",
                                                  _defaultDeploymentItemOutputDirectory)
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
                                                   _defaultDeploymentItemPath,
                                                   _defaultDeploymentItemOutputDirectory),
                                               new KeyValuePair<string, string>(
                                                   _defaultDeploymentItemPath + "\\temp2",
                                                   _defaultDeploymentItemOutputDirectory)
                                           };
        var memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems");
        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        var classLevelDeploymentItems = new DeploymentItem[]
                                            {
                                                new DeploymentItem(
                                                    _defaultDeploymentItemPath,
                                                    _defaultDeploymentItemOutputDirectory),
                                                new DeploymentItem(
                                                    _defaultDeploymentItemPath + "\\temp1",
                                                    _defaultDeploymentItemOutputDirectory)
                                            };

        // Act.
        var deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            classLevelDeploymentItems,
            _warnings);

        // Assert.
        var expectedDeploymentItems = new KeyValuePair<string, string>[]
                                          {
                                              new KeyValuePair<string, string>(
                                                  _defaultDeploymentItemPath,
                                                  _defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  _defaultDeploymentItemPath + "\\temp2",
                                                  _defaultDeploymentItemOutputDirectory),
                                              new KeyValuePair<string, string>(
                                                  _defaultDeploymentItemPath + "\\temp1",
                                                  _defaultDeploymentItemOutputDirectory)
                                          };

        CollectionAssert.AreEqual(expectedDeploymentItems, deploymentItems.ToArray());
    }

    #endregion

    #region IsValidDeploymentItem tests

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsNull()
    {
        Assert.IsFalse(_deploymentItemUtility.IsValidDeploymentItem(null, _defaultDeploymentItemOutputDirectory, out var warning));

        StringAssert.Contains(Resource.DeploymentItemPathCannotBeNullOrEmpty, warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsEmpty()
    {
        Assert.IsFalse(_deploymentItemUtility.IsValidDeploymentItem(string.Empty, _defaultDeploymentItemOutputDirectory, out var warning));

        StringAssert.Contains(Resource.DeploymentItemPathCannotBeNullOrEmpty, warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsNull()
    {
        Assert.IsFalse(_deploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, null, out var warning));

        StringAssert.Contains(Resource.DeploymentItemOutputDirectoryCannotBeNull, warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfSourcePathHasInvalidCharacters()
    {
        Assert.IsFalse(_deploymentItemUtility.IsValidDeploymentItem("C:<>", _defaultDeploymentItemOutputDirectory, out var warning));

        StringAssert.Contains(
            string.Format(
                Resource.DeploymentItemContainsInvalidCharacters,
                "C:<>",
                _defaultDeploymentItemOutputDirectory),
            warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfOutputDirectoryHasInvalidCharacters()
    {
        Assert.IsFalse(_deploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, "<>", out var warning));

        StringAssert.Contains(
            string.Format(
                Resource.DeploymentItemContainsInvalidCharacters,
                _defaultDeploymentItemPath,
                "<>"),
            warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsRooted()
    {
        Assert.IsFalse(_deploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, "C:\\temp", out var warning));

        StringAssert.Contains(
           string.Format(
               Resource.DeploymentItemOutputDirectoryMustBeRelative,
               "C:\\temp"),
           warning);
    }

    [TestMethod]
    public void IsValidDeploymentItemShouldReturnTrueForAValidDeploymentItem()
    {
        Assert.IsTrue(_deploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, _defaultDeploymentItemOutputDirectory, out var warning));

        Assert.IsTrue(string.Empty.Equals(warning));
    }
    #endregion

    #region HasDeployItems tests

    [TestMethod]
    public void HasDeployItemsShouldReturnFalseForNoDeploymentItems()
    {
        TestCase testCase = new("A.C.M", new System.Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemsProperty, null);

        Assert.IsFalse(_deploymentItemUtility.HasDeploymentItems(testCase));
    }

    [TestMethod]
    public void HasDeployItemsShouldReturnTrueWhenDeploymentItemsArePresent()
    {
        TestCase testCase = new("A.C.M", new System.Uri("executor://testExecutor"), "A");
        var kvpArray = new[]
                {
                    new KeyValuePair<string, string>(
                        _defaultDeploymentItemPath,
                        _defaultDeploymentItemOutputDirectory)
                };
        testCase.SetPropertyValue(DeploymentItemsProperty, kvpArray);

        Assert.IsTrue(_deploymentItemUtility.HasDeploymentItems(testCase));
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

        _mockReflectionUtility.Setup(
            ru =>
            ru.GetCustomAttributes(
                memberInfo,
                typeof(TestFrameworkV2Extension.DeploymentItemAttribute))).Returns((object[])deploymentItemAttributes.ToArray());
    }

    #endregion
}
