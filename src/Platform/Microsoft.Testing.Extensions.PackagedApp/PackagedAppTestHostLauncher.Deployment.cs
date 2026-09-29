// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class PackagedAppTestHostLauncher
{
#if !PACKAGEDAPP_WINRT
#pragma warning disable IDE0051 // Compiled into the non-Windows flavor so recipe materialization is unit-testable.
#endif
    private static string MaterializeAppxRecipeLayout(
        string targetFileName,
        out string? appxRecipePath,
        Func<string, string>? resolveFinalPath = null)
    {
#if PACKAGEDAPP_WINRT
        resolveFinalPath ??= ResolveFinalPath;
#else
        resolveFinalPath ??= static path => Path.GetFullPath(path);
#endif
        string sourceDirectory = Path.GetDirectoryName(targetFileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{targetFileName}'.");
        string[] recipePaths = Directory.GetFiles(sourceDirectory, "*.build.appxrecipe", SearchOption.TopDirectoryOnly);
        if (recipePaths.Length == 0)
        {
            appxRecipePath = null;
            return targetFileName;
        }

        if (recipePaths.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one .build.appxrecipe beside '{targetFileName}', but found {recipePaths.Length}.");
        }

        appxRecipePath = recipePaths[0];
        var recipe = XDocument.Load(appxRecipePath);
        if (IsAppxRecipeAlreadyMaterialized(recipe, sourceDirectory))
        {
            return targetFileName;
        }

        string layoutDirectory = Path.Combine(sourceDirectory, "_MtpPackageLayout");
        if (Directory.Exists(layoutDirectory))
        {
            Directory.Delete(layoutDirectory, recursive: true);
        }

        Directory.CreateDirectory(layoutDirectory);
        string recipeDirectory = Path.GetDirectoryName(Path.GetFullPath(appxRecipePath))!;
        string requestedTargetPath = resolveFinalPath(Path.GetFullPath(targetFileName));
        string? targetPackagePath = null;
        foreach (XElement item in recipe.Descendants().Where(element =>
            element.Name.LocalName is "AppXManifest" or "AppxPackagedFile"))
        {
            string? sourcePath = item.Attribute("Include")?.Value;
            string? packagePath = item.Elements().FirstOrDefault(element => element.Name.LocalName == "PackagePath")?.Value;
            if (sourcePath is null || packagePath is null)
            {
                continue;
            }

            sourcePath = Path.GetFullPath(Uri.UnescapeDataString(sourcePath), recipeDirectory);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"The AppX recipe '{appxRecipePath}' references missing payload '{sourcePath}'.",
                    sourcePath);
            }

            sourcePath = resolveFinalPath(sourcePath);
            if (item.Name.LocalName == "AppxPackagedFile"
                && string.Equals(sourcePath, requestedTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                targetPackagePath = packagePath;
            }

            string destinationPath = Path.Combine(
                layoutDirectory,
                packagePath.Replace('\\', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        if (targetPackagePath is null)
        {
            throw new InvalidOperationException(
                $"The AppX recipe '{appxRecipePath}' does not package the requested executable '{targetFileName}'.");
        }

        string normalizedTargetPackagePath = targetPackagePath.Replace('\\', Path.DirectorySeparatorChar);
        string materializedTargetPath = Path.Combine(layoutDirectory, normalizedTargetPackagePath);
        var manifestInfo = AppxManifestInfo.ReadFromManifest(
            Path.Combine(layoutDirectory, AppxManifestInfo.AppxManifestFileName));
        bool targetIsManifestExecutable = manifestInfo.Applications.Any(application =>
            application.Executable is not null
            && string.Equals(
                application.Executable.Replace('\\', Path.DirectorySeparatorChar),
                normalizedTargetPackagePath,
                StringComparison.OrdinalIgnoreCase));
        bool targetIsClassicUwpEntrypoint = string.Equals(
            Path.GetDirectoryName(normalizedTargetPackagePath),
            "entrypoint",
            StringComparison.OrdinalIgnoreCase);
        // Classic UWP recipes package the managed target below entrypoint but activate a generated
        // root bootstrap with the same file name so the framework package runtime is initialized.
        AppxApplicationInfo application = (targetIsManifestExecutable
            ? manifestInfo.ResolveApplication(layoutDirectory, materializedTargetPath)
            : targetIsClassicUwpEntrypoint
                ? manifestInfo.ResolveApplication(Path.GetFileName(materializedTargetPath))
                : manifestInfo.ResolveApplication(layoutDirectory, materializedTargetPath))
            ?? throw new InvalidOperationException($"The AppX recipe layout '{layoutDirectory}' declares no application.");
        return application.Executable is { Length: > 0 } executable
            ? Path.Combine(layoutDirectory, executable.Replace('\\', Path.DirectorySeparatorChar))
            : throw new InvalidOperationException($"The AppX recipe layout '{layoutDirectory}' declares no executable.");
    }

#if PACKAGEDAPP_WINRT
    private static string? TryGetOptionValue(IReadOnlyList<string> arguments, string option)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            if (string.Equals(arguments[i], option, StringComparison.Ordinal))
            {
                return i + 1 < arguments.Count ? arguments[i + 1] : null;
            }

            if (TryGetInlineOptionValue(arguments[i], option, out string? value))
            {
                return value;
            }
        }

        return null;
    }

    private static void TryDeleteScratchDirectory(string? scratchDirectory)
    {
        if (scratchDirectory is null)
        {
            return;
        }

        try
        {
            if (Directory.Exists(scratchDirectory))
            {
                Directory.Delete(scratchDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Best-effort delete of packaged test-host scratch directory '{scratchDirectory}' failed: {ex}");
        }
    }
#endif

    private static bool TryGetInlineOptionValue(string argument, string option, out string? value)
    {
        if (argument.Length > option.Length
            && argument.StartsWith(option, StringComparison.Ordinal)
            && argument[option.Length] is '=' or ':')
        {
            value = argument.Substring(option.Length + 1);
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsAppxRecipeAlreadyMaterialized(XDocument recipe, string sourceDirectory)
    {
        XElement? manifestItem = recipe.Descendants().FirstOrDefault(element => element.Name.LocalName == "AppXManifest");
        string? manifestSourcePath = manifestItem?.Attribute("Include")?.Value;
        if (manifestSourcePath is null)
        {
            return false;
        }

        manifestSourcePath = Uri.UnescapeDataString(manifestSourcePath);
        if (!Path.IsPathFullyQualified(manifestSourcePath))
        {
            manifestSourcePath = Path.GetFullPath(Path.Combine(sourceDirectory, manifestSourcePath));
        }

        string existingManifestPath = Path.Combine(sourceDirectory, AppxManifestInfo.AppxManifestFileName);
        return File.Exists(existingManifestPath)
            && string.Equals(
                Path.GetFullPath(manifestSourcePath),
                Path.GetFullPath(existingManifestPath),
                StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> RedirectAppContainerFileSystemOptions(
        IReadOnlyList<string> arguments,
        string resultsScratchDirectory,
        string diagnosticScratchDirectory,
        bool removeMSBuildNode)
    {
        List<string> redirectedArguments = [.. arguments];
        if (removeMSBuildNode)
        {
            RemoveOptionWithValue(redirectedArguments, MSBuildNodeOption);
        }

        bool hasResultsDirectory = false;
        bool hasDiagnosticOutputDirectory = false;
        bool diagnosticEnabled = false;
        for (int i = 0; i < redirectedArguments.Count; i++)
        {
            string argument = redirectedArguments[i];
            diagnosticEnabled |= string.Equals(argument, "--diagnostic", StringComparison.Ordinal);
            if (string.Equals(argument, ResultsDirectoryOption, StringComparison.Ordinal)
                && i + 1 < redirectedArguments.Count)
            {
                redirectedArguments[i + 1] = resultsScratchDirectory;
                hasResultsDirectory = true;
                i++;
            }
            else if (TryGetInlineOptionValue(argument, ResultsDirectoryOption, out _))
            {
                redirectedArguments[i] = $"{ResultsDirectoryOption}{argument[ResultsDirectoryOption.Length]}{resultsScratchDirectory}";
                hasResultsDirectory = true;
            }
            else if (string.Equals(argument, DiagnosticOutputDirectoryOption, StringComparison.Ordinal)
                && i + 1 < redirectedArguments.Count)
            {
                redirectedArguments[i + 1] = diagnosticScratchDirectory;
                hasDiagnosticOutputDirectory = true;
                i++;
            }
            else if (TryGetInlineOptionValue(argument, DiagnosticOutputDirectoryOption, out _))
            {
                redirectedArguments[i] = $"{DiagnosticOutputDirectoryOption}{argument[DiagnosticOutputDirectoryOption.Length]}{diagnosticScratchDirectory}";
                hasDiagnosticOutputDirectory = true;
            }
        }

        if (!hasResultsDirectory)
        {
            redirectedArguments.Add(ResultsDirectoryOption);
            redirectedArguments.Add(resultsScratchDirectory);
        }

        if (diagnosticEnabled && !hasDiagnosticOutputDirectory)
        {
            redirectedArguments.Add(DiagnosticOutputDirectoryOption);
            redirectedArguments.Add(diagnosticScratchDirectory);
        }

        return redirectedArguments;
    }

    private static void RemoveOptionWithValue(List<string> arguments, string option)
    {
        for (int i = arguments.Count - 1; i >= 0; i--)
        {
            string argument = arguments[i];
            if (string.Equals(argument, option, StringComparison.Ordinal))
            {
                arguments.RemoveAt(i);
                if (i < arguments.Count)
                {
                    arguments.RemoveAt(i);
                }
            }
            else if (argument.StartsWith(option + "=", StringComparison.Ordinal)
                || argument.StartsWith(option + ":", StringComparison.Ordinal))
            {
                arguments.RemoveAt(i);
            }
        }
    }

    private static string GetControllerPath(string path, TestHostLaunchContext context)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }

        string baseDirectory = context.WorkingDirectory is { Length: > 0 } workingDirectory
            ? workingDirectory
            : Path.GetDirectoryName(context.FileName)
                ?? throw new InvalidOperationException($"Unable to determine the working directory for '{context.FileName}'.");
        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }
#if !PACKAGEDAPP_WINRT
#pragma warning restore IDE0051
#endif
}
