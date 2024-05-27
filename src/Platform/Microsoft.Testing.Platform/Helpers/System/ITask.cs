// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Match the Task API")]
internal interface ITask
{
    Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken);

    Task Run(Action action, CancellationToken cancellationToken);

    Task WhenAll(IEnumerable<Task> tasks);

    Task WhenAll(params Task[] tasks);

    Task<Task> WhenAny(params Task[] tasks);

    Task Delay(int millisecondDelay);

    Task Delay(TimeSpan timeSpan, CancellationToken cancellation);

    Task Delay(TimeSpan timeSpan);
}
