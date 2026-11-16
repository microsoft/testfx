// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

// MSTest 5 defines these public types in MSTest.TestFramework rather than in this assembly. Forwarding them keeps
// binaries compiled against MSTest 4 (for example, Microsoft.Playwright.MSTest.v4) and assembly-qualified type names
// that still name this assembly resolving to the single core definition, without a rebuild.
[assembly: TypeForwardedTo(typeof(TestContext))]
[assembly: TypeForwardedTo(typeof(MessageLevel))]
#pragma warning disable MSTESTEXP // Forwarding an experimental type does not use it.
[assembly: TypeForwardedTo(typeof(TestRun))]
[assembly: TypeForwardedTo(typeof(ITestRunInfo))]
[assembly: TypeForwardedTo(typeof(PlannedTest))]
#pragma warning restore MSTESTEXP

#if !NETSTANDARD2_0 && !WINDOWS_UWP && !WIN_UI
[assembly: TypeForwardedTo(typeof(DeploymentItemAttribute))]
#endif

#if NETFRAMEWORK
[assembly: TypeForwardedTo(typeof(DataSourceElement))]
[assembly: TypeForwardedTo(typeof(DataSourceElementCollection))]
[assembly: TypeForwardedTo(typeof(PrivateObject))]
[assembly: TypeForwardedTo(typeof(PrivateType))]
[assembly: TypeForwardedTo(typeof(TestConfiguration))]
[assembly: TypeForwardedTo(typeof(TestConfigurationSection))]
#endif
