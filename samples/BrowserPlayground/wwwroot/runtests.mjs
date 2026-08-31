// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Headless node runner for the browser-wasm bundle.
//
// browser-wasm normally boots inside a browser, but Microsoft.Testing.Platform never
// touches the DOM, so the same bundle boots under node via the dotnet.js loader. This
// gives a CI-friendly, headless way to run the tests where the .NET exit code becomes
// the node process exit code and stdout/stderr flow straight through — no browser or
// web server required.
//
// Usage (from the published AppBundle folder, i.e. next to _framework/):
//   node runtests.mjs [mtp-args...]
//
// e.g.  node runtests.mjs --minimum-expected-tests 1
import { dotnet } from './_framework/dotnet.js';

const slowTestThresholdEnvironmentVariable = 'MTP_PROGRESS_SLOW_TEST_SECONDS';
const runtimeBuilder = dotnet.withApplicationArguments(...process.argv.slice(2));
const slowTestThreshold = process.env[slowTestThresholdEnvironmentVariable];
if (slowTestThreshold !== undefined) {
    runtimeBuilder.withEnvironmentVariable(slowTestThresholdEnvironmentVariable, slowTestThreshold);
}

const { runMain } = await runtimeBuilder.create();

// runMain() boots the same bundle under Node and resolves to the exit code of the sample's
// Program.Main (defined by the top-level statements in Program.cs).
const exitCode = await runMain();

// Set exitCode rather than calling process.exit(): process.exit() can terminate Node before
// redirected stdout/stderr has flushed (which would truncate the MTP output this runner promises
// to surface). Setting exitCode lets Node drain its streams and exit with the .NET result.
process.exitCode = exitCode;
