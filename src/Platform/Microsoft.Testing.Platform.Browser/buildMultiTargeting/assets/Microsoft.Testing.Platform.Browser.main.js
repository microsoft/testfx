// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { dotnet } from './_framework/dotnet.js';

const status = document.querySelector('[role=status]');
const argumentsFromLauncher = globalThis.__mtpBrowserArguments;

if (!Array.isArray(argumentsFromLauncher)) {
    throw new Error('Microsoft.Testing.Platform.Browser did not inject the test application arguments.');
}

globalThis.addEventListener('error', event => {
    console.error(`Unhandled browser error: ${event.message}`);
});

globalThis.addEventListener('unhandledrejection', event => {
    console.error(`Unhandled browser rejection: ${String(event.reason)}`);
});

try {
    const { runMain } = await dotnet
        .withApplicationArguments(...argumentsFromLauncher)
        .create();

    const exitCode = await runMain();
    globalThis.__mtpBrowserResult = { completed: true, exitCode };
    status.textContent = exitCode === 0 ? 'Passed' : `Failed (exit code ${exitCode})`;
}
catch (error) {
    globalThis.__mtpBrowserResult = {
        completed: true,
        exitCode: 1,
        error: error instanceof Error ? error.stack ?? error.message : String(error),
    };
    status.textContent = 'Failed (launcher error)';
    throw error;
}
