// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using TestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// Bridge implementation of <see cref="IFrameworkHandle"/> that forwards calls to VSTest and Microsoft Testing Platforms.
/// </summary>
internal sealed class FrameworkHandlerAdapter : IFrameworkHandle
{
    /// <remarks>
    /// Not null when used in the context of VSTest.
    /// </remarks>
    private readonly IFrameworkHandle? _frameworkHandle;
    private readonly ILogger<FrameworkHandlerAdapter> _logger;
    private readonly IMessageBus _messageBus;
    private readonly VSTestBridgedTestFrameworkBase _adapterExtensionBase;
    private readonly TestSessionContext _session;
    private readonly CancellationToken _cancellationToken;
    private readonly bool _isTrxEnabled;
    private readonly MessageLoggerAdapter _comboMessageLogger;
    private readonly string _testAssemblyPath;

    public FrameworkHandlerAdapter(VSTestBridgedTestFrameworkBase adapterExtensionBase, TestSessionContext session, string[] testAssemblyPaths,
        ITestApplicationModuleInfo testApplicationModuleInfo, ILoggerFactory loggerFactory, IMessageBus messageBus, IOutputDevice outputDevice,
        bool isTrxEnabled, CancellationToken cancellationToken, IFrameworkHandle? frameworkHandle = null)
    {
        if (testAssemblyPaths.Length == 0)
        {
            throw new ArgumentException($"{nameof(testAssemblyPaths)} should contain at least one test assembly.");
        }
        else if (testAssemblyPaths.Length > 1)
        {
            _testAssemblyPath = testApplicationModuleInfo.GetCurrentTestApplicationFullPath();

            if (!testAssemblyPaths.Contains(_testAssemblyPath))
            {
                throw new ArgumentException("None of the test assemblies are the test application.");
            }
        }
        else
        {
            _testAssemblyPath = testAssemblyPaths[0];
        }

        _frameworkHandle = frameworkHandle;
        _logger = loggerFactory.CreateLogger<FrameworkHandlerAdapter>();
        _messageBus = messageBus;
        _adapterExtensionBase = adapterExtensionBase;
        _session = session;
        _cancellationToken = cancellationToken;
        _isTrxEnabled = isTrxEnabled;
        _comboMessageLogger = new MessageLoggerAdapter(loggerFactory, outputDevice, adapterExtensionBase, frameworkHandle);
    }

    /// <inheritdoc/>
    public bool EnableShutdownAfterTestRun
    {
        get => _frameworkHandle?.EnableShutdownAfterTestRun ?? false;
        set
        {
            _logger.LogTraceAsync($"{nameof(FrameworkHandlerAdapter)}.EnableShutdownAfterTestRun: set to {value}").Await();
            if (_frameworkHandle is not null)
            {
                _frameworkHandle.EnableShutdownAfterTestRun = value;
            }
        }
    }

    /// <inheritdoc/>
    public int LaunchProcessWithDebuggerAttached(string filePath, string? workingDirectory, string? arguments,
        IDictionary<string, string?>? environmentVariables)
    {
        _logger.LogTraceAsync($"{nameof(FrameworkHandlerAdapter)}.LaunchProcessWithDebuggerAttached").Await();
        return _frameworkHandle?.LaunchProcessWithDebuggerAttached(filePath, workingDirectory, arguments, environmentVariables)
            ?? -1;
    }

    /// <inheritdoc/>
    public void RecordAttachments(IList<AttachmentSet> attachmentSets)
    {
        _logger.LogTraceAsync($"{nameof(FrameworkHandlerAdapter)}.RecordAttachments").Await();
        _frameworkHandle?.RecordAttachments(attachmentSets);
        PublishAttachmentsAsync(attachmentSets).Await();
    }

    /// <inheritdoc/>
    public void RecordEnd(TestCase testCase, TestOutcome outcome)
    {
        _logger.LogTraceAsync($"{nameof(FrameworkHandlerAdapter)}.RecordEnd").Await();

        _cancellationToken.ThrowIfCancellationRequested();

        testCase.FixUpTestCase(_testAssemblyPath);

        // Forward call to VSTest
        _frameworkHandle?.RecordEnd(testCase, outcome);
    }

    /// <inheritdoc/>
    public void RecordResult(TestResult testResult)
    {
        _logger.LogTraceAsync($"{nameof(FrameworkHandlerAdapter)}.RecordResult").Await();

        _cancellationToken.ThrowIfCancellationRequested();

        testResult.TestCase.FixUpTestCase(_testAssemblyPath);

        // Forward call to VSTest
        _frameworkHandle?.RecordResult(testResult);

        // Publish node state change to Microsoft Testing Platform
        var testNode = testResult.ToTestNode(_isTrxEnabled, _session.Client);

        var testNodeChange = new TestNodeUpdateMessage(_session.SessionUid, testNode);
        _messageBus.PublishAsync(_adapterExtensionBase, testNodeChange).Await();

        PublishAttachmentsAsync(testResult.Attachments, testNode).Await();
    }

    /// <inheritdoc/>
    public void RecordStart(TestCase testCase)
    {
        _logger.LogTraceAsync($"{nameof(FrameworkHandlerAdapter)}.RecordStart").Await();

        _cancellationToken.ThrowIfCancellationRequested();

        testCase.FixUpTestCase(_testAssemblyPath);

        // Forward call to VSTest
        _frameworkHandle?.RecordStart(testCase);

        // Publish node state change to Microsoft Testing Platform
        var testNode = testCase.ToTestNode(_isTrxEnabled, _session.Client);
        testNode.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
        var testNodeChange = new TestNodeUpdateMessage(_session.SessionUid, testNode);

        _messageBus.PublishAsync(_adapterExtensionBase, testNodeChange).Await();
    }

    /// <inheritdoc/>
    public void SendMessage(TestMessageLevel testMessageLevel, string message)
        => _comboMessageLogger.SendMessage(testMessageLevel, message);

    private async Task PublishAttachmentsAsync(IEnumerable<AttachmentSet> attachments, TestNode? testNode = null)
    {
        foreach (AttachmentSet attachmentSet in attachments)
        {
            foreach (UriDataAttachment attachment in attachmentSet.Attachments)
            {
                if (!attachment.Uri.IsFile)
                {
                    throw new FormatException($"Test adapter {_adapterExtensionBase.DisplayName} only supports file attachments.");
                }

                SessionFileArtifact fileArtifact = testNode is null
                    ? new SessionFileArtifact(_session.SessionUid, new(attachment.Uri.LocalPath), attachmentSet.DisplayName, attachment.Description)
                    : new TestNodeFileArtifact(_session.SessionUid, testNode, new(attachment.Uri.LocalPath), attachmentSet.DisplayName, attachment.Description);
                await _messageBus.PublishAsync(_adapterExtensionBase, fileArtifact);
            }
        }
    }
}
