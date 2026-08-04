// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !IS_CORE_MTP
using System.Resources;
#endif

using Microsoft.CodeAnalysis;

#if !IS_CORE_MTP
#pragma warning disable IDE0005 // Using directive is unnecessary.
using Microsoft.Testing.Platform.Builder;
#pragma warning restore IDE0005 // Using directive is unnecessary.
#endif

namespace Microsoft.Testing.Platform.Resources;

// We want to avoid the core platform from exposing IVT to extensions as much as we can.
// So, we make the PlatformResources embedded.
// And, when this file is linked to extension projects, we create ResourceManager pointing out to the core MTP assembly.
// The resource properties defined under !IS_CORE_MTP are the properties that are accessed by the extension projects.
// In the future, we might consider duplicating those specific resources in each extension resx if needed.
[Embedded]
internal static partial class PlatformResources
{
#if !IS_CORE_MTP

    internal static ResourceManager ResourceManager => field ??= new ResourceManager("Microsoft.Testing.Platform.Resources.PlatformResources", typeof(ITestApplication).Assembly);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetResourceString(string resourceKey) => ResourceManager.GetString(resourceKey, null)!;

    internal static string @InternalLoopAsyncDidNotExitSuccessfullyErrorMessage => GetResourceString("InternalLoopAsyncDidNotExitSuccessfullyErrorMessage");

    internal static string @NamedPipePathTooLongErrorMessage => GetResourceString("NamedPipePathTooLongErrorMessage");

    internal static string @NamedPipeDirectoryNotWritableErrorMessage => GetResourceString("NamedPipeDirectoryNotWritableErrorMessage");

#if IS_MTP_UNIT_TESTS
    internal static string @ArtifactPostProcessingManifestInvalid => GetResourceString("ArtifactPostProcessingManifestInvalid");

    internal static string @ActiveTestsRunning_FullTestsCount => GetResourceString("ActiveTestsRunning_FullTestsCount");

    internal static string @ActiveTestsRunning_MoreTestsCount => GetResourceString("ActiveTestsRunning_MoreTestsCount");

    internal static string @PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage => GetResourceString("PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage");

    internal static string @PlatformCommandLineDiscoverTestsInvalidArgument => GetResourceString("PlatformCommandLineDiscoverTestsInvalidArgument");

    internal static string @PlatformCommandLineServerInvalidArgument => GetResourceString("PlatformCommandLineServerInvalidArgument");

    internal static string @PlatformCommandLineZeroTestsPolicyInvalidArgument => GetResourceString("PlatformCommandLineZeroTestsPolicyInvalidArgument");

    internal static string @PlatformCommandLineDotnetTestCliRequiresPipe => GetResourceString("PlatformCommandLineDotnetTestCliRequiresPipe");

    internal static string @PlatformCommandLineDotnetTestTransportInvalid => GetResourceString("PlatformCommandLineDotnetTestTransportInvalid");

    internal static string @PlatformCommandLineDotnetTestHttpEndpointInvalid => GetResourceString("PlatformCommandLineDotnetTestHttpEndpointInvalid");

    internal static string @PlatformCommandLineDotnetTestHttpTokenInvalid => GetResourceString("PlatformCommandLineDotnetTestHttpTokenInvalid");

    internal static string @PlatformCommandLineDotnetTestTransportConflict => GetResourceString("PlatformCommandLineDotnetTestTransportConflict");

    internal static string @PlatformCommandLineDotnetTestHttpOptionsRequireTransport => GetResourceString("PlatformCommandLineDotnetTestHttpOptionsRequireTransport");

    internal static string @PlatformCommandLineDotnetTestCliRequiresTransport => GetResourceString("PlatformCommandLineDotnetTestCliRequiresTransport");

    internal static string @PlatformCommandLineDotnetTestHttpRequiresEndpointAndToken => GetResourceString("PlatformCommandLineDotnetTestHttpRequiresEndpointAndToken");

    internal static string @PlatformCommandLinePipeTransportNotSupportedOnBrowser => GetResourceString("PlatformCommandLinePipeTransportNotSupportedOnBrowser");

    internal static string @PlatformCommandLinePipeTransportNotSupportedOnWasi => GetResourceString("PlatformCommandLinePipeTransportNotSupportedOnWasi");

    internal static string @PlatformCommandLineExitOnProcessExitSingleArgument => GetResourceString("PlatformCommandLineExitOnProcessExitSingleArgument");

    internal static string @PlatformCommandLineDiagnosticOptionIsMissing => GetResourceString("PlatformCommandLineDiagnosticOptionIsMissing");

    internal static string @PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests => GetResourceString("PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests");

    internal static string @OnlyOneFilterSupported => GetResourceString("OnlyOneFilterSupported");

    internal static string @PlatformCommandLineMinimumExpectedTestsOptionSingleArgument => GetResourceString("PlatformCommandLineMinimumExpectedTestsOptionSingleArgument");

    internal static string @PlatformCommandLinePortOptionSingleArgument => GetResourceString("PlatformCommandLinePortOptionSingleArgument");

    internal static string @PlatformCommandLineTimeoutArgumentErrorMessage => GetResourceString("PlatformCommandLineTimeoutArgumentErrorMessage");

    internal static string @Aborted => GetResourceString("Aborted");

    internal static string @ZeroTestsRan => GetResourceString("ZeroTestsRan");

    internal static string @Failed => GetResourceString("Failed");

    internal static string @Passed => GetResourceString("Passed");

    internal static string @TotalLowercase => GetResourceString("TotalLowercase");

    internal static string @FailedLowercase => GetResourceString("FailedLowercase");

    internal static string @SucceededLowercase => GetResourceString("SucceededLowercase");

    internal static string @SkippedLowercase => GetResourceString("SkippedLowercase");
#endif

#endif
}
