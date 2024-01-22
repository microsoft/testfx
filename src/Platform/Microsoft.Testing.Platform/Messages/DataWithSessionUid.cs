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

            builder.Append(' ').Append(property.ToString());
        }

        if (hasAnyProperty)
        {
            builder.Append(' ');
        }

        builder.Append("] }");

        return builder.ToString();
    }
}
