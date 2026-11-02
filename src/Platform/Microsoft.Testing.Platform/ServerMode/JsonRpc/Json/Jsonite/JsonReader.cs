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

#if MTP_MSBUILD_TASKS
            public object ParseDocument(object existingObject, Type expectedType, bool expectValue)
            {
                object result = Parse(existingObject, expectedType, expectValue);
                if (c != Eof)
                {
                    RaiseUnexpected("after the end of the JSON document");
                }

                return result;
            }
#endif

        }
    }
}

#endif
