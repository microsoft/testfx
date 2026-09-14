// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { dotnet } from './_framework/dotnet.js';

const status = document.querySelector('[role=status]');
const browserApi = globalThis.testingPlatformBrowser;

if (browserApi?.contractVersion !== 1
    || typeof browserApi.getArguments !== 'function'
    || typeof browserApi.complete !== 'function') {
    throw new Error('Microsoft.Testing.Platform.Browser did not provide browser API version 1.');
}

const argumentsFromLauncher = browserApi.getArguments();

globalThis.addEventListener('error', event => {
    console.error(`Unhandled browser error: ${event.message}`);
});

globalThis.addEventListener('unhandledrejection', event => {
    console.error(`Unhandled browser rejection: ${String(event.reason)}`);
});

let exitCode;
let failure;
try {
    const { runMain } = await dotnet
        .withApplicationArguments(...argumentsFromLauncher)
        .create();

    exitCode = await runMain();
    status.textContent = exitCode === 0 ? 'Passed' : `Failed (exit code ${exitCode})`;
}
catch (error) {
    failure = error;
    exitCode = 1;
    console.error(error instanceof Error ? error.stack ?? error.message : String(error));
    status.textContent = 'Failed (launcher error)';
}

browserApi.complete(exitCode);
if (failure !== undefined) {
    throw failure;
}
