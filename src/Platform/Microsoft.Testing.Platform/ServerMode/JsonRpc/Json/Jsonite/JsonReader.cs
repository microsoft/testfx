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
        private struct JsonReader
        {
            private int offset;
            private int line;
            private int column;
            private char c;
            private readonly JsonSettings settings;
            private readonly IJsonReflector reflector;
            private readonly StringBuilder builder;
            private const char Eof = '\0';
            private bool isEof;
            private readonly bool isValidate;
            private int level;

            public JsonReader(TextReader reader, JsonSettings settings)
            {
                Reader = reader;
                this.settings = settings;
                this.reflector = settings.Reflector ?? JsonReflectorDefault.Instance;
                reflector.Initialize(settings);
                isValidate = reflector is JsonReflectorForValidate;
                offset = 0;
                line = 0;
                column = 0;
                c = Eof;
                level = 0;
                isEof = false;
                builder = new StringBuilder();
                NextCharSkipWhitespaces();
            }

            private TextReader Reader { get; }

            public object Parse(object existingObject, Type expectedType, bool expectValue)
            {
                switch (c)
                {
                    case '{':
                        return ParseObject(existingObject, expectedType);
                    case '[':
                        return ParseArray(existingObject, expectedType);
                    case '"':
                        return ParseString();
                    case 't':
                        return ParseTrue();
                    case 'f':
                        return ParseFalse();
                    case 'n':
                        return ParseNull();
                    default:
                        if (c == '-' || IsDigit(c))
                        {
                            return ParseNumber();
                        }

                        if (c != Eof)
                        {
                            RaiseUnexpected("");
                        }
                        break;

                }

                if (expectValue)
                {
                    RaiseUnexpected("while parsing a value. Expecting OBJECT, ARRAY, STRING, NUMBER, true, false or null"); // unit-test: 020-test-error-object3.txt
                }

                return null;
            }

            private void IncrementLevel()
            {
                level++;
                if (settings.MaxDepth > 0 && level > settings.MaxDepth)
                {
                    RaiseException("The maximum allowed depth [{settings.MaxDepth}] level has been reached. The object graph is too deep");
                }
            }

            private void DecrementLevel()
            {
                level--;
            }

            private object ParseObject(object obj, Type expectedType)
            {
                IncrementLevel();

                NextCharSkipWhitespaces(); // Skip starting {

                // If we are deserializing to a value that is the same as the target, we can reuse it

                object objectContext;
                obj = reflector.OnDeserializeEnterObject(obj, expectedType, out objectContext);

                bool expectMember = false;

                while (c != Eof)
                {
                    if (c == '"')
                    {
                        // Deserialize the member
                        var memberName = ParseString();

                        if (c != ':')
                        {
                            RaiseUnexpected($"while parsing an object. Expecting a colon ':' after a member");  // unit test: 020-test-error-object2.txt
                        }

                        NextCharSkipWhitespaces();

                        Type memberExpectedType;
                        object memberContext;
                        object memberExistingValue;
                        reflector.OnDeserializePrepareMemberForObject(objectContext, obj, memberName, out memberExpectedType, out memberContext, out memberExistingValue);

                        // Deserialize the value
                        var value = Parse(memberExistingValue, memberExpectedType, true);

                        // Sets the value on the object
                        reflector.OnDeserializeSetObjectMember(objectContext, obj, memberContext, value);
                        expectMember = false;

                        if (c == ',')
                        {
                            NextCharSkipWhitespaces();
                            expectMember = true;
                            continue;
                        }
                    }

                    if (c == '}')
                    {
                        break;
                    }

                    RaiseUnexpected("while parsing an object. Expecting a STRING or '}'"); // unit test: 020-test-error-object1.txt
                }

                if (c == Eof)
                {
                    RaiseUnexpected("while parsing an object"); // unit-test: 020-test-error-object4.txt
                }
                else if (expectMember && !settings.AllowTrailingCommas)
                {
                    RaiseUnexpected("while parsing an object. Expecting a STRING after a comma ','");  // unit-test: 020-test-error-object5.txt
                }

                NextCharSkipWhitespaces(); // Skip closing }

                var result = reflector.OnDeserializeExitObject(objectContext, obj);
                DecrementLevel();
                return result;
            }

            private object ParseArray(object array, Type expectedType)
            {
                IncrementLevel();
                NextCharSkipWhitespaces(); // Skip starting [

                Type expectedArrayItemType;
                object arrayContext;
                array = reflector.OnDeserializeEnterArray(array, expectedType, out expectedArrayItemType, out arrayContext);
                bool expectItem = false;

                int index = 0;
                while (c != Eof)
                {
                    if (c == ']')
                    {
                        break;
                    }

                    var value = Parse(null, expectedArrayItemType, true);
                    expectItem = false;

                    // Add the item to the array
                    reflector.OnDeserializeAddArrayItem(arrayContext, array, index++, value);

                    if (c == ']')
                    {
                        break;
                    }

                    if (c == ',')
                    {
                        NextCharSkipWhitespaces();
                        expectItem = true;
                    }
                    else
                    {
                        RaiseUnexpected("while parsing an array"); // unit-test: 030-test-error-array2.txt
                    }
                }

                if (c == Eof)
                {
                    RaiseUnexpected("while parsing an array"); // unit-test: 030-test-error-array1.txt
                }
                else if (expectItem && !settings.AllowTrailingCommas)
                {
                    RaiseUnexpected("while parsing an array. Expecting a STRING, NUMBER, OBJECT, ARRAY, true, false or null after a comma ','"); // unit-test: 030-test-error-array3.txt
                }

                NextCharSkipWhitespaces(); // Skip closing ]

                var result = reflector.OnDeserializeExitArray(arrayContext, array);
                DecrementLevel();
                return result;
            }

            private string ParseString()
            {
                NextChar(); // Skip " but don't skip whitespaces
                builder.Length = 0;
                while (true)
                {
                    // Handle escape
                    switch (c)
                    {
                        case '\\':
                            NextChar();
                            switch (c)
                            {
                                case '"':
                                    builder.Append('"');
                                    NextChar();
                                    continue;
                                case '\\':
                                    builder.Append('\\');
                                    NextChar();
                                    continue;
                                case '/':
                                    builder.Append('/');
                                    NextChar();
                                    continue;
                                case 'b':
                                    builder.Append('\b');
                                    NextChar();
                                    continue;
                                case 'f':
                                    builder.Append('\f');
                                    NextChar();
                                    continue;
                                case 'n':
                                    builder.Append('\n');
                                    NextChar();
                                    continue;
                                case 'r':
                                    builder.Append('\r');
                                    NextChar();
                                    continue;
                                case 't':
                                    builder.Append('\t');
                                    NextChar();
                                    continue;
                                case 'u':
                                    NextChar();
                                    // Must be followed 4 hex numbers (0000-FFFF)
                                    if (IsHex(c)) // 1
                                    {
                                        var value = HexToInt(c);
                                        NextChar();
                                        if (IsHex(c)) // 2
                                        {
                                            value = (value << 4) | HexToInt(c);
                                            NextChar();
                                            if (IsHex(c)) // 3
                                            {
                                                value = (value << 4) | HexToInt(c);
                                                NextChar();
                                                if (IsHex(c)) // 4
                                                {
                                                    value = (value << 4) | HexToInt(c);
                                                    builder.Append((char)value);
                                                    NextChar();
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                    RaiseUnexpected("while parsing a string. Expecting only hexadecimals [0-9a-fA-F] after escape \\u"); // unit-test: 001-test-error-string4.txt
                                    goto end_of_parsing;
                            }
                            RaiseUnexpected("while parsing a string. Only \\ \" b f n r t v u0000-uFFFF are allowed"); // unit-test: 001-test-error-string1.txt
                            goto end_of_parsing;
                        case Eof:
                            RaiseUnexpected("while parsing a string"); // unit-test: 001-test-error-string2.txt
                            goto end_of_parsing;
                        case '"':
                            NextCharSkipWhitespaces();
                            goto end_of_parsing;
                        default:
                            if (c < ' ')
                            {
                                RaiseUnexpected("while parsing a string. Use escape \\ instead"); // unit-test: 001-test-error-string3.txt
                            }
                            builder.Append(c);
                            NextChar();
                            break;
                    }
                }
            end_of_parsing:

                // If we are validating, no need to create a string as we won't use it
                return isValidate ? null : builder.ToString();
            }

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

                // Skip any whitespaces after a value
                while (IsWhiteSpace(c))
                {
                    NextChar();
                }

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

            private void RaiseException(string message)
            {
                reflector.OnDeserializeRaiseParsingError(offset, line, column, message, null);
            }

            private void RaiseUnexpected(string message)
            {
                RaiseException((isEof ? "Unexpected EOF " : $"Unexpected character '{EscapeChar(c)}' ") + message);
            }

            private void NextCharSkipWhitespaces()
            {
                do
                {
                    NextChar();
                } while (IsWhiteSpace(c));
            }

            [MethodImpl((MethodImplOptions)256)]
            private void NextChar()
            {
                var nextChar = Reader.Read();
                if (nextChar < 0)
                {
                    if (c != Eof)
                    {
                        column++;
                        offset++;
                    }
                    isEof = true;
                    c = Eof;
                    return;
                }

                if (c == '\n')
                {
                    offset++;
                    column = 0;
                    line++;
                }
                else if (c != Eof)
                {
                    offset++;
                    column++;
                }
                c = (char)nextChar;
            }

        }
    }
}

#endif
