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

        }
    }
}

#endif
