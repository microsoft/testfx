// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class AttributeExtensions
{
    public static bool IsIgnored(this ICustomAttributeProvider type, out string? ignoreMessage)
    {
        // Walk the cached Attribute[] directly to avoid the yield-iterator state machine
        // that GetAttributes<T>() allocates on every call. For the common case (no
        // ConditionBaseAttribute on the member), this returns immediately with zero
        // heap allocations.
        Attribute[] allAttributes = ReflectHelper.Instance.GetCustomAttributesCached(type);

        // Fast path: no condition attributes at all (most common case).
        ConditionBaseAttribute? firstConditionAttr = null;
        foreach (Attribute attr in allAttributes)
        {
            if (attr is ConditionBaseAttribute condAttr)
            {
                firstConditionAttr = condAttr;
                break;
            }
        }

        if (firstConditionAttr is null)
        {
            ignoreMessage = null;
            return false;
        }

        // Check whether there is more than one condition attribute.
        bool hasMultiple = false;
        foreach (Attribute attr in allAttributes)
        {
            if (attr is ConditionBaseAttribute && !ReferenceEquals(attr, firstConditionAttr))
            {
                hasMultiple = true;
                break;
            }
        }

        if (!hasMultiple)
        {
            // Single condition attribute — evaluate without any GroupBy allocation.
            bool shouldRun = firstConditionAttr.Mode == ConditionMode.Include
                ? firstConditionAttr.IsConditionMet
                : !firstConditionAttr.IsConditionMet;

            if (!shouldRun)
            {
                ignoreMessage = firstConditionAttr.IgnoreMessage;
                return true;
            }

            ignoreMessage = null;
            return false;
        }

        // Multiple condition attributes — collect into a list and apply the group-by logic.
        List<ConditionBaseAttribute> conditionAttrs = [];
        foreach (Attribute attr in allAttributes)
        {
            if (attr is ConditionBaseAttribute condAttr)
            {
                conditionAttrs.Add(condAttr);
            }
        }

        IEnumerable<IGrouping<string, ConditionBaseAttribute>> groups = conditionAttrs.GroupBy(attr => attr.GroupName);
        foreach (IGrouping<string, ConditionBaseAttribute> group in groups)
        {
            bool atLeastOneInGroupIsSatisfied = false;
            string? firstNonSatisfiedMatch = null;
            foreach (ConditionBaseAttribute attribute in group)
            {
                bool shouldRun = attribute.Mode == ConditionMode.Include ? attribute.IsConditionMet : !attribute.IsConditionMet;
                if (shouldRun)
                {
                    atLeastOneInGroupIsSatisfied = true;
                    break;
                }

                firstNonSatisfiedMatch ??= attribute.IgnoreMessage;
            }

            if (!atLeastOneInGroupIsSatisfied)
            {
                ignoreMessage = firstNonSatisfiedMatch;
                return true;
            }
        }

        ignoreMessage = null;
        return false;
    }
}
