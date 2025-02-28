// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace TestFramework.ForTestingMSTest;

internal sealed class TrxReportCapability : ITrxReportCapability
{
    public bool IsSupported => true;

    public void Enable()
    {
    }
}
