// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Resources;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

#if !WIN_UI
// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: NeutralResourcesLanguage("en")]
[assembly: CLSCompliant(true)]

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
// [assembly: AssemblyVersion("1.0.0.0")]
// [assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: ComVisible(false)]
#else
// Extra .0 at the end is a workaround for https://github.com/dotnet/roslyn-analyzers/issues/5728
// Can be removed after the issue is closed.
[assembly: SupportedOSPlatform("windows10.0.17763.0")]
#endif
