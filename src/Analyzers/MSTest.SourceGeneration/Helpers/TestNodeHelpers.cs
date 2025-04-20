// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.SourceGeneration.Helpers;

internal static class TestNodeHelpers
{
    public static string GenerateEscapedName(string name)
        => name.Replace('.', '_');

    public static DisposableAction AppendTestNode(this IndentedStringBuilder nodeStringBuilder, string stableUid, string displayName,
        ICollection<string> properties, char testNodeBlockSuffixChar = ',')
    {
        IDisposable testNodeBlock = AppendTestNodeCommonPart(nodeStringBuilder, stableUid, displayName, properties, testNodeBlockSuffixChar);
        IDisposable testsBlock = nodeStringBuilder.AppendBlock("Tests = new MSTF::TestNode[]", closingBraceSuffixChar: ',');

        return new DisposableAction(() =>
        {
            testsBlock.Dispose();
            testNodeBlock.Dispose();
        });
    }

    public static DisposableAction AppendTestNode(this IndentedStringBuilder nodeStringBuilder, string stableUid, string displayName,
        ICollection<string> properties, string testsVariableName, char testNodeBlockSuffixChar = ',')
    {
        IDisposable testNodeBlock = AppendTestNodeCommonPart(nodeStringBuilder, stableUid, displayName, properties, testNodeBlockSuffixChar);
        nodeStringBuilder.AppendLine($"Tests = {testsVariableName}.ToArray(),");

        return new DisposableAction(testNodeBlock.Dispose);
    }

    private static IDisposable AppendTestNodeCommonPart(IndentedStringBuilder nodeStringBuilder, string stableUid, string displayName,
        ICollection<string> properties, char testNodeBlockSuffixChar = ',')
    {
        IDisposable testNodeBlock = nodeStringBuilder.AppendBlock("new MSTF::TestNode", testNodeBlockSuffixChar);
        nodeStringBuilder.AppendLine($"StableUid = \"{stableUid}\",");
        nodeStringBuilder.AppendLine($"DisplayName = \"{displayName}\",");

        if (properties.Count > 0)
        {
            using (nodeStringBuilder.AppendBlock($"Properties = new Msg::IProperty[{properties.Count}]", closingBraceSuffixChar: ','))
            {
                foreach (string property in properties)
                {
                    nodeStringBuilder.AppendLine(property);
                }
            }
        }
        else
        {
            nodeStringBuilder.AppendLine("Properties = Sys::Array.Empty<Msg::IProperty>(),");
        }

        return testNodeBlock;
    }
}
