// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
///  A facade service for options passed to a test method.
/// </summary>
internal sealed record TestMethodOptions(TimeoutInfo TimeoutInfo, ExpectedExceptionBaseAttribute? ExpectedException, ITestContext? TestContext, bool CaptureDebugTraces, TestMethodAttribute? Executor);
