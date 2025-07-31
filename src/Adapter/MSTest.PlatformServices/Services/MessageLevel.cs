// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.PlatformServices;

internal static class MessageLevelExtensions
{
    public static TestMessageLevel ToTestMessageLevel(this MessageLevel messageLevel)
        => messageLevel switch
        {
            MessageLevel.Informational => TestMessageLevel.Informational,
            MessageLevel.Warning => TestMessageLevel.Warning,
            MessageLevel.Error => TestMessageLevel.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(messageLevel)),
        };
}
