// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET11_0
// Match the explicit native Bazel toolchain, not the separate Arcade engineering SDK.
[assembly: System.Reflection.AssemblyMetadata("Net11RuntimeVersion", "11.0.0-rc.3.26522.148")]
#endif
