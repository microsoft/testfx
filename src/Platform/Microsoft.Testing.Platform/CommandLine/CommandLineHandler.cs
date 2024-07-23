// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

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
    private readonly IPlatformOutputDevice _platformOutputDevice;
    private readonly IRuntimeFeature _runtimeFeature;
#if !NETCOREAPP
    [SuppressMessage("CodeQuality", "IDE0052:RemoveVariable unread private members", Justification = "Used in netcoreapp")]
#endif
    private readonly IEnvironment _environment;

#if NETCOREAPP
    [SuppressMessage("CodeQuality", "IDE0052:RemoveVariable unread private members", Justification = "Used in netstandard")]
#endif
    private readonly IProcessHandler _process;

    public CommandLineHandler(CommandLineParseResult parseResult, IReadOnlyCollection<ICommandLineOptionsProvider> extensionsCommandLineOptionsProviders,
        IReadOnlyCollection<ICommandLineOptionsProvider> systemCommandLineOptionsProviders, ITestApplicationModuleInfo testApplicationModuleInfo,
        IRuntimeFeature runtimeFeature, IPlatformOutputDevice platformOutputDevice, IEnvironment environment, IProcessHandler process)
    {
        ParseResult = parseResult;
        ExtensionsCommandLineOptionsProviders = extensionsCommandLineOptionsProviders;
        SystemCommandLineOptionsProviders = systemCommandLineOptionsProviders;
        CommandLineOptionsProviders = systemCommandLineOptionsProviders.Union(extensionsCommandLineOptionsProviders);
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _runtimeFeature = runtimeFeature;
        _platformOutputDevice = platformOutputDevice;
        _environment = environment;
        _process = process;
    }

    public IEnumerable<ICommandLineOptionsProvider> CommandLineOptionsProviders { get; }

    public string Uid => nameof(CommandLineHandler);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    internal IReadOnlyCollection<ICommandLineOptionsProvider> ExtensionsCommandLineOptionsProviders { get; }

    internal IReadOnlyCollection<ICommandLineOptionsProvider> SystemCommandLineOptionsProviders { get; }

    internal CommandLineParseResult ParseResult { get; }

    public async Task PrintInfoAsync(IReadOnlyList<ITool>? availableTools = null)
    {
        // /!\ Info should not be localized as it serves debugging purposes.
        await DisplayPlatformInfoAsync();
        await _platformOutputDevice.DisplayAsync(this, EmptyText);
        await DisplayBuiltInExtensionsInfoAsync();
        await _platformOutputDevice.DisplayAsync(this, EmptyText);

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
        await _platformOutputDevice.DisplayAsync(this, EmptyText);
        await DisplayRegisteredToolsInfoAsync(availableTools, toolExtensions);
        await _platformOutputDevice.DisplayAsync(this, EmptyText);

        return;

        async Task DisplayPlatformInfoAsync()
        {
            // Product title, do not translate.
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Microsoft Testing Platform:"));

            // TODO: Replace Assembly with IAssembly
            AssemblyInformationalVersionAttribute? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string versionInfo = version?.InformationalVersion ?? "Not Available";
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Version: {versionInfo}"));

            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Dynamic Code Supported: {_runtimeFeature.IsDynamicCodeSupported}"));

            // TODO: Replace RuntimeInformation with IRuntimeInformation
#if NETCOREAPP
            string runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
#else
            string runtimeInformation = $"{RuntimeInformation.FrameworkDescription}";
#endif
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Runtime information: {runtimeInformation}"));

#if !NETCOREAPP
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file, this branch run only in .NET Framework
            string runtimeLocation = typeof(object).Assembly?.Location ?? "Not Found";
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Runtime location: {runtimeLocation}"));
#endif

            string? moduleName = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();
            moduleName = RoslynString.IsNullOrEmpty(moduleName)
#if NETCOREAPP
                ? _environment.ProcessPath
#else
                ? _process.GetCurrentProcess().MainModule.FileName
#endif
                : moduleName;
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  Test module: {moduleName}"));
        }

        async Task DisplayOptionsAsync(IEnumerable<CommandLineOption> options, int indentLevel)
        {
            string optionNameIndent = new(' ', indentLevel * 2);
            string optionInfoIndent = new(' ', (indentLevel + 1) * 2);
            foreach (CommandLineOption option in options.OrderBy(x => x.Name))
            {
                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionNameIndent}--{option.Name}"));
                if (option.Arity.Min == option.Arity.Max)
                {
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Arity: {option.Arity.Min}"));
                }
                else
                {
                    string maxArityValue = option.Arity.Max == int.MaxValue ? "N" : $"{option.Arity.Max}";
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Arity: {option.Arity.Min}..{maxArityValue}"));
                }

                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Hidden: {option.IsHidden}"));
                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{optionInfoIndent}Description: {option.Description}"));
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
                        await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerIdIndent}{provider.Uid}"));
                        await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Name: {provider.DisplayName}"));
                        await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Version: {provider.Version}"));
                        await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Description: {provider.Description}"));
                        await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{providerInfoIndent}Options:"));
                    }

                    await DisplayOptionsAsync(provider.GetCommandLineOptions(), indentLevel + 2);
                }
            }
        }

        async Task DisplayBuiltInExtensionsInfoAsync()
        {
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Built-in command line providers:"));
            if (SystemCommandLineOptionsProviders.Count == 0)
            {
                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no built-in command line providers."));
            }
            else
            {
                await DisplayProvidersAsync(SystemCommandLineOptionsProviders, 1);
            }
        }

        async Task DisplayRegisteredExtensionsInfoAsync(List<ICommandLineOptionsProvider> nonToolExtensions)
        {
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Registered command line providers:"));
            if (nonToolExtensions.Count == 0)
            {
                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no registered command line providers."));
            }
            else
            {
                await DisplayProvidersAsync(nonToolExtensions, 1);
            }
        }

        async Task DisplayRegisteredToolsInfoAsync(IReadOnlyList<ITool>? availableTools, List<IToolCommandLineOptionsProvider> toolExtensions)
        {
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("Registered tools:"));
            if (availableTools is null || availableTools.Count == 0)
            {
                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("  There are no registered tools."));
            }
            else
            {
                var groupedToolExtensions = toolExtensions.GroupBy(x => x.ToolName).ToDictionary(x => x.Key, x => x.ToList());
                foreach (ITool tool in availableTools.OrderBy(x => x.Uid))
                {
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"  {tool.Uid}"));
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Command: {tool.Name}"));
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Name: {tool.DisplayName}"));
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Version: {tool.Version}"));
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"    Description: {tool.Description}"));
                    await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("    Tool command line providers:"));
                    if (groupedToolExtensions.TryGetValue(tool.Name, out List<IToolCommandLineOptionsProvider>? providers))
                    {
                        await DisplayProvidersAsync(providers, 3);
                    }
                    else
                    {
                        await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData("      There are no registered command line providers."));
                    }
                }
            }
        }
    }

    public bool IsOptionSet(string optionName)
        => ParseResult.IsOptionSet(optionName);

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
    {
        arguments = null;
        return ParseResult is not null && ParseResult.TryGetOptionArgumentList(optionName, out arguments);
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    public bool IsHelpInvoked() => IsOptionSet(PlatformCommandLineProvider.HelpOptionKey);

    public bool IsInfoInvoked() => IsOptionSet(PlatformCommandLineProvider.InfoOptionKey);

    public bool IsDotNetTestPipeInvoked() => IsOptionSet(PlatformCommandLineProvider.DotNetTestPipeOptionKey);

#pragma warning disable IDE0060 // Remove unused parameter, temporary we don't use it.
    public async Task PrintHelpAsync(IReadOnlyList<ITool>? availableTools = null)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        string applicationName = GetApplicationName(_testApplicationModuleInfo);
        await PrintApplicationUsageAsync(applicationName);

        // Temporary disabled, we don't remove the code because could be useful in future.
        // PrintApplicationToolUsage(availableTools, applicationName);
        await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Empty));

        // Local functions
        static string GetApplicationName(ITestApplicationModuleInfo testApplicationModuleInfo)
            => testApplicationModuleInfo.IsAppHostOrSingleFileOrNativeAot
                ? Path.GetFileName(testApplicationModuleInfo.GetProcessPath())
                : testApplicationModuleInfo.IsCurrentTestApplicationHostDotnetMuxer
                    ? $"dotnet exec {Path.GetFileName(testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}"
                    : PlatformResources.HelpTestApplicationRunner;

        async Task<bool> PrintOptionsAsync(IEnumerable<ICommandLineOptionsProvider> optionProviders, int leftPaddingDepth,
            bool builtInOnly = false)
        {
            CommandLineOption[] options =
                optionProviders
               .SelectMany(provider => provider.GetCommandLineOptions())
               .Where(option => !option.IsHidden && option.IsBuiltIn == builtInOnly)
               .OrderBy(option => option.Name)
               .ToArray();

            if (options.Length == 0)
            {
                return false;
            }

            int maxOptionNameLength = options.Max(option => option.Name.Length);

            foreach (CommandLineOption? option in options)
            {
                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData($"{new string(' ', leftPaddingDepth * 2)}--{option.Name}{new string(' ', maxOptionNameLength - option.Name.Length)} {option.Description}"));
            }

            return options.Length != 0;
        }

        async Task PrintApplicationUsageAsync(string applicationName)
        {
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.HelpApplicationUsage, applicationName)));
            await _platformOutputDevice.DisplayAsync(this, EmptyText);
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpExecuteTestApplication));
            await _platformOutputDevice.DisplayAsync(this, EmptyText);

            RoslynDebug.Assert(
                !SystemCommandLineOptionsProviders.OfType<IToolCommandLineOptionsProvider>().Any(),
                "System command line options should not have any tool option registered.");
            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpOptions));
            ICommandLineOptionsProvider[] nonToolsExtensionProviders =
                ExtensionsCommandLineOptionsProviders
                .Where(provider => provider is not IToolCommandLineOptionsProvider)
                .ToArray();
            // By default, only system options are built-in but some extensions (e.g. retry) are considered as built-in too,
            // so we need to union the 2 collections before printing the options.
            await PrintOptionsAsync(SystemCommandLineOptionsProviders.Union(nonToolsExtensionProviders), 1, builtInOnly: true);
            await _platformOutputDevice.DisplayAsync(this, EmptyText);

            await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpExtensionOptions));
            if (!await PrintOptionsAsync(nonToolsExtensionProviders, 1))
            {
                await _platformOutputDevice.DisplayAsync(this, new TextOutputDeviceData(PlatformResources.HelpNoExtensionRegistered));
            }

            await _platformOutputDevice.DisplayAsync(this, EmptyText);
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
