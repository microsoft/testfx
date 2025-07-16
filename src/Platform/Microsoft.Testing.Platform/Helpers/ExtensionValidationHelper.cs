// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Helpers;

internal static class ExtensionValidationHelper
{
    /// <summary>
    /// Validates that an extension with the same UID is not already registered in the collection.
    /// Throws an InvalidOperationException with a detailed error message if duplicates are found.
    /// </summary>
    /// <typeparam name="T">The type of extension being validated.</typeparam>
    /// <param name="existingExtensions">Collection of existing extensions to check against.</param>
    /// <param name="newExtension">The new extension being registered.</param>
    /// <param name="extensionSelector">Function to extract the IExtension from the collection item.</param>
    public static void ValidateUniqueExtension<T>(this IEnumerable<T> existingExtensions, IExtension newExtension, Func<T, IExtension> extensionSelector)
    {
        Guard.NotNull(existingExtensions);
        Guard.NotNull(newExtension);
        Guard.NotNull(extensionSelector);

        T[] duplicates = existingExtensions.Where(x => extensionSelector(x).Uid == newExtension.Uid).ToArray();
        if (duplicates.Length > 0)
        {
            IExtension[] allDuplicates = duplicates.Select(extensionSelector).Concat([newExtension]).ToArray();
            string typesList = string.Join(", ", allDuplicates.Select(x => $"'{x.GetType()}'"));
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.ExtensionWithSameUidAlreadyRegisteredErrorMessage, newExtension.Uid, typesList));
        }
    }

    /// <summary>
    /// Validates that an extension with the same UID is not already registered in the collection.
    /// This overload is for simple collections where the items are extensions themselves.
    /// </summary>
    /// <param name="existingExtensions">Collection of existing extensions to check against.</param>
    /// <param name="newExtension">The new extension being registered.</param>
    public static void ValidateUniqueExtension(this IEnumerable<IExtension> existingExtensions, IExtension newExtension)
        => existingExtensions.ValidateUniqueExtension(newExtension, x => x);
}