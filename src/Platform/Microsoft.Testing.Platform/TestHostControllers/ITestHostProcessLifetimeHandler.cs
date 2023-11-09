// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

public interface ITestHostProcessLifetimeHandler : ITestHostControllersExtension
{
    Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken);

    Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation);

    Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation);
}
