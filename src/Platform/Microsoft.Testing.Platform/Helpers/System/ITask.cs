// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Match the Task API")]
internal interface ITask
{
    Task Run(Func<Task> function, CancellationToken cancellationToken);

    Task Run(Action action);

    Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken);

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken);

    Task WhenAll(params Task[] tasks);

    Task Delay(int millisecondDelay);

    Task Delay(TimeSpan timeSpan, CancellationToken cancellation);
}
