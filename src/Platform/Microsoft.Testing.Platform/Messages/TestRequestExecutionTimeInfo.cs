// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

// This internal type was removed in https://github.com/microsoft/testfx/pull/8514 but
// is referenced by older shipped versions of Microsoft.Testing.Extensions.MSBuild
// (packaged as Microsoft.Testing.Platform.MSBuild <= 2.2.x). When such an older extension
// is loaded against this newer Microsoft.Testing.Platform assembly — a common scenario
// when another package (e.g. xunit.v3.core.mtp-v2) pins an older Microsoft.Testing.Platform.MSBuild
// version while a newer package (e.g. Microsoft.Testing.Extensions.TrxReport) brings in this
// newer Microsoft.Testing.Platform — the old MSBuildConsumer's static initializer fails with:
//   System.TypeLoadException: Could not load type
//   'Microsoft.Testing.Platform.Extensions.Messages.TestRequestExecutionTimeInfo'
//   from assembly 'Microsoft.Testing.Platform, Version=2.3.0.0, ...'
// To preserve binary compatibility with previously shipped extensions, the type definition
// must continue to exist in this assembly even though no code in this version publishes it.
// The internal extension projects which still need the InternalsVisibleTo grant (e.g.
// Microsoft.Testing.Extensions.MSBuild) can still resolve the type at load time. The type
// is never published by the current platform, so the consumer's switch arm that handles it
// simply never fires.
internal readonly struct TestRequestExecutionTimeInfo(TimingInfo timingInfo) : IData
{
    public string DisplayName => nameof(TestRequestExecutionTimeInfo);

    public string? Description => "Information about the test execution times.";

    public TimingInfo TimingInfo { get; } = timingInfo;

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestRequestExecutionTimeInfo { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
            .Append(", TimingInfo = ")
            .Append(TimingInfo.ToString())
            .Append(" }");

        return builder.ToString();
    }
}
