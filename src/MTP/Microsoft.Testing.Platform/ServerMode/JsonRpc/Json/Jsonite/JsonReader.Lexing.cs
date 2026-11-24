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
                NextChar();
                SkipWhitespacesAndComments();
            }

            private void SkipWhitespacesAndComments()
            {
                while (true)
                {
                    while (IsWhiteSpace(c))
                    {
                        NextChar();
                    }

                    if (!settings.AllowComments || c != '/')
                    {
                        return;
                    }

                    switch (Reader.Peek())
                    {
                        case '/':
                            do
                            {
                                NextChar();
                            }
                            while (!isEof && c is not ('\r' or '\n'));
                            break;

                        case '*':
                            NextChar();
                            while (true)
                            {
                                NextChar();
                                if (isEof)
                                {
                                    RaiseUnexpected("while parsing a comment. Expecting the end of a comment '*/'");
                                }

                                if (c == '*' && Reader.Peek() == '/')
                                {
                                    NextChar();
                                    NextChar();
                                    break;
                                }
                            }

                            break;

                        default:
                            return;
                    }
                }
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
