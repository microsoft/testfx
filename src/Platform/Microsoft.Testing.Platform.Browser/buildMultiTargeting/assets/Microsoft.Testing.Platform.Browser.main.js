// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

const status = document.querySelector('[role=status]');

globalThis.addEventListener('error', event => {
    console.error(`Unhandled browser error: ${event.message}`);
});

globalThis.addEventListener('unhandledrejection', event => {
    console.error(`Unhandled browser rejection: ${String(event.reason)}`);
});

try {
    const launcherAvailable = typeof globalThis.__mtpBrowserGetArguments === 'function'
        && typeof globalThis.__mtpBrowserComplete === 'function';
    const argumentsFromLauncher = launcherAvailable
        ? await globalThis.__mtpBrowserGetArguments()
        : [];
    const { dotnet } = await import('../_framework/dotnet.js');
    const { runMain } = await dotnet
        .withApplicationArguments(...argumentsFromLauncher)
        .create();

    const exitCode = await runMain();
    status.textContent = exitCode === 0 ? 'Passed' : `Failed (exit code ${exitCode})`;
    if (launcherAvailable) {
        await globalThis.__mtpBrowserComplete({ exitCode });
    }
}
catch (error) {
    const message = error instanceof Error
        ? `${error.name}: ${error.message}\n${error.stack ?? ''}`
        : String(error);
    console.error(message);
    status.textContent = 'Failed (launcher error)';
    if (typeof globalThis.__mtpBrowserComplete === 'function') {
        await globalThis.__mtpBrowserComplete({ exitCode: 1, error: message });
    }
}
