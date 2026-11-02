// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.ServerMode;

[Embedded]
internal static class RpcIdParser
{
    public static bool TryParseNumericId(string value, out int result)
    {
        int start = value[0] == '-' ? 1 : 0;
        bool isNegative = start == 1;
        int exponentIndex = value.IndexOf('e');
        if (exponentIndex < 0)
        {
            exponentIndex = value.IndexOf('E');
        }

        string mantissa = exponentIndex < 0 ? value.Substring(start) : value.Substring(start, exponentIndex - start);
        int decimalPointIndex = mantissa.IndexOf('.');
        int fractionalDigits = decimalPointIndex < 0 ? 0 : mantissa.Length - decimalPointIndex - 1;
        string digits = decimalPointIndex < 0 ? mantissa : mantissa.Remove(decimalPointIndex, 1);
        if (digits.All(c => c == '0'))
        {
            result = 0;
            return true;
        }

        int exponent = 0;
        if (exponentIndex is not -1
            && !int.TryParse(
                value.Substring(exponentIndex + 1),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out exponent))
        {
            result = default;
            return false;
        }

        long scale = (long)fractionalDigits - exponent;
        long trailingZeroCount = 0;
        switch (Math.Sign(scale))
        {
            case 1:
                {
                    // A scale equal to the digit count also cannot produce an integer: because the all-zero case
                    // returned above, removing every digit necessarily removes at least one non-zero digit.
                    if (scale > digits.Length - 1L)
                    {
                        result = default;
                        return false;
                    }

                    int firstFractionalIndex = digits.Length - (int)scale;
                    for (int i = firstFractionalIndex; i < digits.Length; i++)
                    {
                        if (digits[i] != '0')
                        {
                            result = default;
                            return false;
                        }
                    }

                    digits = digits.Substring(0, firstFractionalIndex);
                    break;
                }

            case -1:
                {
                    trailingZeroCount = -scale;
                    break;
                }
        }

        digits = digits.TrimStart('0');
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long magnitude))
        {
            result = default;
            return false;
        }

        for (long i = 0; i < trailingZeroCount; i++)
        {
            // A scaled value always ends in zero, so it can never be the one Int32 magnitude that differs
            // between signs (2,147,483,648 for Int32.MinValue). The positive limit therefore applies here.
            if (magnitude > 214_748_364)
            {
                result = default;
                return false;
            }

            magnitude *= 10;
        }

        long signedValue = isNegative ? -magnitude : magnitude;
        if (signedValue is < int.MinValue or > int.MaxValue)
        {
            result = default;
            return false;
        }

        result = (int)signedValue;
        return true;
    }
}
