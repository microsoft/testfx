// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

using MSTestAdapter.PlatformServices.Desktop.UnitTests.Utilities;

// Custom attributes for tests.
[assembly: ReflectionUtilityTests.DummyAAttribute("a1")]
[assembly: ReflectionUtilityTests.DummyAAttribute("a2")]

[assembly: ComVisible(false)]