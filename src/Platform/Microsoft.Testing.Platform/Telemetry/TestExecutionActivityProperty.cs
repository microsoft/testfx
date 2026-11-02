// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class TestExecutionActivityProperty(
    TestExecutionActivityReservation reservation,
    bool isFinalResult) : IProperty
{
    public TestExecutionActivityReservation Reservation { get; } = reservation;

    public bool IsFinalResult { get; } = isFinalResult;
}
