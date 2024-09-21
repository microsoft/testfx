// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter;

/// <summary>
/// Stores a flag that is used throughout the adapter to determine if we are using source generator or not. This is a workaround for passing the value around
/// to all components that need to respect it, because we don't have an easy way to flow it through constructors. By default the value is unset and false,
/// so by default we don't use source generator. We only use it when we call the hook (which we do from the source generated code), and that way we activate
/// the different components that look at metadata rather than using standard reflection and file system providers.
/// </summary>
internal static class SourceGeneratorToggle
{
    private static bool? s_useSourceGenerator;

    public static bool UseSourceGenerator
    {
        get => s_useSourceGenerator is true;

        set
        {
            ApplicationStateGuard.Ensure(value, "UseSourceGenerator was set to false. It can be set only to true from the ReflectionMetadataHook. If the hook is not called it remains false, because we are not using source generator.");
            ApplicationStateGuard.Ensure(s_useSourceGenerator == null, "UseSourceGenerator was set multiple times. It can be set only once, from the ReflectionMetadataHook. If the hook is not called it remains false, because we are not using source generator.");

            s_useSourceGenerator = true;
        }
    }
}
