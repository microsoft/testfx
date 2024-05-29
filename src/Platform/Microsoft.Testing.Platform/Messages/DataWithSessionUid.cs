// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Represents data with a session UID.
/// </summary>
public abstract class DataWithSessionUid : PropertyBagData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataWithSessionUid"/> class.
    /// </summary>
    /// <param name="displayName">The display name of the data.</param>
    /// <param name="description">The description of the data.</param>
    /// <param name="sessionUid">The session UID.</param>
    protected DataWithSessionUid(string displayName, string? description, SessionUid sessionUid)
        : base(displayName, description)
    {
        SessionUid = sessionUid;
    }

    /// <summary>
    /// Gets the session UID.
    /// </summary>
    public SessionUid SessionUid { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("DataWithSessionUid { DisplayName = ")
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

        builder.Append("] }");

        return builder.ToString();
    }
}
