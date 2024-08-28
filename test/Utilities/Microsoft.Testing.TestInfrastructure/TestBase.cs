// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;

namespace Microsoft.Testing.TestInfrastructure;

public abstract class TestBase
{
    protected TestBase(ITestExecutionContext testExecutionContext) => TestsRunWatchDog.AddTestRun(testExecutionContext.TestInfo.StableUid);
}
