// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

internal class TestHostProcessExitRequest : IRequest
{
    public TestHostProcessExitRequest(int returnCode)
    {
        ExitCode = returnCode;
    }

    public int ExitCode { get; }
}
