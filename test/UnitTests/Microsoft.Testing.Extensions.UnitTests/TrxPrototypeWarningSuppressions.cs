// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[assembly: SuppressMessage(
    "Style",
    "IDE0022:Use expression body for method",
    Justification = "The Phase 4 test file is append-only.",
    Scope = "member",
    Target = "~M:Microsoft.Testing.Extensions.UnitTests.TrxPrototypeRealFileSystemTests.TempDirectory.Dispose")]
[assembly: SuppressMessage(
    "Style",
    "IDE0330:Use 'System.Threading.Lock'",
    Justification = "The Phase 4 test file is append-only.",
    Scope = "member",
    Target = "~F:Microsoft.Testing.Extensions.UnitTests.TrxPrototypeRealFileSystemTests.BarrierFileOperations._sync")]
