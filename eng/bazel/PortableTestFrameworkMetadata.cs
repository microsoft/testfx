// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v8.0", FrameworkDisplayName = ".NET 8.0")]
#elif NET9_0
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v9.0", FrameworkDisplayName = ".NET 9.0")]
#elif NET10_0
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
#elif NET11_0
[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v11.0", FrameworkDisplayName = ".NET 11.0")]
#else
#error The native portable test host requires an explicit target framework.
#endif
