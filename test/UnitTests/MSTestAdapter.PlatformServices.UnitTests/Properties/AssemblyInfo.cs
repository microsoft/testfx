// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

#if NET462
using MSTestAdapter.PlatformServices.UnitTests.Utilities;

// Custom attributes for tests.
[assembly: ReflectionUtilityTests.DummyA("a1")]
[assembly: ReflectionUtilityTests.DummyA("a2")]
#endif

#if NETCOREAPP
using MSTestAdapter.PlatformServices.Tests.Services;
using MSTestAdapter.PlatformServices.Tests.Utilities;

[assembly: ReflectionUtilityTests.DummyA("a1")]
[assembly: ReflectionUtilityTests.DummyA("a2")]

[assembly: ReflectionOperationsTests.DummyA("a1")]
[assembly: ReflectionOperationsTests.DummyA("a2")]
#endif

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("599833dc-ec5a-40ca-b5cf-def719548eef")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]

// This is set by GlobalAssemblyInfo which is auto-generated due to import of TestPlatform.NonRazzle.targets
// [assembly: AssemblyVersion("1.0.0.0")]
// [assembly: AssemblyFileVersion("1.0.0.0")]
