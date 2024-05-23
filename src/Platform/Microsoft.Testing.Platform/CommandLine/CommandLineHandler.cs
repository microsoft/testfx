// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineHandler(string[] args, CommandLineParseResult parseResult, ICommandLineOptionsProvider[] extensionsCommandLineOptionsProviders,
    ICommandLineOptionsProvider[] systemCommandLineOptionsProviders, ITestApplicationModuleInfo testApplicationModuleInfo, IRuntimeFeature runtimeFeature,
    IPlatformOutputDevice platformOutputDevice, IEnvironment environment, IProcessHandler process) : ICommandLineHandler, ICommandLineOptions, IOutputDeviceDataProducer
{
    private static readonly TextOutputDeviceData EmptyText = new(string.Empty);

#if !NETCOREAPP
    [SuppressMessage("CodeQuality", "IDE0052:RemoveVariable unread private members", Justification = "Used in netcoreapp")]
#endif

#if NETCOREAPP
    [SuppressMessage("CodeQuality", "IDE0052:RemoveVariable unread private members", Justification = "Used in netstandard")]
#endif
    private readonly IProcessHandler _process = process;

    public string[] Arguments { get; } = args;

    public ICommandLineOptionsProvider[] ExtensionsCommandLineOptionsProviders { get; } = extensionsCommandLineOptionsProviders;

    public string Uid => nameof(CommandLineHandler);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public async Task<(bool IsValid, string? ValidationError)> TryParseAndValidateAsync()
    {
        if (parseResult.HasError)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(PlatformResources.InvalidCommandLineArguments);
            foreach (string error in parseResult.Errors)
            {
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"\t- {error}");
            }

            return (false, stringBuilder.ToString());
        }

        if (ExtensionOptionsContainReservedPrefix(out string? reservedPrefixError))
        {
            return (false, reservedPrefixError);
        }

        if (ExtensionOptionsContainReservedOptions(out string? reservedOptionError))
        {
            return (false, reservedOptionError);
        }

        if (ExtensionOptionAreDuplicated(out string? duplicationError))
        {
            return (false, duplicationError);
        }

        if (UnknownOptions(out string? unknownOptionsError))
        {
            return (false, unknownOptionsError);
        }

        if (ExtensionArgumentArityAreInvalid(out string? arityErrors))
        {
            return (false, arityErrors);
        }

        ValidationResult optionsResult = await ValidateOptionsArgumentsAsync();
        if (!optionsResult.IsValid)
        {
            return (false, optionsResult.ErrorMessage);
        }

        ValidationResult configurationResult = await ValidateConfigurationAsync();
#pragma warning disable IDE0046 // Convert to conditional expression - make the code less readable
        if (!configurationResult.IsValid)
        {
            return (false, configurationResult.ErrorMessage);
        }
#pragma warning restore IDE0046 // Convert to conditional expression

        return (true, null);
    }

    private bool ExtensionOptionsContainReservedPrefix([NotNullWhen(true)] out string? error)
    {
        StringBuilder? stringBuilder = null;
        foreach (ICommandLineOptionsProvider commandLineOptionsProvider in ExtensionsCommandLineOptionsProviders)
        {
            foreach (CommandLineOption option in commandLineOptionsProvider.GetCommandLineOptions())
            {
                if (option.IsBuiltIn)
                {
                    continue;
                }

                string trimmedOption = option.Name.Trim(CommandLineParseResult.OptionPrefix);
                if (trimmedOption.StartsWith("internal", StringComparison.OrdinalIgnoreCase)
                    || option.Name.StartsWith("-internal", StringComparison.OrdinalIgnoreCase))
                {
                    stringBuilder ??= new();
                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsUsingReservedPrefix, trimmedOption, commandLineOptionsProvider.DisplayName, commandLineOptionsProvider.Uid));
                }
            }
        }

        error = stringBuilder?.ToString();
        return stringBuilder?.Length > 0;
    }

    private async Task<ValidationResult> ValidateConfigurationAsync()
    {
        StringBuilder? stringBuilder = null;
        foreach (ICommandLineOptionsProvider commandLineOptionsProvider in systemCommandLineOptionsProviders.Union(ExtensionsCommandLineOptionsProviders))
        {
            ValidationResult result = await commandLineOptionsProvider.ValidateCommandLineOptionsAsync(this);
            if (!result.IsValid)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidConfiguration, commandLineOptionsProvider.DisplayName, commandLineOptionsProvider.Uid, result.ErrorMessage));
                stringBuilder.AppendLine();
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToString())
            : ValidationResult.Valid();
    }

    private async Task<ValidationResult> ValidateOptionsArgumentsAsync()
    {
        ApplicationStateGuard.Ensure(parseResult is not null);

        StringBuilder? stringBuilder = null;
        foreach (OptionRecord optionRecord in parseResult.Options)
        {
            ICommandLineOptionsProvider extension = GetAllCommandLineOptionsProviderByOptionName(optionRecord.Option).Single();
            ValidationResult result = await extension.ValidateOptionArgumentsAsync(extension.GetCommandLineOptions().Single(x => x.Name == optionRecord.Option), optionRecord.Arguments);
            if (!result.IsValid)
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineInvalidArgumentsForOption, optionRecord.Option, result.ErrorMessage));
            }
        }

        return stringBuilder?.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToString())
            : ValidationResult.Valid();
    }

    private bool UnknownOptions([NotNullWhen(true)] out string? error)
    {
        error = null;

        ApplicationStateGuard.Ensure(parseResult is not null);

        StringBuilder? stringBuilder = null;
        foreach (OptionRecord optionRecord in parseResult.Options)
        {
            if (!GetAllCommandLineOptionsProviderByOptionName(optionRecord.Option).Any())
            {
                stringBuilder ??= new();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineUnknownOption, optionRecord.Option));
            }
        }

        if (stringBuilder?.Length > 0)
        {
            error = stringBuilder.ToString();
            return true;
        }

        return false;
    }

    private bool ExtensionArgumentArityAreInvalid([NotNullWhen(true)] out string? error)
    {
        error = null;

        ApplicationStateGuard.Ensure(parseResult is not null);

        StringBuilder stringBuilder = new();
        foreach (IGrouping<string, OptionRecord> groupedOptions in parseResult.Options.GroupBy(x => x.Option))
        {
            // getting the arguments count for an option.
            int arity = 0;
            foreach (OptionRecord optionEntry in groupedOptions)
            {
                arity += optionEntry.Arguments.Length;
            }

            string optionName = groupedOptions.Key;
            ICommandLineOptionsProvider extension = GetAllCommandLineOptionsProviderByOptionName(optionName).Single();
            CommandLineOption commandLineOption = extension.GetCommandLineOptions().Single(x => x.Name == optionName);

            if (arity > commandLineOption.Arity.Max && commandLineOption.Arity.Max == 0)
            {
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsNoArguments, optionName, extension.DisplayName, extension.Uid));
            }
            else if (arity < commandLineOption.Arity.Min)
            {
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtLeastArguments, optionName, extension.DisplayName, extension.Uid, commandLineOption.Arity.Min));
            }
            else if (arity > commandLineOption.Arity.Max)
            {
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionExpectsAtMostArguments, optionName, extension.DisplayName, extension.Uid, commandLineOption.Arity.Max));
            }
        }

        if (stringBuilder.Length > 0)
        {
            error = stringBuilder.ToString();
            return true;
        }

        return false;
    }

    private bool ExtensionOptionAreDuplicated([NotNullWhen(true)] out string? error)
    {
        error = null;
        IEnumerable<string> duplications = ExtensionsCommandLineOptionsProviders.SelectMany(x => x.GetCommandLineOptions())
            .Select(x => x.Name)
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key);

        StringBuilder? stringBuilder = null;
        foreach (string duplicatedOption in duplications)
        {
            IEnumerable<ICommandLineOptionsProvider> commandLineOptionProviders = GetExtensionCommandLineOptionsProviderByOptionName(duplicatedOption);
            stringBuilder ??= new();
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsDeclaredByMultipleProviders, duplicatedOption, string.Join("', '", commandLineOptionProviders.Select(x => x.DisplayName))));
        }

        if (stringBuilder?.Length > 0)
        {
            stringBuilder.AppendLine(PlatformResources.CommandLineOptionIsDeclaredByMultipleProvidersWorkaround);
            error = stringBuilder.ToString();
            return true;
        }

        return false;
    }

    private bool ExtensionOptionsContainReservedOptions([NotNullWhen(true)] out string? error)
    {
        error = null;

        IEnumerable<string> allExtensionOptions = ExtensionsCommandLineOptionsProviders.SelectMany(x => x.GetCommandLineOptions()).Select(x => x.Name).Distinct();
        IEnumerable<string> allSystemOptions = systemCommandLineOptionsProviders.SelectMany(x => x.GetCommandLineOptions()).Select(x => x.Name).Distinct();

        IEnumerable<string> invalidReservedOptions = allSystemOptions.Intersect(allExtensionOptions);
        StringBuilder? stringBuilder = null;
        if (invalidReservedOptions.Any())
        {
            stringBuilder = new();
            foreach (string reservedOption in invalidReservedOptions)
            {
                IEnumerable<ICommandLineOptionsProvider> commandLineOptionProviders = GetExtensionCommandLineOptionsProviderByOptionName(reservedOption);
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.CommandLineOptionIsReserved, reservedOption, string.Join("', '", commandLineOptionProviders.Select(x => x.DisplayName))));
            }

            error = stringBuilder.ToString();
            return true;
        }

        return false;
    }

    private IEnumerable<ICommandLineOptionsProvider> GetExtensionCommandLineOptionsProviderByOptionName(string optionName)
    {
        foreach (ICommandLineOptionsProvider commandLineOptionsProvider in ExtensionsCommandLineOptionsProviders)
        {
            if (commandLineOptionsProvider.GetCommandLineOptions().Any(option => option.Name == optionName))
            {
                yield return commandLineOptionsProvider;
            }
        }
    }

    private IEnumerable<ICommandLineOptionsProvider> GetAllCommandLineOptionsProviderByOptionName(string optionName)
    {
        foreach (ICommandLineOptionsProvider commandLineOptionsProvider in systemCommandLineOptionsProviders.Union(ExtensionsCommandLineOptionsProviders))
        {
            if (commandLineOptionsProvider.GetCommandLineOptions().Any(option => option.Name == optionName))
            {
                yield return commandLineOptionsProvider;
            }
        }
    }

    public bool IsHelpInvoked() => IsOptionSet(PlatformCommandLineProvider.HelpOptionKey);

    public bool IsInfoInvoked() => IsOptionSet(PlatformCommandLineProvider.InfoOptionKey);

#pragma warning disable IDE0060 // Remove unused parameter, temporary we don't use it.
    public async Task PrintHelpAsync(ITool[]? availableTools = null)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        string applicationName = GetApplicationName(testApplicationModuleInfo);
        await PrintApplicationUsageAsync(applicationName);

        // Temporary disabled, we don't remove the code because could be useful in future.
        // PrintApplicationToolUsage(availableTools, applicationName);
        await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Empty));

        // Local functions
        static string GetApplicationName(ITestApplicationModuleInfo testApplicationModuleInfo)
            => testApplicationModuleInfo.IsAppHostOrSingleFileOrNativeAot
                ? Path.GetFileName(testApplicationModuleInfo.GetProcessPath())
                : testApplicationModuleInfo.IsCurrentTestApplicationHostDotnetMuxer
                    ? $"dotnet exec {Path.GetFileName(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}"
                    : PlatformResources.HelpTestApplicationRunner;

        async Task<bool> PrintOptionsAsync(IEnumerable<ICommandLineOptionsProvider> optionProviders, int leftPaddingDepth, bool builtInOnly = false)
        {
            IEnumerable<CommandLineOption> options =
                optionProviders
               .SelectMany(provider => provider.GetCommandLineOptions())
               .Where(option => !option.IsHidden)
               .OrderBy(option => option.Name);

            options = builtInOnly ? options.Where(option => option.IsBuiltIn) : options.Where(option => !option.IsBuiltIn);

            if (!options.Any())
            {
                return false;
            }

            int maxOptionNameLength = options.Max(option => option.Name.Length);

            foreach (CommandLineOption? option in options)
            {
                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{new string(' ', leftPaddingDepth * 2)}--{option.Name}{new string(' ', maxOptionNameLength - option.Name.Length)} {option.Description}"));
            }

            return options.Any();
        }

        async Task PrintApplicationUsageAsync(string applicationName)
        {
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.HelpApplicationUsage, applicationName)));
            await platformOutputDevice.DisplayAsync(this, EmptyText);
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpExecuteTestApplication));
            await platformOutputDevice.DisplayAsync(this, EmptyText);

            RoslynDebug.Assert(
                !systemCommandLineOptionsProviders.OfType<IToolCommandLineOptionsProvider>().Any(),
                "System command line options should not have any tool option registered.");
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpOptions));
            await PrintOptionsAsync(systemCommandLineOptionsProviders.Union(ExtensionsCommandLineOptionsProviders), 1, builtInOnly: true);
            await platformOutputDevice.DisplayAsync(this, EmptyText);

            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpExtensionOptions));
            if (!await PrintOptionsAsync(ExtensionsCommandLineOptionsProviders.Where(provider => provider is not IToolCommandLineOptionsProvider), 1))
            {
                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpNoExtensionRegistered));
            }

            await platformOutputDevice.DisplayAsync(this, EmptyText);
        }

        // Temporary disabled, we don't remove the code because could be useful in future.
        // void PrintApplicationToolUsage(ITool[]? availableTools, string applicationName)
        // {
        //     _console.WriteLine($"Usage {applicationName} [tool-name] [tool-optionProviders]");
        //     _console.WriteLine();
        //     _console.WriteLine("Execute a .NET Test Application tool.");
        //     _console.WriteLine();
        //     _console.WriteLine("Tools:");
        //     var tools = availableTools
        //         ?.Where(tool => !tool.Hidden)
        //         .OrderBy(tool => tool.DisplayName)
        //         .ToList();
        //     if (tools is null || tools.Count == 0)
        //     {
        //         _console.WriteLine("No tools registered.");
        //         return;
        //     }
        //     int maxToolNameLength = tools.Max(tool => tool.Name.Length);
        //     foreach (ITool tool in tools)
        //     {
        //         _console.WriteLine($"  {tool.Name}{new string(' ', maxToolNameLength - tool.Name.Length)} ({tool.DisplayName}): {tool.Description}");
        //         PrintOptions(ExtensionsCommandLineOptionsProviders.Where(provider => provider is IToolCommandLineOptionsProvider), 2);
        //     }
        // }
    }

    public async Task PrintInfoAsync(ITool[]? availableTools = null)
    {
        // /!\ Info should not be localized as it serves debugging purposes.
        await DisplayPlatformInfoAsync();
        await platformOutputDevice.DisplayAsync(this, EmptyText);
        await DisplayBuiltInExtensionsInfoAsync();
        await platformOutputDevice.DisplayAsync(this, EmptyText);

        List<IToolCommandLineOptionsProvider> toolExtensions = [];
        List<ICommandLineOptionsProvider> nonToolExtensions = [];
        foreach (ICommandLineOptionsProvider provider in ExtensionsCommandLineOptionsProviders)
        {
            if (provider is IToolCommandLineOptionsProvider toolProvider)
            {
                toolExtensions.Add(toolProvider);
            }
            else
            {
                nonToolExtensions.Add(provider);
            }
        }

        await DisplayRegisteredExtensionsInfoAsync(nonToolExtensions);
        await platformOutputDevice.DisplayAsync(this, EmptyText);
        await DisplayRegisteredToolsInfoAsync(availableTools, toolExtensions);
        await platformOutputDevice.DisplayAsync(this, EmptyText);

        return;

        async Task DisplayPlatformInfoAsync()
        {
            // Product title, do not translate.
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Microsoft Testing Platform:"));

            // TODO: Replace Assembly with IAssembly
            AssemblyInformationalVersionAttribute? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string versionInfo = version?.InformationalVersion ?? "Not Available";
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Version: {versionInfo}"));

            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Dynamic Code Supported: {runtimeFeature.IsDynamicCodeSupported}"));

            // TODO: Replace RuntimeInformation with IRuntimeInformation
#if NETCOREAPP
            string runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
#else
            string runtimeInformation = $"{RuntimeInformation.FrameworkDescription}";
#endif
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Runtime information: {runtimeInformation}"));

#if !NETCOREAPP
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file, this branch run only in .NET Framework
            string runtimeLocation = typeof(object).Assembly?.Location ?? "Not Found";
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Runtime location: {runtimeLocation}"));
#endif

            string? moduleName = testApplicationModuleInfo.GetCurrentTestApplicationFullPath();
            moduleName = RoslynString.IsNullOrEmpty(moduleName)
#if NETCOREAPP
                ? environment.ProcessPath
#else
                ? _process.GetCurrentProcess().MainModule.FileName
#endif
                : moduleName;
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Test module: {moduleName}"));
        }

        async Task DisplayOptionsAsync(IEnumerable<CommandLineOption> options, int indentLevel)
        {
            string optionNameIndent = new(' ', indentLevel * 2);
            string optionInfoIndent = new(' ', (indentLevel + 1) * 2);
            foreach (CommandLineOption option in options.OrderBy(x => x.Name))
            {
                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionNameIndent}--{option.Name}"));
                if (option.Arity.Min == option.Arity.Max)
                {
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Arity: {option.Arity.Min}"));
                }
                else
                {
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Arity: {option.Arity.Min}..{option.Arity.Max}"));
                }

                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Hidden: {option.IsHidden}"));
                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Description: {option.Description}"));
            }
        }

        async Task DisplayProvidersAsync(IEnumerable<ICommandLineOptionsProvider> optionsProviders, int indentLevel)
        {
            string providerIdIndent = new(' ', indentLevel * 2);
            string providerInfoIndent = new(' ', (indentLevel + 1) * 2);
            foreach (IGrouping<string, ICommandLineOptionsProvider>? group in optionsProviders.GroupBy(x => x.Uid).OrderBy(x => x.Key))
            {
                bool isFirst = true;
                foreach (ICommandLineOptionsProvider provider in group)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                        await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerIdIndent}{provider.Uid}"));
                        await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Name: {provider.DisplayName}"));
                        await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Version: {provider.Version}"));
                        await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Description: {provider.Description}"));
                        await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Options:"));
                    }

                    await DisplayOptionsAsync(provider.GetCommandLineOptions(), indentLevel + 2);
                }
            }
        }

        async Task DisplayBuiltInExtensionsInfoAsync()
        {
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Built-in command line providers:"));
            if (systemCommandLineOptionsProviders.Length == 0)
            {
                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no built-in command line providers."));
            }
            else
            {
                await DisplayProvidersAsync(systemCommandLineOptionsProviders, 1);
            }
        }

        async Task DisplayRegisteredExtensionsInfoAsync(List<ICommandLineOptionsProvider> nonToolExtensions)
        {
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Registered command line providers:"));
            if (nonToolExtensions.Count == 0)
            {
                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no registered command line providers."));
            }
            else
            {
                await DisplayProvidersAsync(nonToolExtensions, 1);
            }
        }

        async Task DisplayRegisteredToolsInfoAsync(ITool[]? availableTools, List<IToolCommandLineOptionsProvider> toolExtensions)
        {
            await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Registered tools:"));
            if (availableTools is null || availableTools.Length == 0)
            {
                await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no registered tools."));
            }
            else
            {
                var groupedToolExtensions = toolExtensions.GroupBy(x => x.ToolName).ToDictionary(x => x.Key, x => x.ToList());
                foreach (ITool tool in availableTools.OrderBy(x => x.Uid))
                {
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  {tool.Uid}"));
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Command: {tool.Name}"));
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Name: {tool.DisplayName}"));
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Version: {tool.Version}"));
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Description: {tool.Description}"));
                    await platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("    Tool command line providers:"));
                    await DisplayProvidersAsync(groupedToolExtensions[tool.Name], 3);
                }
            }
        }
    }

    public bool IsOptionSet(string optionName)
        => parseResult?.IsOptionSet(optionName) == true;

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
    {
        arguments = null;
        return parseResult is not null && parseResult.TryGetOptionArgumentList(optionName, out arguments);
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);
}
