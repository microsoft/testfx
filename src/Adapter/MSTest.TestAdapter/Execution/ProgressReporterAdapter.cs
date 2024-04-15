// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal class ProgressReporterAdapter : IProgressReporter
{
    private readonly IFrameworkHandle _testExecutionRecorder;

    public ProgressReporterAdapter(IFrameworkHandle testExecutionRecorder)
    {
        _testExecutionRecorder = testExecutionRecorder;
    }

    public void ReportProgress(string message, int percent, bool done)
    {
        _testExecutionRecorder.SendMessage(TestMessageLevel.Warning, message);
    }
}
