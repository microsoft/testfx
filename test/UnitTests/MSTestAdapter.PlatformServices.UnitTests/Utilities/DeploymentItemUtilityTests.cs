// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public class DeploymentItemUtilityTests : TestContainer
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
        _warnings = [];
    }

    #region GetClassLevelDeploymentItems tests

    public void GetClassLevelDeploymentItemsShouldReturnEmptyListWhenNoDeploymentItems()
    {
        _mockReflectionUtility.Setup(x => x.GetCustomAttributes(typeof(DeploymentItemUtilityTests), typeof(DeploymentItemAttribute)))
            .Returns([]);
        IList<DeploymentItem> deploymentItems = _deploymentItemUtility.GetClassLevelDeploymentItems(typeof(DeploymentItemUtilityTests), _warnings);

        deploymentItems.Should().NotBeNull();
        deploymentItems.Count.Should().Be(0);
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
        expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()).Should().BeTrue();
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

        expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()).Should().BeTrue();
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

        expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()).Should().BeTrue();
    }

    public void GetClassLevelDeploymentItemsShouldReportWarningsForInvalidDeploymentItems()
    {
        KeyValuePair<string, string>[] deploymentItemAttributes =
        [
            new KeyValuePair<string, string>(
                _defaultDeploymentItemPath,
                _defaultDeploymentItemOutputDirectory),
            new KeyValuePair<string, string>(
                null!,
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

        expectedDeploymentItems.SequenceEqual(deploymentItems.ToArray()).Should().BeTrue();
        _warnings.Count.Should().Be(1);
        _warnings.ToArray()[0].Contains(Resource.DeploymentItemPathCannotBeNullOrEmpty).Should().BeTrue();
    }

    #endregion

    #region GetDeploymentItems tests

    public void GetDeploymentItemsShouldReturnNullOnNoDeploymentItems()
    {
        MethodInfo method = typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems")!;
        _mockReflectionUtility.Setup(x => x.GetCustomAttributes(method, typeof(DeploymentItemAttribute)))
            .Returns([]);

        _deploymentItemUtility.GetDeploymentItems(method, null!, _warnings).Should().BeNull();
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
                "GetDeploymentItemsShouldReturnNullOnNoDeploymentItems")!;

        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        KeyValuePair<string, string>[]? deploymentItems = _deploymentItemUtility.GetDeploymentItems(
            memberInfo,
            null!,
            _warnings);

        deploymentItemAttributes.SequenceEqual(deploymentItems).Should().BeTrue();
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

        MethodInfo method = typeof(DeploymentItemUtilityTests).GetMethod("GetDeploymentItemsShouldReturnNullOnNoDeploymentItems")!;
        _mockReflectionUtility.Setup(x => x.GetCustomAttributes(method, typeof(DeploymentItemAttribute)))
            .Returns([]);

        // Act.
        KeyValuePair<string, string>[]? deploymentItems = _deploymentItemUtility.GetDeploymentItems(method, classLevelDeploymentItems, _warnings);

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

        expectedDeploymentItems.SequenceEqual(deploymentItems).Should().BeTrue();
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
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems")!;
        SetupDeploymentItems(memberInfo, deploymentItemAttributes);

        DeploymentItem[] classLevelDeploymentItems =
        [
            new DeploymentItem(
                _defaultDeploymentItemPath + "\\temp2",
                _defaultDeploymentItemOutputDirectory)
        ];

        // Act.
        KeyValuePair<string, string>[]? deploymentItems = _deploymentItemUtility.GetDeploymentItems(
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

        expectedDeploymentItems.SequenceEqual(deploymentItems).Should().BeTrue();
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
                "GetDeploymentItemsShouldReturnClassAndMethodLevelDeploymentItems")!;
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
        KeyValuePair<string, string>[]? deploymentItems = _deploymentItemUtility.GetDeploymentItems(
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

        expectedDeploymentItems.SequenceEqual(deploymentItems).Should().BeTrue();
    }

    #endregion

    #region IsValidDeploymentItem tests

    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsNull()
    {
        DeploymentItemUtility.IsValidDeploymentItem(null, _defaultDeploymentItemOutputDirectory, out string? warning).Should().BeFalse();
        Resource.DeploymentItemPathCannotBeNullOrEmpty.Should().Contain(warning!);
    }

    public void IsValidDeploymentItemShouldReportWarningIfSourcePathIsEmpty()
    {
        DeploymentItemUtility.IsValidDeploymentItem(string.Empty, _defaultDeploymentItemOutputDirectory, out string? warning).Should().BeFalse();
        Resource.DeploymentItemPathCannotBeNullOrEmpty.Should().Contain(warning!);
    }

    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsNull()
    {
        DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, null, out string? warning).Should().BeFalse();

        warning.Should().Contain(Resource.DeploymentItemOutputDirectoryCannotBeNull);
    }

    public void IsValidDeploymentItemShouldReportWarningIfSourcePathHasInvalidCharacters()
    {
        DeploymentItemUtility.IsValidDeploymentItem("C:<>", _defaultDeploymentItemOutputDirectory, out string? warning).Should().BeFalse();

        warning.Should().Contain(
            string.Format(
                CultureInfo.InvariantCulture,
                Resource.DeploymentItemContainsInvalidCharacters,
                "C:<>",
                _defaultDeploymentItemOutputDirectory));
    }

    public void IsValidDeploymentItemShouldReportWarningIfOutputDirectoryHasInvalidCharacters()
    {
        DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, "<>", out string? warning).Should().BeFalse();

        warning.Should().Contain(
            string.Format(
                CultureInfo.InvariantCulture,
                Resource.DeploymentItemContainsInvalidCharacters,
                _defaultDeploymentItemPath,
                "<>"));
    }

    public void IsValidDeploymentItemShouldReportWarningIfDeploymentOutputDirectoryIsRooted()
    {
        DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, "C:\\temp", out string? warning).Should().BeFalse();

        warning.Should().Contain(
           string.Format(
               CultureInfo.InvariantCulture,
               Resource.DeploymentItemOutputDirectoryMustBeRelative,
               "C:\\temp"));
    }

    public void IsValidDeploymentItemShouldReturnTrueForAValidDeploymentItem()
    {
        DeploymentItemUtility.IsValidDeploymentItem(_defaultDeploymentItemPath, _defaultDeploymentItemOutputDirectory, out string? warning).Should().BeTrue();

        warning.Should().BeNull();
    }
    #endregion

    #region HasDeployItems tests

    public void HasDeployItemsShouldReturnFalseForNoDeploymentItems()
    {
        TestCase testCase = new("A.C.M", new Uri("executor://testExecutor"), "A");
        testCase.SetPropertyValue(DeploymentItemsProperty, null);

        DeploymentItemUtility.HasDeploymentItems(testCase).Should().BeFalse();
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

        DeploymentItemUtility.HasDeploymentItems(testCase).Should().BeTrue();
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
#endif
