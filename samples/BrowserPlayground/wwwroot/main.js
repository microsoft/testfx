// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Browser boot module. The WebAssembly runtime loads this file first (it is the
// project's WasmMainJSPath). It creates the .NET runtime through dotnet.js and runs the
// sample's Program.Main (defined by the top-level statements in Program.cs), which boots
// Microsoft.Testing.Platform.
import { dotnet } from './_framework/dotnet.js';

const { runMain } = await dotnet
    // A browser page has no argv, so Microsoft.Testing.Platform command-line options are
    // taken from the page's query string, e.g.:
    //   index.html?arg=--minimum-expected-tests&arg=1
    .withApplicationArgumentsFromQuery()
    .create();

// runMain() invokes Program.Main and resolves to its exit code (0 = all tests passed,
// non-zero = failures / policy violation).
const exitCode = await runMain();

// Surface the result so a headless browser driver (Playwright/Puppeteer) can read it,
// and so a human sees it in the dev-tools console / tab title.
console.log(`Microsoft.Testing.Platform exit code: ${exitCode}`);
globalThis.mtpExitCode = exitCode;
document.title = `exit:${exitCode}`;
