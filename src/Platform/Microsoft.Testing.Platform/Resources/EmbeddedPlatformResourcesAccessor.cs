// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if TESTING_PLATFORM_SOURCE_EMBEDDED

using System.Resources;

namespace Microsoft.Testing.Platform.Resources;

/// <summary>
/// Provides localized resource strings for source-embedded platform helpers.
/// This class mirrors a subset of PlatformResources for use in projects that
/// source-embed platform helper files and cannot reference PlatformResources directly.
/// </summary>
internal static class EmbeddedPlatformResources
{
#pragma warning disable IDE0032 // Use auto property - follows the same pattern as Arcade-generated resx accessors
    private static ResourceManager? s_resourceManager;
#pragma warning restore IDE0032

    private static ResourceManager ResourceManager
        => s_resourceManager ??= new("Microsoft.Testing.Platform.Resources.EmbeddedPlatformResources", typeof(EmbeddedPlatformResources).Assembly);

    internal static string InternalLoopAsyncDidNotExitSuccessfullyErrorMessage
        => ResourceManager.GetString(nameof(InternalLoopAsyncDidNotExitSuccessfullyErrorMessage), CultureInfo.CurrentUICulture)!;

    internal static string NoSerializerRegisteredWithIdErrorMessage
        => ResourceManager.GetString(nameof(NoSerializerRegisteredWithIdErrorMessage), CultureInfo.CurrentUICulture)!;

    internal static string NoSerializerRegisteredWithTypeErrorMessage
        => ResourceManager.GetString(nameof(NoSerializerRegisteredWithTypeErrorMessage), CultureInfo.CurrentUICulture)!;

    internal static string UnexpectedExceptionDuringByteConversionErrorMessage
        => ResourceManager.GetString(nameof(UnexpectedExceptionDuringByteConversionErrorMessage), CultureInfo.CurrentUICulture)!;

    internal static string UnexpectedStateErrorMessage
        => ResourceManager.GetString(nameof(UnexpectedStateErrorMessage), CultureInfo.CurrentUICulture)!;

    internal static string UnreachableLocationErrorMessage
        => ResourceManager.GetString(nameof(UnreachableLocationErrorMessage), CultureInfo.CurrentUICulture)!;
}

#endif
