// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

/// <summary>
/// Indicates whether a test framework supports enriching test nodes for TRX report generation.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="IsSupported"/> is <see langword="true"/>, the TRX reporter calls <see cref="Enable"/>
/// to request TRX-specific properties on the framework's final test-node updates.
/// Only updates with a <see cref="PassedTestNodeStateProperty"/>, <see cref="FailedTestNodeStateProperty"/>,
/// <see cref="ErrorTestNodeStateProperty"/>, <see cref="TimeoutTestNodeStateProperty"/>, or
/// <see cref="SkippedTestNodeStateProperty"/> are included in the report.
/// Properties supplied only on discovery or in-progress updates are not carried forward to the final update.
/// The final state is the only required property; every other property is optional.
/// </para>
/// <para>
/// To create a test definition, the reporter uses <see cref="TrxFullyQualifiedTypeNameProperty"/> and
/// <see cref="TestMethodIdentifierProperty"/> from the final node. Providing at least one of them is recommended
/// so that the test is grouped under its real class.
/// <see cref="TrxFullyQualifiedTypeNameProperty"/> supplies the TRX class name when present; otherwise,
/// the namespace and type name from <see cref="TestMethodIdentifierProperty"/> are used.
/// When neither property is present, the class name is <c>DefaultClassName</c>, the same placeholder the
/// VSTest TRX logger uses, because the TRX schema requires a class name.
/// The method identifier supplies the method name when available; otherwise, <see cref="TestNode.DisplayName"/> is used.
/// Source-file information is not required, and the reporter does not look up whether the class or method exists.
/// </para>
/// <para>The following properties are optional:</para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="TimingProperty"/> supplies the test's start time, end time, and duration through
/// <see cref="TimingProperty.GlobalTiming"/>. Without it, the duration is zero and the start and end
/// timestamps use the current UTC time while generating the report, not the actual execution times.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="TrxTestDefinitionName"/> supplies the test definition's name, falling back to
/// <see cref="TestNode.DisplayName"/> when absent. For results sharing a <see cref="TestNode.Uid"/>,
/// if any result supplies an explicit definition name, all explicit or fallback definition names must match.
/// Conflicting names cause report generation to fail.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="TrxExceptionProperty"/> supplies the result's <c>ErrorInfo</c> message and stack trace.
/// It is optional even for failed, error, and timeout results; without it, their failed outcome is
/// preserved but no <c>ErrorInfo</c> is written.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="TrxMessagesProperty"/> contains <see cref="StandardOutputTrxMessage"/>,
/// <see cref="StandardErrorTrxMessage"/>, and <see cref="DebugOrTraceTrxMessage"/> entries, which populate
/// the result's <c>StdOut</c>, <c>StdErr</c>, and <c>DebugTrace</c> elements, respectively.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="TrxCategoriesProperty"/> adds categories to the test definition through
/// <c>TestCategory/TestCategoryItem</c> entries.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="TestMetadataProperty"/> adds test-definition metadata. The keys <c>Owner</c>,
/// <c>Priority</c>, and <c>Description</c> populate the corresponding TRX fields. Priority must parse
/// as an integer, and the default <see cref="int.MaxValue"/> is omitted. Other keys become custom
/// key/value entries under <c>Properties/Property</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="TrxWorkItemsProperty"/> adds work-item IDs to the test definition through
/// <c>Workitems/WorkItem</c> entries.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="FileArtifactProperty"/> supplies files to copy into the results directory layout and
/// reference from the individual result's <c>ResultFiles/ResultFile</c> entries.
/// </description>
/// </item>
/// </list>
/// <para>
/// Provide one final state and at most one instance of each identity or optional property above,
/// except <see cref="TestMetadataProperty"/> and <see cref="FileArtifactProperty"/>, which can have multiple instances.
/// Providing more than one instance of any other property makes report generation fail.
/// <see cref="StandardOutputProperty"/> and <see cref="TestFileLocationProperty"/> are not consumed by the TRX reporter.
/// </para>
/// </remarks>
/// <seealso href="https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-test-reports#visual-studio-test-reports-trx">TRX report extension documentation</seealso>
public interface ITrxReportCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Gets a value indicating whether the test framework can provide the properties used for TRX report generation.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Notifies the test framework that TRX reporting is enabled and the documented properties should be
    /// added to its final test-node updates.
    /// </summary>
    void Enable();
}
