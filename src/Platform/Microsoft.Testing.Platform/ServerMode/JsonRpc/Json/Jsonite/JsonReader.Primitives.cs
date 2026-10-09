// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright(c) 2016, Alexandre Mutel
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification
// , are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#pragma warning disable

#if !NETCOREAPP

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Testing.Platform.Helpers;

namespace Jsonite
{
    /// <summary>
    /// A JSON parser and reflector to Dictionary/List.
    /// </summary>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    static partial class Json
    {
        /// <summary>
        /// The internal JsonReader used to deserialize a json text into an object graph.
        /// </summary>
        private partial struct JsonReader
        {
            private object ParseNumber()
            {
                bool isFloat = false;
                bool hasExponent = false;
                bool isNegative = false;
                builder.Length = 0;
                if (c == '-')
                {
                    isNegative = true;
                    builder.Append(c);
                    NextChar();
                }

                if (!IsDigit(c))
                {
                    RaiseUnexpected("while parsing a number after a '-'. Expecting a digit 0-9"); // unit-test: 002-test-error-number1.txt
                }

                // If number starts by 0, we don't expect any digit after
                if (c == '0')
                {
                    builder.Append(c);
                    NextChar();

                    // Make sure that we don't have a digit after
                    if (IsDigit(c))
                    {
                        RaiseUnexpected("while parsing a number. The number '0' must followed by '.' or by an exponent or nothing"); // unit-test: 002-test-error-number2.txt
                    }
                }
                else
                {
                    // Else number starts by non-0, so we can advance as much digits as we have
                    do
                    {
                        builder.Append(c);
                        NextChar();
                    } while (IsDigit(c));
                }

                if (c == '.')
                {
                    isFloat = true;
                    builder.Append('.');
                    NextChar();

                    if (!IsDigit(c))
                    {
                        RaiseUnexpected("while parsing the floating part of a number. Expecting a digit 0-9 after a period '.'"); // unit-test: 002-test-error-number3.txt
                    }

                    do
                    {
                        builder.Append(c);
                        NextChar();
                    } while (IsDigit(c));
                }

                if (c is 'e' or 'E')
                {
                    hasExponent = true;

                    builder.Append(c);
                    NextChar();
                    if (c is '+' or '-')
                    {
                        builder.Append(c);
                        NextChar();
                    }

                    if (!IsDigit(c))
                    {
                        RaiseUnexpected("while parsing the exponent of a number. Expecting a digit 0-9 after an exponent"); // unit-test: 002-test-error-number4.txt
                    }

                    do
                    {
                        builder.Append(c);
                        NextChar();
                    } while (IsDigit(c));
                }

                SkipWhitespacesAndComments();

                // If we are expecting to parse only things into strings, early exit here
                if (settings.ParseValuesAsStrings)
                {
                    return builder.ToString();
                }

                if (isFloat || hasExponent)
                {
                    var numberAsText = builder.ToString();
                    if (settings.ParseFloatAsDecimal)
                    {
                        decimal decimalNumber;
                        if (decimal.TryParse(numberAsText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimalNumber))
                        {
                            return decimalNumber;
                        }
                    }
                    else
                    {
                        double doubleNumber;
                        if (double.TryParse(numberAsText, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleNumber))
                        {
                            return doubleNumber;
                        }
                    }
                }
                else
                {
                    // Fast parse for all integers smaller than  -999999999 <= value <= 999999999
                    // 2147483647
                    //  999999999
                    const int maxIntStringEasyParse = 9;
                    int intNumber = 0;
                    if (builder.Length <= (isNegative ? maxIntStringEasyParse + 1 : maxIntStringEasyParse))
                    {
                        for (int i = isNegative ? 1 : 0; i < builder.Length; i++)
                        {
                            intNumber = intNumber * 10 + (builder[i] - '0');
                        }
                        return isNegative ? -intNumber : intNumber;
                    }

                    // Else go the long way

                    // Try first to parse to an int
                    var numberAsText = builder.ToString();
                    if (int.TryParse(numberAsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out intNumber))
                    {
                        return intNumber;
                    }

                    // Then a long
                    long longNumber;
                    if (long.TryParse(numberAsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out longNumber))
                    {
                        return longNumber;
                    }

                    // Or an ulong
                    ulong ulongNumber;
                    if (ulong.TryParse(numberAsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulongNumber))
                    {
                        return ulongNumber;
                    }

                    // Or a decimal
                    decimal decimalNumber;
                    if (decimal.TryParse(numberAsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimalNumber))
                    {
                        return decimalNumber;
                    }
                }

                RaiseException($"Unable to parse number [{builder}] to a valid C# number ");
                return null;
            }

            private object ParseTrue()
            {
                NextChar();
                if (c == 'r')
                {
                    NextChar();
                    if (c == 'u')
                    {
                        NextChar();
                        if (c == 'e')
                        {
                            NextCharSkipWhitespaces();
                            return settings.ParseValuesAsStrings ? "true" : true;
                        }
                    }
                }
                RaiseUnexpected("while trying to parse a BOOL 'true' value"); // unit-test: 000-test-error-true1.txt and 000-test-error-true2.txt
                return null;
            }

            private object ParseFalse()
            {
                NextChar();
                if (c == 'a')
                {
                    NextChar();
                    if (c == 'l')
                    {
                        NextChar();
                        if (c == 's')
                        {
                            NextChar();
                            if (c == 'e')
                            {
                                NextCharSkipWhitespaces();
                                return settings.ParseValuesAsStrings ? "false" : false;
                            }
                        }
                    }
                }
                RaiseUnexpected("while trying to parse a BOOL 'false' value"); // unit-test: 000-test-error-false1.txt
                return null;
            }

            private object ParseNull()
            {
                NextChar();
                if (c == 'u')
                {
                    NextChar();
                    if (c == 'l')
                    {
                        NextChar();
                        if (c == 'l')
                        {
                            NextCharSkipWhitespaces();
                            return null;
                        }
                    }
                }
                RaiseUnexpected("while trying to parse the NULL 'null' value"); // unit-test: 000-test-error-null1.txt
                return null;
            }

        }
    }
}

#endif
