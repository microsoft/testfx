// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.PlatformServices.ObjectModel;

internal sealed class AdapterSettingsException : Exception
{
    internal AdapterSettingsException(string? message)
        : base(message)
    {
    }
}
