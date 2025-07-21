// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class CommandLineHandler : ICommandLineHandler, ICommandLineOptions, IOutputDeviceDataProducer
{
    private static readonly TextOutputDeviceData EmptyText = new(string.Empty);

    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IRuntimeFeature _runtimeFeature;

    public CommandLineHandler(CommandLineParseResult parseResult, IReadOnlyCollection<ICommandLineOptionsProvider> extensionsCommandLineOptionsProviders,
        IReadOnlyCollection<ICommandLineOptionsProvider> systemCommandLineOptionsProviders, ITestApplicationModuleInfo testApplicationModuleInfo,
        IRuntimeFeature runtimeFeature)
    {
        ParseResult = parseResult;
        ExtensionsCommandLineOptionsProviders = extensionsCommandLineOptionsProviders;
        SystemCommandLineOptionsProviders = systemCommandLineOptionsProviders;
        CommandLineOptionsProviders = systemCommandLineOptionsProviders.Union(extensionsCommandLineOptionsProviders);
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _runtimeFeature = runtimeFeature;
    }

    public IEnumerable<ICommandLineOptionsProvider> CommandLineOptionsProviders { get; }

    public string Uid => nameof(CommandLineHandler);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    internal IReadOnlyCollection<ICommandLineOptionsProvider> ExtensionsCommandLineOptionsProviders { get; }

    internal IReadOnlyCollection<ICommandLineOptionsProvider> SystemCommandLineOptionsProviders { get; }

    internal CommandLineParseResult ParseResult { get; }

    public async Task PrintInfoAsync(IOutputDevice outputDevice, IReadOnlyList<ITool>? availableTools, CancellationToken cancellationToken)
    {
        // /!\ Info should not be localized as it serves debugging purposes.
        await DisplayPlatformInfoAsync().ConfigureAwait(false);
        await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);
        await DisplayBuiltInExtensionsInfoAsync(outputDevice, cancellationToken).ConfigureAwait(false);
        await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);

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

        await DisplayRegisteredExtensionsInfoAsync(outputDevice, nonToolExtensions, cancellationToken).ConfigureAwait(false);
        await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);
        await DisplayRegisteredToolsInfoAsync(outputDevice, availableTools, toolExtensions, cancellationToken).ConfigureAwait(false);
        await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);

        return;

        async Task DisplayPlatformInfoAsync()
        {
            // Product title, do not translate.
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData("Microsoft Testing Platform:"), cancellationToken).ConfigureAwait(false);

            // TODO: Replace Assembly with IAssembly
            AssemblyInformationalVersionAttribute? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string versionInfo = version?.InformationalVersion ?? "Not Available";
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Version: {versionInfo}"), cancellationToken).ConfigureAwait(false);

            await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Dynamic Code Supported: {_runtimeFeature.IsDynamicCodeSupported}"), cancellationToken).ConfigureAwait(false);

            // TODO: Replace RuntimeInformation with IRuntimeInformation
#if NETCOREAPP
            string runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
#else
            string runtimeInformation = $"{RuntimeInformation.FrameworkDescription}";
#endif
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Runtime information: {runtimeInformation}"), cancellationToken).ConfigureAwait(false);

#if !NETCOREAPP
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file, this branch run only in .NET Framework
            string runtimeLocation = typeof(object).Assembly?.Location ?? "Not Found";
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Runtime location: {runtimeLocation}"), cancellationToken).ConfigureAwait(false);
#endif

            string moduleName = _testApplicationModuleInfo.GetDisplayName();
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Test module: {moduleName}"), cancellationToken).ConfigureAwait(false);
        }

        async Task DisplayOptionsAsync(IOutputDevice outputDevice, IEnumerable<CommandLineOption> options, int indentLevel)
        {
            string optionNameIndent = new(' ', indentLevel * 2);
            string optionInfoIndent = new(' ', (indentLevel + 1) * 2);
            foreach (CommandLineOption option in options.OrderBy(x => x.Name))
            {
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionNameIndent}--{option.Name}"), cancellationToken).ConfigureAwait(false);
                if (option.Arity.Min == option.Arity.Max)
                {
                    await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Arity: {option.Arity.Min}"), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    string maxArityValue = option.Arity.Max == int.MaxValue ? "N" : $"{option.Arity.Max}";
                    await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Arity: {option.Arity.Min}..{maxArityValue}"), cancellationToken).ConfigureAwait(false);
                }

                await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Hidden: {option.IsHidden}"), cancellationToken).ConfigureAwait(false);
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"Description: {option.Description}") { Padding = optionInfoIndent.Length }, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task DisplayProvidersAsync(IOutputDevice outputDevice, IEnumerable<ICommandLineOptionsProvider> optionsProviders, int indentLevel)
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
                        await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerIdIndent}{provider.Uid}"), cancellationToken).ConfigureAwait(false);
                        await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Name: {provider.DisplayName}"), cancellationToken).ConfigureAwait(false);
                        await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Version: {provider.Version}"), cancellationToken).ConfigureAwait(false);
                        await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"Description: {provider.Description}") { Padding = providerInfoIndent.Length }, cancellationToken).ConfigureAwait(false);
                        await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Options:"), cancellationToken).ConfigureAwait(false);
                    }

                    await DisplayOptionsAsync(outputDevice, provider.GetCommandLineOptions(), indentLevel + 2).ConfigureAwait(false);
                }
            }
        }

        async Task DisplayBuiltInExtensionsInfoAsync(IOutputDevice outputDevice, CancellationToken cancellationToken)
        {
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData("Built-in command line providers:"), cancellationToken).ConfigureAwait(false);
            if (SystemCommandLineOptionsProviders.Count == 0)
            {
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no built-in command line providers."), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await DisplayProvidersAsync(outputDevice, SystemCommandLineOptionsProviders, 1).ConfigureAwait(false);
            }
        }

        async Task DisplayRegisteredExtensionsInfoAsync(IOutputDevice outputDevice, List<ICommandLineOptionsProvider> nonToolExtensions, CancellationToken cancellationToken)
        {
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData("Registered command line providers:"), cancellationToken).ConfigureAwait(false);
            if (nonToolExtensions.Count == 0)
            {
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no registered command line providers."), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await DisplayProvidersAsync(outputDevice, nonToolExtensions, 1).ConfigureAwait(false);
            }
        }

        async Task DisplayRegisteredToolsInfoAsync(IOutputDevice outputDevice, IReadOnlyList<ITool>? availableTools, List<IToolCommandLineOptionsProvider> toolExtensions, CancellationToken cancellationToken)
        {
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData("Registered tools:"), cancellationToken).ConfigureAwait(false);
            if (availableTools is null || availableTools.Count == 0)
            {
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no registered tools."), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var groupedToolExtensions = toolExtensions.GroupBy(x => x.ToolName).ToDictionary(x => x.Key, x => x.ToList());
                foreach (ITool tool in availableTools.OrderBy(x => x.Uid))
                {
                    await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"  {tool.Uid}"), cancellationToken).ConfigureAwait(false);
                    await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Command: {tool.Name}"), cancellationToken).ConfigureAwait(false);
                    await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Name: {tool.DisplayName}"), cancellationToken).ConfigureAwait(false);
                    await outputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Version: {tool.Version}"), cancellationToken).ConfigureAwait(false);
                    await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"Description: {tool.Description}") { Padding = 4 }, cancellationToken).ConfigureAwait(false);
                    await outputDevice.DisplayAsync(this, new TextOutputDeviceData("    Tool command line providers:"), cancellationToken).ConfigureAwait(false);
                    if (groupedToolExtensions.TryGetValue(tool.Name, out List<IToolCommandLineOptionsProvider>? providers))
                    {
                        await DisplayProvidersAsync(outputDevice, providers, 3).ConfigureAwait(false);
                    }
                    else
                    {
                        await outputDevice.DisplayAsync(this, new TextOutputDeviceData("      There are no registered command line providers."), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    public bool IsOptionSet(string optionName)
        => ParseResult.IsOptionSet(optionName);

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments) =>
        ParseResult.TryGetOptionArgumentList(optionName, out arguments);

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    public bool IsHelpInvoked() => IsOptionSet(PlatformCommandLineProvider.HelpOptionKey) || IsOptionSet(PlatformCommandLineProvider.HelpOptionQuestionMark);

    public bool IsInfoInvoked() => IsOptionSet(PlatformCommandLineProvider.InfoOptionKey);

    public bool IsDotNetTestPipeInvoked() => IsOptionSet(PlatformCommandLineProvider.DotNetTestPipeOptionKey);

#pragma warning disable IDE0060 // Remove unused parameter, temporary we don't use it.
    public async Task PrintHelpAsync(IOutputDevice outputDevice, IReadOnlyList<ITool>? availableTools, CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        string applicationName = GetApplicationName(_testApplicationModuleInfo);
        await PrintApplicationUsageAsync(applicationName).ConfigureAwait(false);

        // Temporary disabled, we don't remove the code because could be useful in future.
        // PrintApplicationToolUsage(availableTools, applicationName);
        await outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Empty), cancellationToken).ConfigureAwait(false);

        // Local functions
        static string GetApplicationName(ITestApplicationModuleInfo testApplicationModuleInfo)
            => testApplicationModuleInfo.IsAppHostOrSingleFileOrNativeAot
                ? Path.GetFileName(testApplicationModuleInfo.GetProcessPath())
                : testApplicationModuleInfo.IsCurrentTestApplicationHostDotnetMuxer
                    ? $"dotnet exec {Path.GetFileName(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}"
                    : PlatformResources.HelpTestApplicationRunner;

        async Task<bool> PrintOptionsAsync(IEnumerable<ICommandLineOptionsProvider> optionProviders, bool builtInOnly = false)
        {
            CommandLineOption[] options =
                [.. optionProviders
               .SelectMany(provider => provider.GetCommandLineOptions())
               .Where(option => !option.IsHidden && option.IsBuiltIn == builtInOnly)
               .OrderBy(option => option.Name)];

            if (options.Length == 0)
            {
                return false;
            }

            foreach (CommandLineOption? option in options)
            {
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"--{option.Name}") { Padding = 4 }, cancellationToken).ConfigureAwait(false);
                await outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(option.Description) { Padding = 8 }, cancellationToken).ConfigureAwait(false);
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Empty), cancellationToken).ConfigureAwait(false);
            }

            return options.Length != 0;
        }

        async Task PrintApplicationUsageAsync(string applicationName)
        {
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.HelpApplicationUsage, applicationName)), cancellationToken).ConfigureAwait(false);
            await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpExecuteTestApplication), cancellationToken).ConfigureAwait(false);
            await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);

            RoslynDebug.Assert(
                !SystemCommandLineOptionsProviders.OfType<IToolCommandLineOptionsProvider>().Any(),
                "System command line options should not have any tool option registered.");
            await outputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpOptions), cancellationToken).ConfigureAwait(false);
            ICommandLineOptionsProvider[] nonToolsExtensionProviders =
                [.. ExtensionsCommandLineOptionsProviders.Where(provider => provider is not IToolCommandLineOptionsProvider)];
            // By default, only system options are built-in but some extensions (e.g. retry) are considered as built-in too,
            // so we need to union the 2 collections before printing the options.
            await PrintOptionsAsync(SystemCommandLineOptionsProviders.Union(nonToolsExtensionProviders), builtInOnly: true).ConfigureAwait(false);
            await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);

            await outputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpExtensionOptions), cancellationToken).ConfigureAwait(false);
            if (!await PrintOptionsAsync(nonToolsExtensionProviders).ConfigureAwait(false))
            {
                await outputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpNoExtensionRegistered), cancellationToken).ConfigureAwait(false);
            }

            await outputDevice.DisplayAsync(this, EmptyText, cancellationToken).ConfigureAwait(false);
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
}
