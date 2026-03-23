// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// A message representing a test node update.
/// </summary>
/// <param name="sessionUid">The session UID.</param>
/// <param name="testNode">The test node.</param>
/// <param name="displayName">The display name for the test update.</param>
/// <param name="parentTestNodeUid">The test node parent UID.</param>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
public sealed class TestNodeUpdateMessage(SessionUid sessionUid, string displayName, TestNode testNode, TestNodeUid? parentTestNodeUid = null) :
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
    DataWithSessionUid(displayName, "This data is used to report a TestNode state change.", sessionUid)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestNodeUpdateMessage"/> class.
    /// </summary>
    /// <param name="sessionUid">The session UID.</param>
    /// <param name="testNode">The test node.</param>
    /// <param name="parentTestNodeUid">The test node parent UID.</param>
    public TestNodeUpdateMessage(SessionUid sessionUid, TestNode testNode, TestNodeUid? parentTestNodeUid = null)
        : this(sessionUid, testNode.DisplayName, testNode, parentTestNodeUid)
    {
    }

    /// <summary>
    /// Gets the test node.
    /// </summary>
    public TestNode TestNode { get; } = testNode;

    /// <summary>
    /// Gets the parent test node UID.
    /// </summary>
    public TestNodeUid? ParentTestNodeUid { get; } = parentTestNodeUid;

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestNodeUpdateMessage { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
            .Append(", Properties = [");

        bool hasAnyProperty = false;
        foreach (IProperty property in Properties)
        {
            if (!hasAnyProperty)
            {
                hasAnyProperty = true;
            }
            else
            {
                builder.Append(',');
            }

            builder.Append(' ').Append(property);
        }

        if (hasAnyProperty)
        {
            builder.Append(' ');
        }

        builder.Append("], ParentTestNodeUid = ")
            .Append(ParentTestNodeUid?.ToString() ?? "<null>")
            .Append(", TestNode = ")
            .Append(TestNode)
            .Append(" }");

        return builder.ToString();
    }
}
