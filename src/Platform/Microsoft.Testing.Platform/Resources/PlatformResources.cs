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

    internal static string @InvalidTestApplicationBuilderTypeForAI => GetResourceString("InvalidTestApplicationBuilderTypeForAI");

    internal static string @UnexpectedStateErrorMessage => GetResourceString("UnexpectedStateErrorMessage");

    internal static string @UnreachableLocationErrorMessage => GetResourceString("UnreachableLocationErrorMessage");

    internal static string @UnexpectedExceptionDuringByteConversionErrorMessage => GetResourceString("UnexpectedExceptionDuringByteConversionErrorMessage");

    internal static string @InternalLoopAsyncDidNotExitSuccessfullyErrorMessage => GetResourceString("InternalLoopAsyncDidNotExitSuccessfullyErrorMessage");

#if IS_MTP_UNIT_TESTS
    internal static string @PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage => GetResourceString("PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage");

    internal static string @PlatformCommandLineExitOnProcessExitSingleArgument => GetResourceString("PlatformCommandLineExitOnProcessExitSingleArgument");

    internal static string @PlatformCommandLineDiagnosticOptionIsMissing => GetResourceString("PlatformCommandLineDiagnosticOptionIsMissing");

    internal static string @PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests => GetResourceString("PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests");

    internal static string @PlatformCommandLinePortOptionSingleArgument => GetResourceString("PlatformCommandLinePortOptionSingleArgument");

    internal static string @PlatformCommandLineTimeoutArgumentErrorMessage => GetResourceString("PlatformCommandLineTimeoutArgumentErrorMessage");
#endif

#endif
}
