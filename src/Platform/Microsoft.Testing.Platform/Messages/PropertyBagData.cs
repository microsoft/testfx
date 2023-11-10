// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public abstract class PropertyBagData(string displayName, string? description) : IData
{
    public PropertyBag Properties { get; } = new();

    public string DisplayName { get; } = displayName;

    public string? Description { get; } = description;
}
