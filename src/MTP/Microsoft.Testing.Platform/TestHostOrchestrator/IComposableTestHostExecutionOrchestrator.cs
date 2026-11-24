// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;

/// <summary>
/// Defines the order in which composable test host orchestrators are activated across child processes.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum TestHostExecutionOrchestratorStage
{
    /// <summary>
    /// Selects the tests that will participate in the invocation.
    /// </summary>
    Selection = 100,

    /// <summary>
    /// Splits selected tests across child invocations.
    /// </summary>
    Distribution = 200,

    /// <summary>
    /// Repeats the selected work across child invocations.
    /// </summary>
    Repetition = 300,

    /// <summary>
    /// Recovers from failed tests by rerunning them.
    /// </summary>
    Recovery = 400,
}

/// <summary>
/// Marks an orchestrator as able to participate in stage-ordered process composition.
/// </summary>
/// <remarks>
/// <para>
/// When more than one orchestrator is enabled, all must implement this interface and declare distinct
/// stages. The lowest stage runs outermost. Uids and stages must be stable across child processes;
/// enablement is evaluated in each process, but only the selected composable orchestrator is initialized.
/// </para>
/// <para>
/// The consumed-orchestrator chain is carried in a process-wide environment variable. It is inherited by
/// every child process launched while an orchestrator is running, not only the child that continues the
/// composition. This includes helper processes and custom launcher targets. MTP consumes and clears the
/// variable during startup; remove it from launches that must not continue the composition.
/// </para>
/// <para>
/// An outer stage must launch the same test application locally with the inherited environment and
/// forward arguments it does not own, including inner-stage, filter and server arguments. It may
/// narrow the work but must not change which other stages are enabled. Remove its own activation
/// arguments, propagate cancellation to children, and return a verdict that accounts for their exits.
/// Middleware and orchestrator application lifetimes wrap the outermost stage only.
/// </para>
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IComposableTestHostExecutionOrchestrator : ITestHostExecutionOrchestrator
{
    /// <summary>
    /// Gets the stage at which this orchestrator participates.
    /// </summary>
    TestHostExecutionOrchestratorStage Stage { get; }
}
