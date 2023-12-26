// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Performance.Runner;

internal class NoInputOutput : IPayload
{
    public static NoInputOutput NoInputOutputInstance { get; set; } = new();
}
