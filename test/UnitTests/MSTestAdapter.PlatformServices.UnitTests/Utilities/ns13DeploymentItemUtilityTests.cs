// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Utilities;

#pragma warning disable SA1649 // File name must match first type name
public class DeploymentItemUtilityTests : TestContainer
#pragma warning restore SA1649 // File name must match first type name
{
    internal static readonly TestProperty DeploymentItemsProperty = TestProperty.Register(
        "MSTestDiscoverer.DeploymentItems",
        "DeploymentItems",
        typeof(KeyValuePair<string, string>[]),
        TestPropertyAttributes.Hidden,
        typeof(TestCase));

    private readonly Mock<ReflectionUtility> _mockReflectionUtility;
    private readonly DeploymentItemUtility _deploymentItemUtility;
    private readonly ICollection<string> _warnings;

    private readonly string _defaultDeploymentItemPath = @"c:\temp";
    private readonly string _defaultDeploymentItemOutputDirectory = "out";

    public DeploymentItemUtilityTests()
    {
        _mockReflectionUtility = new Mock<ReflectionUtility>();
        _deploymentItemUtility = new DeploymentItemUtility(_mockReflectionUtility.Object);
        _warnings = new List<string>();
    }

    #region GetClassLevelDeploymentItems tests

    public void GetClassLevelDeploymentItemsShouldReturnEmptyListWhenNoDeploymentItems()
    {
        _mockReflectionUtility.Setup(x => x.GetCustomAttributes(typeof(DeploymentItemUtilityTests), typeof(DeploymentItemAttribute)))
            .Returns([]);
        IList<DeploymentItem> deploymentItems = _deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), _warnings);

        Verify(deploymentItems is not null);
        Verify(deploymentItems.Count == 0);
    }

    public void GetClassLevelDeploymentItemsShouldReturnADeploymentItem()
    {
        KeyValuePair<string, string>[] kvpArray =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory)
        ];
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests), kvpArray);

        IList<DeploymentItem> deploymentItems = _deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), _warnings);
        var expectedDeploymentItems = new DeploymentItem[]
        {
            new(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
        };
        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()));
    }

    public void GetClassLevelDeploymentItemsShouldReturnMoreThanOneDeploymentItems()
    {
        KeyValuePair<string, string>[] deploymentItemAttributes =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory)
        ];
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests), deploymentItemAttributes);

        IList<DeploymentItem> deploymentItems =
            _deploymentItemUtility.GetClassLevelDeploymentItems(
                typeof(DeploymentItemUtilityTests),
                _warnings);

        var expectedDeploymentItems = new DeploymentItem[]
        {
            new(
                deploymentItemAttributes[0].Key,
                deploymentItemAttributes[0].Value),
            new(
                deploymentItemAttributes[1].Key,
                deploymentItemAttributes[1].Value),
        };

        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()));
    }

    public void GetClassLevelDeploymentItemsShouldNotReturnDuplicateDeploymentItemEntries()
    {
        KeyValuePair<string, string>[] deploymentItemAttributes =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory)
        ];
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests), deploymentItemAttributes);

        IList<DeploymentItem> deploymentItems =
            _deploymentItemUtility.GetClassLevelDeploymentItems(
                typeof(DeploymentItemUtilityTests),
                _warnings);

        DeploymentItem[] expectedDeploymentItems =
        [
            new DeploymentItem(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory)
        ];

        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()));
    }

    public void GetClassLevelDeploymentItemsShouldReportWarningsForInvalidDeploymentItems()
    {
        KeyValuePair<string, string>[] deploymentItemAttributes =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new KeyValuePair<string, string>(
                null,
                _defaultDeploymentItemOutputDirectory)
        ];
        SetupDeploymentItems(typeof(DeploymentItemUtilityTests), deploymentItemAttributes);

        IList<DeploymentItem> deploymentItems = _deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), _warnings);

        var expectedDeploymentItems = new DeploymentItem[]
        {
            new(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
        };

        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()));
        Verify(_warnings.Count == 1);
        Verify(_warnings.ToArray()[0].Contains(Resource.DeploymentItemPathCannotBeNullOrEmpty));
    }

    #endregion

    #region GetDeploymentItems tests

    public void GetDeploymentItemsShouldReturnNullOnNoDeploymentItems()
    {
        MethodInfo method = typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems");
        _mockReflectionUtility.Setup(x => x.GetCustomAttributes(method, typeof(DeploymentItemAttribute)))
            .Returns([]);

        Verify(_deploymentItemUtility.GetDeploymentItems(method, null, _warnings) is null);
    }

    public void GetDeploymentItemsShouldReturnMethodLevelDeploymentItemsOnly()
    {
        KeyValuePair<string, string>[] deploymentItemAttributes =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory)
        ];
        MethodInfo memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnNullOnNoDeploymentItems");

        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        KeyValuePair<string, string>[] deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            null,
            _warnings);

        Verify(deploymentItemAttributes.SequenceEqual(deploymentItems.ToArray()));
    }

    public void GetDeploymentItemsShouldReturnClassLevelDeploymentItemsOnly()
    {
        // Arrange.
        var classLevelDeploymentItems = new DeploymentItem[]
        {
            new(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory),
        };

        MethodInfo method = typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems");
        _mockReflectionUtility.Setup(x => x.GetCustomAttributes(method, typeof(DeploymentItemAttribute)))
            .Returns([]);

        // Act.
        KeyValuePair<string, string>[] deploymentItems = _deploymentItemUtility.GetDeploymentItems(method, classLevelDeploymentItems, _warnings);

        // Assert.
        KeyValuePair<string, string>[] expectedDeploymentItems =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory)
        ];

        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()));
    }

    public void GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems()
    {
        // Arrange.
        KeyValuePair<string, string>[] deploymentItemAttributes =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory)
        ];
        MethodInfo memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems");
        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        DeploymentItem[] classLevelDeploymentItems =
        [
            new DeploymentItem(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory)
        ];

        // Act.
        KeyValuePair<string, string>[] deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            classLevelDeploymentItems,
            _warnings);

        // Assert.
        var expectedDeploymentItems = new KeyValuePair<string, string>[]
        {
            new(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory),
        };

        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems));
    }

    public void GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItemsWithoutDuplicates()
    {
        // Arrange.
        KeyValuePair<string, string>[] deploymentItemAttributes =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory)
        ];
        MethodInfo memberInfo =
            typeof(DeploymentItemUtilityTests).GetMethod(
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems");
        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        var classLevelDeploymentItems = new DeploymentItem[]
        {
            new(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new(
                _defaultDeploymentItemPath + "\\temp1",
                _defaultDeploymentItemOutputDirectory),
        };

        // Act.
        KeyValuePair<string, string>[] deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            classLevelDeploymentItems,
            _warnings);

        // Assert.
        var expectedDeploymentItems = new KeyValuePair<string, string>[]
        {
            new(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory),
            new(
                _defaultDeploymentItemPath + "\\temp1",
                _defaultDeploymentItemOutputDirectory),
        };

        Verify(expectedDeploymentItems.SequenceEqual(deploymentItems));
    }

    #endregion

    #region IsValidDeploymentItem tests

    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsNull()
    {
        Verify(!DeploymentItemUtility.IsValidDeploymentItem(null, _defaultDeploymentItemOutputDirectory, out string warning));

        Verify(Resource.DeploymentItemPathCannotBeNullOrEmpty.Contains(warning));
    }

    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsEmpty()
    {
        Verify(!DeploymentItemUtility.IsValidDeploymentItem(string.Empty, _defaultDeploymentItemOutputDirectory, out string warning));

        Verify(Resource.DeploymentItemPathCannotBeNullOrEmpty.Contains(warning));
    }

    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsNull()
    {
        Verify(!DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, null, out string warning));

        StringAssert.Contains(Resource.DeploymentItemOutputDirectoryCannotBeNull, warning);
    }

    public void IsValidDeploymentItemShouldReportWarningIfSourcePathHasInvalidCharacters()
    {
        Verify(!DeploymentItemUtility.IsValidDeploymentItem("C:<>", _defaultDeploymentItemOutputDirectory, out string warning));

        StringAssert.Contains(
            string.Format(
                CultureInfo.InvariantCulture,
                Resource.DeploymentItemContainsInvalidCharacters,
                "C:<>",
                _defaultDeploymentItemOutputDirectory),
            warning);
    }

    public void IsValidDeploymentItemShouldReportWarningIfOutputDirectoryHasInvalidCharacters()
    {
        Verify(!DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, "<>", out string warning));

        StringAssert.Contains(
            string.Format(
                CultureInfo.InvariantCulture,
                Resource.DeploymentItemContainsInvalidCharacters,
                _defaultDeploymentItemPath,
                "<>"),
            warning);
    }

    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsRooted()
    {
        Verify(!DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, "C:\\temp", out string warning));

        StringAssert.Contains(
           string.Format(
               CultureInfo.InvariantCulture,
               Resource.DeploymentItemOutputDirectoryMustBeRelative,
               "C:\\temp"),
           warning);
    }

    public void IsValidDeploymentItemShouldReturnTrueForAValidDeploymentItem()
    {
        Verify(DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, _defaultDeploymentItemOutputDirectory, out string warning));

        Verify(warning is null);
    }
    #endregion

    #region HasDeployItems tests

    public void HasDeployItemsShouldReturnFalseForNoDeploymentItems()
    {
        TestCase testCase = new("A.C.M", new Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemsProperty, null);

        Verify(!DeploymentItemUtility.HasDeploymentItems(testCase));
    }

    public void HasDeployItemsShouldReturnTrueWhenDeploymentItemsArePresent()
    {
        TestCase testCase = new("A.C.M", new Uri("executor://testExecutor"), "A");
        KeyValuePair<string, string>[] kvpArray =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory)
        ];
        testCase.SetPropertyValue(DeploymentItemsProperty, kvpArray);

        Verify(DeploymentItemUtility.HasDeploymentItems(testCase));
    }

    #endregion

    #region private methods

    private void SetupDeploymentItems(MemberInfo memberInfo, KeyValuePair<string, string>[] deploymentItems)
    {
        var deploymentItemAttributes = new List<DeploymentItemAttribute>();

        foreach (KeyValuePair<string, string> deploymentItem in deploymentItems)
        {
            deploymentItemAttributes.Add(new DeploymentItemAttribute(deploymentItem.Key, deploymentItem.Value));
        }

        _mockReflectionUtility.Setup(
            ru =>
            ru.GetCustomAttributes(
                memberInfo,
                typeof(DeploymentItemAttribute))).Returns(deploymentItemAttributes.ToArray());
    }

    #endregion
}
