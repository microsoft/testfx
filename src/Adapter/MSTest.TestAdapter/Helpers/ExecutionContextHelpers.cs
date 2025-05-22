// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class ExecutionContextHelpers
{
    internal static void RunOnContext(ExecutionContext? executionContext, Action action)
    {
        if (executionContext is null)
        {
            action();
        }
        else
        {
            ExecutionContext.Run(executionContext, static action => ((Action)action!).Invoke(), action);
        }
    }
}
