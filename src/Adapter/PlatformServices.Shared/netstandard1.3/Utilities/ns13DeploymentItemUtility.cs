// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The deployment utility.
    /// </summary>
    internal class DeploymentItemUtility
    {
        private ReflectionUtility reflectionUtility;

        /// <summary>
        /// A cache for class level deployment items.
        /// </summary>
        private Dictionary<Type, IList<DeploymentItem>> classLevelDeploymentItems;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentItemUtility"/> class.
        /// </summary>
        /// <param name="reflectionUtility"> The reflect helper. </param>
        internal DeploymentItemUtility(ReflectionUtility reflectionUtility)
        {
            this.reflectionUtility = reflectionUtility;
            this.classLevelDeploymentItems = new Dictionary<Type, IList<DeploymentItem>>();
        }

        /// <summary>
        /// Get the class level deployment items.
        /// </summary>
        /// <param name="type"> The type. </param>
        /// <param name="warnings"> The warnings. </param>
        /// <returns> The <see cref="IList{T}"/> of deployment items on a class. </returns>
        internal IList<DeploymentItem> GetClassLevelDeploymentItems(Type type, ICollection<string> warnings)
        {
            if (!this.classLevelDeploymentItems.ContainsKey(type))
            {
                var deploymentItemAttributes = this.reflectionUtility.GetCustomAttributes(
                    type.GetTypeInfo(),
                    typeof(DeploymentItemAttribute));

                this.classLevelDeploymentItems[type] = this.GetDeploymentItems(deploymentItemAttributes, warnings);
            }

            return this.classLevelDeploymentItems[type];
        }

        /// <summary>
        /// The get deployment items.
        /// </summary> <param name="method"> The method. </param>
        /// <param name="classLevelDeploymentItems"> The class level deployment items. </param>
        /// <param name="warnings"> The warnings. </param>
        /// <returns> The <see cref="KeyValuePair{TKey,TValue}"/>.of deployment item information. </returns>
        internal KeyValuePair<string, string>[] GetDeploymentItems(MethodInfo method, IList<DeploymentItem> classLevelDeploymentItems, ICollection<string> warnings)
        {
            var testLevelDeploymentItems = this.GetDeploymentItems(this.reflectionUtility.GetCustomAttributes(method, typeof(DeploymentItemAttribute)), warnings);

            return this.ToKeyValuePairs(this.Concat(testLevelDeploymentItems, classLevelDeploymentItems));
        }

        /// <summary>
        /// Checks if parameters are valid to create deployment item.
        /// </summary>
        /// <param name="sourcePath"> The source Path. </param>
        /// <param name="relativeOutputDirectory"> The relative Output Directory. </param>
        /// <param name="warning"> The warning message if it is an invalid deployment item. </param>
        /// <returns> Returns true if it is a valid deployment item. </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "2#", Justification = "Internal method.")]
        internal bool IsValidDeploymentItem(string sourcePath, string relativeOutputDirectory, out string warning)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                warning = Resource.DeploymentItemPathCannotBeNullOrEmpty;
                return false;
            }

            if (relativeOutputDirectory == null)
            {
                warning = Resource.DeploymentItemOutputDirectoryCannotBeNull;
                return false;
            }

            if (sourcePath.IndexOfAny(Path.GetInvalidPathChars()) != -1 ||
                relativeOutputDirectory.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemContainsInvalidCharacters, sourcePath, relativeOutputDirectory);
                return false;
            }

            if (Path.IsPathRooted(relativeOutputDirectory))
            {
                warning = string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemOutputDirectoryMustBeRelative, relativeOutputDirectory);
                return false;
            }

            warning = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns whether there are any deployment items defined on the test
        /// </summary>
        /// <param name="testCase"> The test Case. </param>
        /// <returns> True if has deployment items.</returns>
        internal bool HasDeploymentItems(TestCase testCase)
        {
            var deploymentItems = this.GetDeploymentItems(testCase);

            return deploymentItems != null && deploymentItems.Length > 0;
        }

        internal IList<DeploymentItem> GetDeploymentItems(IEnumerable<TestCase> tests)
        {
            List<DeploymentItem> allDeploymentItems = new List<DeploymentItem>();
            foreach (var test in tests)
            {
                KeyValuePair<string, string>[] items = this.GetDeploymentItems(test);
                if (items == null || items.Length == 0)
                {
                    continue;
                }

                IList<DeploymentItem> deploymentItemsToBeAdded = this.FromKeyValuePairs(items);
                foreach (var deploymentItemToBeAdded in deploymentItemsToBeAdded)
                {
                    this.AddDeploymentItem(allDeploymentItems, deploymentItemToBeAdded);
                }
            }

            return allDeploymentItems;
        }

        internal void AddDeploymentItem(IList<DeploymentItem> deploymentItemList, DeploymentItem deploymentItem)
        {
            Debug.Assert(deploymentItemList != null, "DeploymentItem list cannot be null");
            Debug.Assert(deploymentItem != null, "DeploymentItem  cannot be null");

            if (!deploymentItemList.Contains(deploymentItem))
            {
                deploymentItemList.Add(deploymentItem);
            }
        }

        private IList<DeploymentItem> GetDeploymentItems(object[] deploymentItemAttributes, ICollection<string> warnings)
        {
            var deploymentItems = new List<DeploymentItem>();

            foreach (DeploymentItemAttribute deploymentItemAttribute in deploymentItemAttributes)
            {
                string warning;
                if (this.IsValidDeploymentItem(deploymentItemAttribute.Path, deploymentItemAttribute.OutputDirectory, out warning))
                {
                    this.AddDeploymentItem(deploymentItems, new DeploymentItem(deploymentItemAttribute.Path, deploymentItemAttribute.OutputDirectory));
                }
                else
                {
                    warnings.Add(warning);
                }
            }

            return deploymentItems;
        }

        private IList<DeploymentItem> Concat(IList<DeploymentItem> deploymentItemList1, IList<DeploymentItem> deploymentItemList2)
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

            IList<DeploymentItem> result = new List<DeploymentItem>(deploymentItemList1);

            foreach (var item in deploymentItemList2)
            {
                this.AddDeploymentItem(result, item);
            }

            return result;
        }

        /// <summary>
        /// Returns the deployment items defined on the test
        /// </summary>
        /// <param name="testCase"> The test Case. </param>
        /// <returns> The <see cref="KeyValuePair{TKey,TValue}"/>. </returns>
        private KeyValuePair<string, string>[] GetDeploymentItems(TestCase testCase)
        {
            return
                testCase.GetPropertyValue(PlatformServices.Constants.DeploymentItemsProperty) as
                KeyValuePair<string, string>[];
        }

        private KeyValuePair<string, string>[] ToKeyValuePairs(IList<DeploymentItem> deploymentItemList)
        {
            if (deploymentItemList == null || deploymentItemList.Count == 0)
            {
                return null;
            }

            IList<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();

            foreach (var deploymentItem in deploymentItemList)
            {
                if (deploymentItem != null)
                {
                    result.Add(new KeyValuePair<string, string>(deploymentItem.SourcePath, deploymentItem.RelativeOutputDirectory));
                }
            }

            return result.ToArray();
        }

        private IList<DeploymentItem> FromKeyValuePairs(KeyValuePair<string, string>[] deploymentItemsData)
        {
            if (deploymentItemsData == null || deploymentItemsData.Length == 0)
            {
                return null;
            }

            IList<DeploymentItem> result = new List<DeploymentItem>();

            foreach (var deploymentItemData in deploymentItemsData)
            {
                this.AddDeploymentItem(result, new DeploymentItem(deploymentItemData.Key, deploymentItemData.Value));
            }

            return result;
        }
    }
}
