// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public abstract record class DataWithSessionUid(string DisplayName, string? Description, SessionUid SessionUid)
    : PropertyBagData(DisplayName, Description);
