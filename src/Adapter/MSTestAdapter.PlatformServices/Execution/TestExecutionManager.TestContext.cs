// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    /// <summary>
    /// Get test context properties.
    /// </summary>
    /// <param name="tcmProperties">Tcm properties.</param>
    /// <param name="sourceLevelParameters">Source level parameters.</param>
    /// <param name="unitTestElement">The unit test element to get properties from.</param>
    /// <returns>Test context properties.</returns>
    private static Dictionary<string, object?> GetTestContextProperties(
        IReadOnlyDictionary<string, object?>? tcmProperties,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestElement unitTestElement)
    {
        // If we only have sourceLevelParameters, we create a new dictionary with just those.
        if (tcmProperties is null &&
            unitTestElement.Traits is null or { Length: 0 } &&
            unitTestElement.TestCategory is null or { Length: 0 })
        {
            return [with(sourceLevelParameters!)];
        }

        // To avoid any resizes and additional overhead, we calculate the capacity beforehand.
        var testContextProperties = new Dictionary<string, object?>(capacity: sourceLevelParameters.Count + (tcmProperties?.Count ?? 0) + (unitTestElement.Traits?.Length ?? 0) + (unitTestElement.TestCategory?.Length ?? 0));

        // Add tcm properties.
        if (tcmProperties is not null)
        {
            foreach (KeyValuePair<string, object?> kvp in tcmProperties)
            {
                testContextProperties[kvp.Key] = kvp.Value;
            }
        }

        // Add source level parameters.
        foreach (KeyValuePair<string, object> kvp in sourceLevelParameters)
        {
            testContextProperties[kvp.Key] = kvp.Value;
        }

        if (unitTestElement.Traits is { Length: > 0 })
        {
            foreach (Trait trait in unitTestElement.Traits)
            {
                ValidateAndAssignTestProperty(testContextProperties, trait.Name, trait.Value);
            }
        }

        if (unitTestElement.TestCategory is { Length: > 0 })
        {
            foreach (string category in unitTestElement.TestCategory)
            {
                ValidateAndAssignTestProperty(testContextProperties, category, string.Empty);
            }
        }

        return testContextProperties;
    }

    /// <summary>
    /// Validates If a Custom test property is valid and then adds it to the TestContext property list.
    /// </summary>
    /// <param name="testContextProperties"> The test context properties. </param>
    /// <param name="propertyName"> The property name. </param>
    /// <param name="propertyValue"> The property value. </param>
    private static void ValidateAndAssignTestProperty(
        Dictionary<string, object?> testContextProperties,
        string propertyName,
        string propertyValue)
    {
        if (StringEx.IsNullOrEmpty(propertyName))
        {
            return;
        }

        if (testContextProperties.ContainsKey(propertyName))
        {
            // Do not add to the test context because it would conflict with an already existing value.
            // We were at one point reporting a warning here. However with extensibility centered around TestProperty where
            // users can have multiple WorkItemAttributes(say) we cannot throw a warning here. Users would have multiple of these attributes
            // so that it shows up in reporting rather than seeing them in TestContext properties.
        }
        else
        {
            testContextProperties.Add(propertyName, propertyValue);
        }
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle errors in user specified run parameters")]
    private void CacheSessionParameters(IRunContext? runContext, IAdapterMessageLogger messageLogger)
    {
        if (StringEx.IsNullOrEmpty(runContext?.RunSettings?.SettingsXml))
        {
            return;
        }

        try
        {
            Dictionary<string, object>? testRunParameters = RunSettingsUtilities.GetTestRunParameters(runContext.RunSettings.SettingsXml);
            if (testRunParameters != null)
            {
                // Clear sessionParameters to prevent key collisions of test run parameters in case
                // "Keep Test Execution Engine Alive" is selected in VS.
                _sessionParameters.Clear();
                foreach (KeyValuePair<string, object> kvp in testRunParameters)
                {
                    _sessionParameters.Add(kvp);
                }
            }
        }
        catch (Exception ex)
        {
            messageLogger.SendMessage(MessageLevel.Error, ex.Message);
        }
    }
}
