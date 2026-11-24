// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET10_0
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
#elif NET11_0
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v11.0", FrameworkDisplayName = ".NET 11.0")]
#else
#error The native portable test host requires an explicit target framework.
#endif

// MSBuild stamps $(Configuration); rules_dotnet defines DEBUG for dbg/fastbuild and RELEASE for opt.
#if DEBUG
[assembly: System.Reflection.AssemblyConfiguration("Debug")]
#else
[assembly: System.Reflection.AssemblyConfiguration("Release")]
#endif
