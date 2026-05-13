// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class AttributeExtensions
{
    public static bool IsIgnored(this ICustomAttributeProvider type, out string? ignoreMessage)
    {
        Attribute[] allAttributes = ReflectHelper.Instance.GetCustomAttributesCached(type);

        // Fast path: no ConditionBaseAttribute present (common case) → zero allocations
        bool hasConditionAttribute = false;
        foreach (Attribute attr in allAttributes)
        {
            if (attr is ConditionBaseAttribute)
            {
                hasConditionAttribute = true;
                break;
            }
        }

        if (!hasConditionAttribute)
        {
            ignoreMessage = null;
            return false;
        }

        // Slow path: manual grouping instead of LINQ GroupBy to avoid iterator/Lookup allocations.
        // We track insertion order explicitly because Dictionary<TKey,TValue>.Values enumeration
        // order is not guaranteed (especially on .NET Framework), and we need to return the ignore
        // message from the first unsatisfied group in attribute order.
        Dictionary<string, (bool Satisfied, string? FirstMessage)> groups = [];
        List<string> groupOrder = [];
        foreach (Attribute attr in allAttributes)
        {
            if (attr is not ConditionBaseAttribute conditionAttr)
            {
                continue;
            }

            bool shouldRun = conditionAttr.Mode == ConditionMode.Include ? conditionAttr.IsConditionMet : !conditionAttr.IsConditionMet;

            if (!groups.TryGetValue(conditionAttr.GroupName, out (bool Satisfied, string? FirstMessage) groupState))
            {
                groups[conditionAttr.GroupName] = (shouldRun, shouldRun ? null : conditionAttr.IgnoreMessage);
                groupOrder.Add(conditionAttr.GroupName);
            }
            else if (!groupState.Satisfied)
            {
                if (shouldRun)
                {
                    groups[conditionAttr.GroupName] = (true, null);
                }
                else if (groupState.FirstMessage is null)
                {
                    groups[conditionAttr.GroupName] = (false, conditionAttr.IgnoreMessage);
                }
            }
        }

        foreach (string groupName in groupOrder)
        {
            (bool satisfied, string? firstMessage) = groups[groupName];
            if (!satisfied)
            {
                ignoreMessage = firstMessage;
                return true;
            }
        }

        ignoreMessage = null;
        return false;
    }
}
