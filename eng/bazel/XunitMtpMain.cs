// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Mirrors the entry point and self-registered extensions that xunit.v3.core.mtp-v2 and
// Microsoft.Testing.Platform.MSBuild generate during an MSBuild build.
return await Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.RunAsync(args, static (builder, arguments) =>
{
    Microsoft.Testing.Platform.MSBuild.TestingPlatformBuilderHook.AddExtensions(builder, arguments);
    Microsoft.Testing.Extensions.Telemetry.TestingPlatformBuilderHook.AddExtensions(builder, arguments);
});
