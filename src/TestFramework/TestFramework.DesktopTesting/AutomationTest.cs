// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTest.DesktopTesting;

/// <summary>
/// Base test class for desktop UI automation tests.
/// This is the root of the desktop testing class hierarchy, analogous to
/// <c>PlaywrightTest</c> in the Playwright MSTest integration.
/// </summary>
/// <remarks>
/// This layer exists to match the Playwright base class hierarchy pattern
/// and provides the extension point for future automation-level configuration.
/// </remarks>
[TestClass]
public class AutomationTest
{
}
