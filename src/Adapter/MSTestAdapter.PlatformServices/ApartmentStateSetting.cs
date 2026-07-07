// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Apartment state parsed from the <c>ExecutionThreadApartmentState</c> run settings and the
/// <c>mstest:execution:executionApartmentState</c> configuration value. This is the adapter's own neutral
/// replacement for the former VSTest <c>PlatformAbstractions.PlatformApartmentState</c> enum.
/// </summary>
internal enum ApartmentStateSetting
{
    // The member order is load-bearing: Enum.TryParse maps numeric-string run settings values ("0"/"1")
    // by number, so MTA must stay 0 and STA must stay 1 to preserve parse compatibility with the former
    // VSTest PlatformAbstractions.PlatformApartmentState enum. Do not reorder.
    MTA,
    STA,
}
