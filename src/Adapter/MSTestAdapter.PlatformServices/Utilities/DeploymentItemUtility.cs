// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

/// <summary>
/// The deployment utility.
/// </summary>
internal sealed class DeploymentItemUtility
{
    private readonly IReflectionOperations _reflectionOperations;

    /// <summary>
    /// A cache for class level deployment items.
    /// </summary>
    private readonly Dictionary<Type, IList<DeploymentItem>> _classLevelDeploymentItems;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentItemUtility"/> class.
    /// </summary>
    /// <param name="reflectionOperations"> The reflection operations. </param>
    internal DeploymentItemUtility(IReflectionOperations reflectionOperations)
    {
        _reflectionOperations = reflectionOperations;
        _classLevelDeploymentItems = [];
    }

    /// <summary>
    /// Get the class level deployment items.
    /// </summary>
    /// <param name="type"> The type. </param>
    /// <param name="warnings"> The warnings. </param>
    /// <returns> The <see cref="IList{T}"/> of deployment items on a class. </returns>
    internal IList<DeploymentItem> GetClassLevelDeploymentItems(Type type, ICollection<string> warnings)
    {
        if (!_classLevelDeploymentItems.TryGetValue(type, out IList<DeploymentItem>? value))
        {
            IEnumerable<DeploymentItemAttribute> deploymentItemAttributes = _reflectionOperations.GetAttributes<DeploymentItemAttribute>(type);
            value = GetDeploymentItems(deploymentItemAttributes, warnings);
            _classLevelDeploymentItems[type] = value;
        }

        return value;
    }

    /// <summary>
    /// The get deployment items.
    /// </summary> <param name="method"> The method. </param>
    /// <param name="classLevelDeploymentItems"> The class level deployment items. </param>
    /// <param name="warnings"> The warnings. </param>
    /// <returns> The <see cref="KeyValuePair{TKey,TValue}"/>.of deployment item information. </returns>
    internal KeyValuePair<string, string>[]? GetDeploymentItems(MethodInfo method, IEnumerable<DeploymentItem> classLevelDeploymentItems,
        ICollection<string> warnings)
    {
        List<DeploymentItem> testLevelDeploymentItems = GetDeploymentItems(_reflectionOperations.GetAttributes<DeploymentItemAttribute>(method), warnings);

        return ToKeyValuePairs(Concat(testLevelDeploymentItems, classLevelDeploymentItems));
    }

    /// <summary>
    /// Checks if parameters are valid to create deployment item.
    /// </summary>
    /// <param name="sourcePath"> The source Path. </param>
    /// <param name="relativeOutputDirectory"> The relative Output Directory. </param>
    /// <param name="warning"> The warning message if it is an invalid deployment item. </param>
    /// <returns> Returns true if it is a valid deployment item. </returns>
    [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "2#", Justification = "Internal method.")]
    internal static bool IsValidDeploymentItem([NotNullWhen(true)] string? sourcePath, [NotNullWhen(true)] string? relativeOutputDirectory, [NotNullWhen(false)] out string? warning)
    {
        if (StringEx.IsNullOrEmpty(sourcePath))
        {
            warning = Resource.DeploymentItemPathCannotBeNullOrEmpty;
            return false;
        }

        if (relativeOutputDirectory == null)
        {
            warning = Resource.DeploymentItemOutputDirectoryCannotBeNull;
            return false;
        }

        if (IsInvalidPath(sourcePath) || IsInvalidPath(relativeOutputDirectory))
        {
            warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemContainsInvalidCharacters, sourcePath, relativeOutputDirectory);
            return false;
        }

        if (Path.IsPathRooted(relativeOutputDirectory))
        {
            warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemOutputDirectoryMustBeRelative, relativeOutputDirectory);
            return false;
        }

        warning = null;
        return true;
    }

    /// <summary>
    /// Returns whether there are any deployment items defined on the test.
    /// </summary>
    /// <param name="testElement"> The test. </param>
    /// <returns> True if has deployment items.</returns>
    internal static bool HasDeploymentItems(UnitTestElement testElement)
        => testElement.DeploymentItems is { Length: > 0 };

    internal static IList<DeploymentItem> GetDeploymentItems(IEnumerable<UnitTestElement> tests)
    {
        List<DeploymentItem> allDeploymentItems = [];
        foreach (UnitTestElement test in tests)
        {
            KeyValuePair<string, string>[]? items = test.DeploymentItems;
            if (items == null || items.Length == 0)
            {
                continue;
            }

            IList<DeploymentItem>? deploymentItemsToBeAdded = FromKeyValuePairs(items);

            // deploymentItemsToBeAdded is never null here: items is non-empty (filtered above) and FromKeyValuePairs only returns null for an empty array.
            foreach (DeploymentItem deploymentItemToBeAdded in deploymentItemsToBeAdded!)
            {
                AddDeploymentItem(allDeploymentItems, deploymentItemToBeAdded);
            }
        }

        return allDeploymentItems;
    }

    internal static void AddDeploymentItem(IList<DeploymentItem> deploymentItemList, DeploymentItem deploymentItem)
    {
        DebugEx.Assert(deploymentItemList != null, "DeploymentItem list cannot be null");
        DebugEx.Assert(deploymentItem != null, "DeploymentItem  cannot be null");

        if (!deploymentItemList.Contains(deploymentItem))
        {
            deploymentItemList.Add(deploymentItem);
        }
    }

    private static bool IsInvalidPath(string path)
    {
        if (path.IndexOfAny(Path.GetInvalidPathChars()) != -1)
        {
            return true;
        }

        try
        {
            string fileName = Path.GetFileName(path);

            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                return true;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static List<DeploymentItem> GetDeploymentItems(IEnumerable<DeploymentItemAttribute> deploymentItemAttributes, ICollection<string> warnings)
    {
        var deploymentItems = new List<DeploymentItem>();

        foreach (DeploymentItemAttribute deploymentItemAttribute in deploymentItemAttributes)
        {
            if (IsValidDeploymentItem(deploymentItemAttribute.Path, deploymentItemAttribute.OutputDirectory, out string? warning))
            {
                AddDeploymentItem(deploymentItems, new DeploymentItem(deploymentItemAttribute.Path, deploymentItemAttribute.OutputDirectory));
            }
            else
            {
                warnings.Add(warning);
            }
        }

        return deploymentItems;
    }

    [return: NotNullIfNotNull(nameof(deploymentItemList1))]
    [return: NotNullIfNotNull(nameof(deploymentItemList2))]
    private static IEnumerable<DeploymentItem>? Concat(IEnumerable<DeploymentItem>? deploymentItemList1, IEnumerable<DeploymentItem>? deploymentItemList2)
    {
        if (deploymentItemList1 == null && deploymentItemList2 == null)
        {
            return null;
        }

        if (deploymentItemList1 == null)
        {
            return deploymentItemList2;
        }

        if (deploymentItemList2 == null)
        {
            return deploymentItemList1;
        }

        IList<DeploymentItem> result = [.. deploymentItemList1];

        foreach (DeploymentItem item in deploymentItemList2)
        {
            AddDeploymentItem(result, item);
        }

        return result;
    }

    private static KeyValuePair<string, string>[]? ToKeyValuePairs(IEnumerable<DeploymentItem> deploymentItemList)
    {
        if (!deploymentItemList.Any())
        {
            return null;
        }

        List<KeyValuePair<string, string>> result = [];

        foreach (DeploymentItem deploymentItem in deploymentItemList)
        {
            if (deploymentItem != null)
            {
                result.Add(new KeyValuePair<string, string>(deploymentItem.SourcePath, deploymentItem.RelativeOutputDirectory));
            }
        }

        return [.. result];
    }

    private static IList<DeploymentItem>? FromKeyValuePairs(KeyValuePair<string, string>[] deploymentItemsData)
    {
        if (deploymentItemsData.Length == 0)
        {
            return null;
        }

        IList<DeploymentItem> result = [];

        foreach (KeyValuePair<string, string> kvp in deploymentItemsData)
        {
            AddDeploymentItem(result, new DeploymentItem(kvp.Key, kvp.Value));
        }

        return result;
    }
}
#endif
