// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace ReliableTestSuite;

/// <summary>
/// A calculator that reads its tax rate from a process-wide environment variable. Unlike
/// <see cref="OrderExporter"/>, this type reaches into genuinely global state: every test in
/// the process shares one copy of the environment block. This is the case that CANNOT be made
/// safe by isolation alone - it must be coordinated (see EnvironmentPricingTests).
/// </summary>
public static class PricingCalculator
{
    public const string TaxRateVariable = "RELIABLE_SUITE_TAX_RATE";

    public static decimal ApplyTax(decimal amount)
    {
        string? raw = Environment.GetEnvironmentVariable(TaxRateVariable);
        decimal rate = raw is null
            ? 0m
            : decimal.Parse(raw, CultureInfo.InvariantCulture);
        return amount + (amount * rate);
    }
}
