// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

[assembly: TestExtensionTypes(typeof(MSTestDiscoverer), typeof(MSTestExecutor))]

#pragma warning disable RS0016 // Add public types and members to the declared API (type forwarding is not public API)
[assembly: TypeForwardedTo(typeof(LogMessageListener))]
[assembly: TypeForwardedTo(typeof(MSTestSettings))]
[assembly: TypeForwardedTo(typeof(RunConfigurationSettings))]
[assembly: TypeForwardedTo(typeof(TestAssemblyInfo))]
[assembly: TypeForwardedTo(typeof(TestClassInfo))]
[assembly: TypeForwardedTo(typeof(TestExecutionManager))]
[assembly: TypeForwardedTo(typeof(TestMethod))]
[assembly: TypeForwardedTo(typeof(TestMethodInfo))]
[assembly: TypeForwardedTo(typeof(TestResultExtensions))]
[assembly: TypeForwardedTo(typeof(TestRunCancellationToken))]
[assembly: TypeForwardedTo(typeof(UnitTestOutcome))]
[assembly: TypeForwardedTo(typeof(UnitTestOutcomeExtensions))]
[assembly: TypeForwardedTo(typeof(UnitTestResult))]
#pragma warning restore RS0016
