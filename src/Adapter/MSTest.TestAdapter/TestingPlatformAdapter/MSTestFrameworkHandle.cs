// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// A native Microsoft.Testing.Platform (MTP) <see cref="IFrameworkHandle"/> for the MSTest native path. It only
/// forwards diagnostic messages to the MTP <see cref="IOutputDevice"/>; test results are reported natively through
/// the <c>MtpTestResultRecorder</c>, so the VSTest result-recording members are intentionally no-ops.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestFrameworkHandle : IFrameworkHandle, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputDevice;
    private readonly IExtension _extension;
    private readonly CancellationToken _cancellationToken;

    public MSTestFrameworkHandle(IOutputDevice outputDevice, IExtension extension, CancellationToken cancellationToken)
    {
        _outputDevice = outputDevice;
        _extension = extension;
        _cancellationToken = cancellationToken;
    }

    public bool EnableShutdownAfterTestRun { get; set; }

    string IExtension.Uid => _extension.Uid;

    string IExtension.Version => _extension.Version;

    string IExtension.DisplayName => _extension.DisplayName;

    string IExtension.Description => _extension.Description;

    public int LaunchProcessWithDebuggerAttached(string filePath, string? workingDirectory, string? arguments, IDictionary<string, string?>? environmentVariables)
        => -1;

    // Results are reported natively through the recorder; the VSTest recording members are not used.
    public void RecordResult(TestResult testResult)
    {
    }

    public void RecordStart(TestCase testCase)
    {
    }

    public void RecordEnd(TestCase testCase, TestOutcome outcome)
    {
    }

    public void RecordAttachments(IList<AttachmentSet> attachmentSets)
    {
    }

    public void SendMessage(TestMessageLevel testMessageLevel, string message)
    {
        IOutputDeviceData data = testMessageLevel switch
        {
            TestMessageLevel.Informational => new TextOutputDeviceData(message),
            TestMessageLevel.Warning => new WarningMessageOutputDeviceData(message),
            TestMessageLevel.Error => new ErrorMessageOutputDeviceData(message),
            _ => throw new NotSupportedException($"Unsupported logging level '{testMessageLevel}'."),
        };

        _outputDevice.DisplayAsync(this, data, _cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    Task<bool> IExtension.IsEnabledAsync() => _extension.IsEnabledAsync();
}
#endif
