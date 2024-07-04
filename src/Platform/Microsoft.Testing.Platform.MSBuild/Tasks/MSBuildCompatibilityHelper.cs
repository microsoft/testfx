// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS8618 // Properties below are set by MSBuild.

using System.Reflection;

using Microsoft.Build.Framework;

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// This class wraps APIs of MSBuild that are newer than the oldest supported .NET SDK. Our task can get loaded into .NET 6 SDK
/// and we need to guard the usage of these newer APIs.
/// </summary>
internal static class MSBuildCompatibilityHelper
{
    // This was added in late 17.10.0, the api to call is the old and stable Log api, but if we send multiline messages to that api
    // before multiline is supported, we get one message per line, and it looks really messy.
    // https://github.com/dotnet/msbuild/pull/9699
    private const string TerminalLoggerMultilineHandlerFeatureName = "TerminalLogger_MultiLineHandler";

    // Terminal logger testing support that consumes ExtendedMessages was added.
    private static readonly Version TerminalLoggerWithExtendedMessagesSupported = new(17, 10, 0);

    // This adds the generic api that allows checking for presence of a feature https://github.com/dotnet/msbuild/pull/9665.
    private static readonly Version FeatureChecksAdded = new(17, 10, 0);

    private static Version? s_msBuildVersion;

    private static bool? s_supportsMultiline;

    private static bool? s_supportsTerminalLoggerWithExtendedMessages;

    private static Version GetMsBuildVersion()
    {
        if (s_msBuildVersion == null)
        {
            var fileVersionAttribute = (AssemblyFileVersionAttribute?)typeof(ILogger).Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false).FirstOrDefault();
            s_msBuildVersion = fileVersionAttribute?.Version != null ? new Version(fileVersionAttribute.Version) : new Version();
        }

        return s_msBuildVersion;
    }

    public static bool SupportsMultiLine()
    {
        s_supportsMultiline ??= GetMsBuildVersion() >= FeatureChecksAdded
                && MSBuildNewApiWrapper.UnsafeCheckFeature(TerminalLoggerMultilineHandlerFeatureName);

        return s_supportsMultiline.Value;
    }

    public static bool SupportsTerminalLoggerWithExtendedMessages()
    {
        s_supportsTerminalLoggerWithExtendedMessages ??= GetMsBuildVersion() >= TerminalLoggerWithExtendedMessagesSupported;

        return s_supportsTerminalLoggerWithExtendedMessages.Value;
    }

    public static bool TryWriteExtendedMessage(IBuildEngine engine, string messageType, string message, Dictionary<string, string?> metadata)
    {
        if (!SupportsTerminalLoggerWithExtendedMessages())
        {
            return false;
        }

        MSBuildNewApiWrapper.UnsafeWriteExtendedMessage(engine, messageType, message, metadata);

        return true;
    }

    private static class MSBuildNewApiWrapper
    {
        public static bool UnsafeCheckFeature(string featureName)
        {
            FeatureStatus featureStatus = Features.CheckFeatureAvailability(featureName);

            return featureStatus is FeatureStatus.Available or FeatureStatus.Preview;
        }

        public static void UnsafeWriteExtendedMessage(IBuildEngine engine, string messageType, string message, Dictionary<string, string?> metadata)
        {
            var extendedMessage = new ExtendedBuildMessageEventArgs(messageType, message, null, null, MessageImportance.High)
            {
                ExtendedMetadata = metadata,
            };

            engine.LogMessageEvent(extendedMessage);
        }
    }
}
