// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public abstract class DataWithSessionUid(string displayName, string? description, SessionUid sessionUid)
    : PropertyBagData(displayName, description)
{
    public SessionUid SessionUid { get; } = sessionUid;

    public override string ToString()
    {
        StringBuilder builder = new();
        builder.AppendLine("Generic session data:");
        builder.Append("Display name: ").AppendLine(DisplayName);
        builder.Append("Description: ").AppendLine(Description);
        builder.Append("Session UID: ").AppendLine(SessionUid.Value);
        builder.AppendLine("Properties: [");
        foreach (IProperty property in Properties)
        {
            builder.AppendLine(property.ToString());
        }

        builder.AppendLine("]");

        return builder.ToString();
    }
}
