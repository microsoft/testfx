// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform;

// Borrowed from dotnet/sdk with some tweaks to allow testing
internal static class UILanguageOverride
{
#pragma warning disable SA1310 // Field names should not contain underscore - That's how we want to name the environment variables!
    private const string TESTINGPLATFORM_UI_LANGUAGE = nameof(TESTINGPLATFORM_UI_LANGUAGE);
    private const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);
#pragma warning restore SA1310 // Field names should not contain underscore

    private const string VSLANG = nameof(VSLANG);
    private const string PreferredUILang = nameof(PreferredUILang);

    internal static void SetCultureSpecifiedByUser(IEnvironment environment)
    {
        CultureInfo? language = GetOverriddenUILanguage(environment);
        if (language == null)
        {
            return;
        }

        ApplyOverrideToCurrentProcess(language);
        FlowOverrideToChildProcesses(language, environment);
    }

    private static void ApplyOverrideToCurrentProcess(CultureInfo language)
        => CultureInfo.DefaultThreadCurrentUICulture = language;

    private static CultureInfo? GetOverriddenUILanguage(IEnvironment environment)
    {
        // For MTP, TESTINGPLATFORM_UI_LANGUAGE environment variable is the highest precedence.
        string? testingPlatformLanguage = environment.GetEnvironmentVariable(TESTINGPLATFORM_UI_LANGUAGE);
        if (testingPlatformLanguage is not null)
        {
            try
            {
                return CultureInfo.GetCultureInfo(testingPlatformLanguage);
            }
            catch (CultureNotFoundException)
            {
            }
        }

        // If TESTINGPLATFORM_UI_LANGUAGE is not set or is invalid, then DOTNET_CLI_UI_LANGUAGE=<culture name> is the main way for users to customize the CLI's UI language.
        string? dotnetCliLanguage = environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
        if (dotnetCliLanguage is not null)
        {
            try
            {
                return new CultureInfo(dotnetCliLanguage);
            }
            catch (CultureNotFoundException)
            {
            }
        }

        // VSLANG=<lcid> is set by VS and we respect that as well so that we will respect the VS
        // language preference if we're invoked by VS.
        string? vsLang = environment.GetEnvironmentVariable(VSLANG);
        if (vsLang != null && int.TryParse(vsLang, out int vsLcid))
        {
            try
            {
                return new CultureInfo(vsLcid);
            }
            catch (ArgumentOutOfRangeException)
            {
            }
            catch (CultureNotFoundException)
            {
            }
        }

        return null;
    }

    private static void FlowOverrideToChildProcesses(CultureInfo language, IEnvironment environment)
    {
        // Do not override any environment variables that are already set as we do not want to clobber a more granular setting with our global setting.
        SetIfNotAlreadySet(TESTINGPLATFORM_UI_LANGUAGE, language.Name, environment);
        SetIfNotAlreadySet(DOTNET_CLI_UI_LANGUAGE, language.Name, environment);
        SetIfNotAlreadySet(VSLANG, language.LCID.ToString(CultureInfo.CurrentCulture), environment); // for tools following VS guidelines to just work in CLI
        SetIfNotAlreadySet(PreferredUILang, language.Name, environment); // for C#/VB targets that pass $(PreferredUILang) to compiler
    }

    private static void SetIfNotAlreadySet(string environmentVariableName, string value, IEnvironment environment)
    {
        string? currentValue = environment.GetEnvironmentVariable(environmentVariableName);
        if (currentValue == null)
        {
            environment.SetEnvironmentVariable(environmentVariableName, value);
        }
    }
}
