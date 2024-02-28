// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal static class TelemetryProperties
{
    public const string VersionPropertyName = $"telemetry version";
    public const string SessionId = "session id";
    public const string ReporterIdPropertyName = $"reporter id";
    public const string IsCIPropertyName = "is ci";

    public const string VersionValue = "18";

    public const string True = "true";
    public const string False = "false";

    public static class HostProperties
    {
        public const string ApplicationModePropertyName = "application mode";
        public const string IsNativeAotPropertyName = "native aot";
        public const string IsHotReloadPropertyName = "hot reload";
        public const string IsDevelopmentRepositoryPropertyName = "dev repository";
        public const string ExitCodePropertyName = "exit code";
        public const string TestHostPropertyName = "testhost";
        public const string HasExitedGracefullyPropertyName = "graceful exit";
        public const string TestingPlatformVersionPropertyName = "version";
        public const string TestAdapterIdPropertyName = "adapter id";
        public const string TestAdapterVersionPropertyName = "adapter version";
        public const string ExtensionsPropertyName = "extensions";
        public const string FrameworkDescriptionPropertyName = "framework";
        public const string RuntimeIdentifierPropertyName = "runtime";
        public const string ProcessArchitecturePropertyName = "architecture";
        public const string OSArchitecturePropertyName = "os architecture";
        public const string OSDescriptionPropertyName = "os";

        // Reported in dotnet/testingplatform/host/testhostbuilt.

        // Start of the application basically. This is in the static call to CreateBuilder. We could get closer to the startup of the app
        // but this is close enough without making it more complicated.
        public const string CreateBuilderStart = "create builder start";

        // When the initial telemetry is sent, should be close enough to be done with creating to not warrant sending the creation stop
        // timestamp in the next telemetry event (e.g. console exit).
        public const string CreateBuilderStop = "create builder stop";

        // Start of testHost build.
        public const string BuildBuilderStart = "build builder start";

        // End of testHost build, telemetry is sent as part of this build, so this is timestamp when telemetry was sent,
        // the data are the same as CreateBuilderStop at the moment.
        public const string BuildBuilderStop = "build builder stop";

        // True when this is built as debug, so we can filter out dev builds.
        public const string IsDebugBuild = "debug build";

        // When we report telemetry and there is debugger, the timing is all off, we don't want these runs to
        // be part of our timings.
        public const string IsDebuggerAttached = "debugger attached";

        // Reported in dotnet/testingplatform/host/consoletesthostexit or dotnet/testingplatform/host/testhostcontrollerstesthostexit.

        // Start of RunAsync (or actually RunAsyncInternal because that is easier).
        public const string RunStart = "run start";

        // Stop of run async, when the telemetry is sent. This is basically process exit.
        public const string RunStop = "run stop";
    }

    public static class RequestProperties
    {
        public const string IsFilterEnabledPropertyName = "filter enabled";

        public const string TotalDiscoveredTestsPropertyName = "total discovered";

        public const string TotalRanTestsPropertyName = "total ran";
        public const string TotalPassedTestsPropertyName = "total passed";
        public const string TotalFailedTestsPropertyName = "total failed";
        public const string TotalPassedRetriesPropertyName = "total passed retries";
        public const string TotalFailedRetriesPropertyName = "total failed retries";

        // Start of a single request, like discovery or run.
        public const string RequestStart = "request start";

        // Stop of a single request, like discovery or run.
        public const string RequestStop = "request stop";

        // Reported in:
        // - dotnet/testingplatform/host/consoletesthostexit
        // - dotnet/testingplatform/execution/testsdiscovery
        // - "dotnet/testingplatform/execution/testsrun";
        // Start of loading adapters and other extensions. This can include time spent running user code, so it might be very variable.
        public const string AdapterLoadStart = "adapter load start";

        // Start of loading adapters and other extensions.
        public const string AdapterLoadStop = "adapter load stop";

        // Start of execution of request, most time in there is assumed to be user code.
        public const string RequestExecuteStart = "request execute start";

        // Stop of execution of request, time till RequestStop is mostly spent disposing services.
        public const string RequestExecuteStop = "request execute stop";
    }

    public static class ApplicationMode
    {
        public const string Console = "Console";
        public const string VSTestAdapterMode = "VSTestAdapterMode";
        public const string Server = "Server";
        public const string TestHostControllers = "TestHostControllers";
        public const string Tool = "Tool";
    }
}
