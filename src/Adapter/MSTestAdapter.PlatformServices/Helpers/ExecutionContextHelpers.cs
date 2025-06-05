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
            // CreateCopy doesn't do anything on .NET Core as ExecutionContexts are immutable.
            // But it's important on .NET Framework.
            // On .NET Framework, ExecutionContext.Run cannot be called twice with the same ExecutionContext.
            // Otherwise, it will throw InvalidOperationException with message:
            // Cannot apply a context that has been marshaled across AppDomains, that was not acquired through a Capture operation or that has already been the argument to a Set call.
            executionContext = executionContext.CreateCopy();
            ExecutionContext.Run(executionContext, static action => ((Action)action!).Invoke(), action);
        }
    }
}
