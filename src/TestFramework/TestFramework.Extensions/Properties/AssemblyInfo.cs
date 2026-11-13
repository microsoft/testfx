// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable MSTESTEXP // Preserve compiled references to the relocated experimental APIs.
[assembly: TypeForwardedTo(typeof(ITestRunInfo))]
[assembly: TypeForwardedTo(typeof(MessageLevel))]
[assembly: TypeForwardedTo(typeof(PlannedTest))]
[assembly: TypeForwardedTo(typeof(TestContext))]
[assembly: TypeForwardedTo(typeof(TestRun))]
#pragma warning restore MSTESTEXP

#if NETFRAMEWORK
[assembly: TypeForwardedTo(typeof(DataSourceElement))]
[assembly: TypeForwardedTo(typeof(DataSourceElementCollection))]
[assembly: TypeForwardedTo(typeof(PrivateObject))]
[assembly: TypeForwardedTo(typeof(PrivateType))]
[assembly: TypeForwardedTo(typeof(TestConfiguration))]
[assembly: TypeForwardedTo(typeof(TestConfigurationSection))]
#endif

#if !NETSTANDARD2_0 && !WINDOWS_UWP && !WIN_UI
[assembly: TypeForwardedTo(typeof(DeploymentItemAttribute))]
#endif

#if !WIN_UI
[assembly: CLSCompliant(true)]
[assembly: ComVisible(false)]
#else
// Extra .0 at the end is a workaround for https://github.com/dotnet/roslyn-analyzers/issues/5728
// Can be removed after the issue is closed.
[assembly: SupportedOSPlatform("windows10.0.17763.0")]
#endif
