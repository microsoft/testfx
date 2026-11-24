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

        }
    }
}

#endif
