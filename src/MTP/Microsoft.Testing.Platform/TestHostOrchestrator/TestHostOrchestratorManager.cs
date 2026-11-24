// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.TestHostOrchestrator;

internal sealed class TestHostOrchestratorManager :
    ITestHostOrchestratorManager,
    ITestHostExecutionOrchestratorMiddlewareManager
{
    private readonly List<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>> _testHostOrchestratorApplicationLifetimeFactories = [];
    private List<Func<IServiceProvider, ITestHostExecutionOrchestrator>>? _factories;
    private List<Func<IServiceProvider, ITestHostExecutionOrchestratorMiddleware>>? _middlewareFactories;

    public void AddTestHostOrchestrator(Func<IServiceProvider, ITestHostExecutionOrchestrator> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));
        _factories ??= [];
        _factories.Add(factory);
    }

    public void AddTestHostExecutionOrchestratorMiddleware(Func<IServiceProvider, ITestHostExecutionOrchestratorMiddleware> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));
        _middlewareFactories ??= [];
        _middlewareFactories.Add(factory);
    }

    internal Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider)
        => BuildAsync(serviceProvider, orchestratorChain: null);

    internal async Task<TestHostOrchestratorConfiguration> BuildAsync(ServiceProvider serviceProvider, string? orchestratorChain)
    {
        string[] consumedOrchestratorUids = DeserializeOrchestratorChain(orchestratorChain);
        List<ITestHostExecutionOrchestrator> orchestrators = [];
        if (_factories is not null)
        {
            foreach (Func<IServiceProvider, ITestHostExecutionOrchestrator> factory in _factories)
            {
                ITestHostExecutionOrchestrator orchestrator = factory(serviceProvider);

                orchestrators.ValidateUniqueExtension(orchestrator);
                if (consumedOrchestratorUids.Contains(orchestrator.Uid, StringComparer.Ordinal))
                {
                    continue;
                }

                if (await orchestrator.IsEnabledAsync().ConfigureAwait(false))
                {
                    // Legacy orchestrators keep their existing initialization order. A composable
                    // orchestrator initializes only in the process selected to run its stage.
                    if (orchestrator is not IComposableTestHostExecutionOrchestrator)
                    {
                        await orchestrator.TryInitializeAsync().ConfigureAwait(false);
                    }

                    orchestrators.Add(orchestrator);
                }
            }
        }

        bool isCompositionActive = consumedOrchestratorUids.Length > 0 || orchestrators.Count > 1;
        if (isCompositionActive && orchestrators.Count > 0)
        {
            IComposableTestHostExecutionOrchestrator[] composableOrchestrators = orchestrators
                .OfType<IComposableTestHostExecutionOrchestrator>()
                .ToArray();

            if (composableOrchestrators.Length != orchestrators.Count)
            {
                string uids = string.Join("', '", orchestrators.Select(orchestrator => orchestrator.Uid));
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    PlatformResources.TestHostExecutionOrchestratorsMustBeComposableErrorMessage,
                    uids));
            }

            ValidateStages(composableOrchestrators);

            IGrouping<TestHostExecutionOrchestratorStage, IComposableTestHostExecutionOrchestrator>? duplicateStage =
                composableOrchestrators.GroupBy(orchestrator => orchestrator.Stage).FirstOrDefault(group => group.Count() > 1);
            if (duplicateStage is not null)
            {
                string uids = string.Join("', '", duplicateStage.Select(orchestrator => orchestrator.Uid));
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    PlatformResources.TestHostExecutionOrchestratorsMustUseDistinctStagesErrorMessage,
                    duplicateStage.Key,
                    uids));
            }

            if (IsDotnetTestPipe(serviceProvider)
                && composableOrchestrators.Any(orchestrator => orchestrator.Stage == TestHostExecutionOrchestratorStage.Repetition)
                && composableOrchestrators.Any(orchestrator => orchestrator.Stage == TestHostExecutionOrchestratorStage.Recovery))
            {
                string uids = string.Join("', '", composableOrchestrators
                    .Where(orchestrator => orchestrator.Stage is TestHostExecutionOrchestratorStage.Repetition or TestHostExecutionOrchestratorStage.Recovery)
                    .Select(orchestrator => orchestrator.Uid));
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    PlatformResources.TestHostExecutionOrchestratorRepetitionAndRecoveryUnsupportedOnDotnetTestPipeErrorMessage,
                    uids));
            }

            orchestrators = [composableOrchestrators.OrderBy(orchestrator => orchestrator.Stage).First()];
        }

        if (orchestrators.Count == 1 && orchestrators[0] is IComposableTestHostExecutionOrchestrator selected)
        {
            await selected.TryInitializeAsync().ConfigureAwait(false);
        }

        List<ITestHostExecutionOrchestratorMiddleware> middleware = [];
        if (consumedOrchestratorUids.Length == 0 && _middlewareFactories is not null)
        {
            // Preserves registration order: BuildAndRegisterExtensionsAsync appends in the order the
            // factories were added, which is the documented outermost-first composition order.
            await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_middlewareFactories, serviceProvider, middleware).ConfigureAwait(false);
        }

        return new TestHostOrchestratorConfiguration(
            [.. orchestrators],
            [.. middleware],
            consumedOrchestratorUids,
            isCompositionActive);
    }

    private static void ValidateStages(IComposableTestHostExecutionOrchestrator[] orchestrators)
    {
        IComposableTestHostExecutionOrchestrator? invalidStageOrchestrator = orchestrators.FirstOrDefault(
            orchestrator => !Enum.IsDefined(typeof(TestHostExecutionOrchestratorStage), orchestrator.Stage));
        if (invalidStageOrchestrator is not null)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                PlatformResources.TestHostExecutionOrchestratorStageIsInvalidErrorMessage,
                invalidStageOrchestrator.Uid,
                (int)invalidStageOrchestrator.Stage));
        }
    }

    private static string[] DeserializeOrchestratorChain(string? chain)
    {
        if (RoslynString.IsNullOrEmpty(chain))
        {
            return [];
        }

        try
        {
            UTF8Encoding encoding = new(false, true);
            string[] consumedOrchestratorUids = chain.Split('.')
                .Select(encodedUid => encoding.GetString(Convert.FromBase64String(encodedUid)))
                .ToArray();
            return consumedOrchestratorUids.Any(RoslynString.IsNullOrEmpty)
                    || consumedOrchestratorUids.Distinct(StringComparer.Ordinal).Count() != consumedOrchestratorUids.Length
                ? throw new FormatException("The orchestrator chain contains an invalid Uid.")
                : consumedOrchestratorUids;
        }
        catch (Exception ex) when (ex is FormatException or System.Text.DecoderFallbackException)
        {
            throw new InvalidOperationException(
                PlatformResources.TestHostExecutionOrchestratorChainIsInvalidErrorMessage,
                ex);
        }
    }

    public void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory)
    {
        _ = testHostOrchestratorApplicationLifetimeFactory ?? throw new ArgumentNullException(nameof(testHostOrchestratorApplicationLifetimeFactory));
        _testHostOrchestratorApplicationLifetimeFactories.Add(testHostOrchestratorApplicationLifetimeFactory);
    }

    internal async Task<ITestHostOrchestratorApplicationLifetime[]> BuildTestHostOrchestratorApplicationLifetimesAsync(ServiceProvider serviceProvider)
    {
        List<ITestHostOrchestratorApplicationLifetime> lifetimes = [];
        await ExtensionBuilderHelper.BuildAndRegisterExtensionsAsync(_testHostOrchestratorApplicationLifetimeFactories, serviceProvider, lifetimes).ConfigureAwait(false);

        return [.. lifetimes];
    }

    private static bool IsDotnetTestPipe(IServiceProvider serviceProvider)
        => serviceProvider.GetService<CommandLineHandler>() is { } commandLineHandler
            && commandLineHandler.HasDotnetTestServerOption()
            && commandLineHandler.IsDotNetTestPipeInvoked();
}
