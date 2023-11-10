// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

internal readonly struct TestRequestExecutionTimeInfo(TimingInfo timingInfo) : IData
{
    public readonly string DisplayName => nameof(TestRequestExecutionTimeInfo);

    public readonly string? Description => "Information about the test execution times.";

    public TimingInfo TimingInfo { get; } = timingInfo;
}
