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

namespace Microsoft.Testing.Platform.ServerMode.JsonRpc.Json.Jsonite
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
        /// The internal class used to serialize an object graph to a json text.
        /// </summary>
        private class JsonWriter
        {
            private readonly TextWriter writer;
            private readonly JsonSettings settings;
            private readonly IJsonReflector reflector;
            private readonly bool indent;
            private readonly char indentChar;
            private readonly int indentCount;
            private int indentLevel;
            private readonly Dictionary<Type, Action<object>> writers;

            public JsonWriter(TextWriter writer, JsonSettings settings)
            {
                this.settings = settings;
                this.reflector = settings.Reflector ?? JsonReflectorDefault.Instance;
                this.writer = writer;
                this.indent = settings.Indent;
                this.indentChar = settings.IndentChar;
                this.indentCount = settings.IndentCount;
                this.indentLevel = 0;
                writers = new Dictionary<Type, Action<object>>
                {
                    // These converters have to match to the one declared in JsonReflectorDefault.Converters
                    {typeof (string), value => WriteString((string) value)},
                    {typeof (bool), value => { writer.Write((bool) value ? "true" : "false"); }},
                    {typeof (char), value =>  { writer.Write(((char) value).ToString()); }},
                    {typeof (byte), value =>  { writer.Write(((byte) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (sbyte), value => { writer.Write(((sbyte) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (short), value =>  { writer.Write(((short) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (ushort), value => { writer.Write(((ushort) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (int), value =>  { writer.Write(((int) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (uint), value => { writer.Write(((uint) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (long), value =>  { writer.Write(((long) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (ulong), value => { writer.Write(((ulong) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (float), value =>  { writer.Write(((double)(float) value).ToString("R", CultureInfo.InvariantCulture)); }},
                    {typeof (double), value => { writer.Write(((double) value).ToString("R", CultureInfo.InvariantCulture)); }},
                    {typeof (decimal), value =>  { writer.Write(((decimal) value).ToString(CultureInfo.InvariantCulture)); }},
                    {typeof (Type), value => { WriteString(value.ToString()); }},
                    {typeof (Guid), value =>  { WriteString(((Guid)value).ToString("D")); }},
                    {typeof (StringBuilder), value => WriteString(((StringBuilder) value).ToString())}, // TODO: Could be optimized but it is a very uncommon case, so...
                    {typeof (DateTime), value => { WriteString(((DateTime)value).ToString("o")); }},
                    {typeof (DateTimeOffset), value => { WriteString(((DateTimeOffset)value).ToString("o")); }},
                    // {typeof (TimeSpan), value => { WriteString(((TimeSpan)value).ToString()); }}, // TODO: handle correctly, remove for now
                };
            }

            public void Write(object? value)
            {
                if (value == null)
                {
                    writer.Write("null");
                    return;
                }

                var type = value.GetType();
                Action<object> valueWriter;

                if (writers.TryGetValue(type, out valueWriter))
                {
                    valueWriter(value);
                    return;
                }

                object objectContext;
                var objectType = reflector.OnSerializeGetObjectType(value, type, out objectContext);
                switch (objectType)
                {
                    case JsonObjectType.Object:
                        WriteObject(reflector.OnSerializeGetObjectMembers(objectContext, value));
                        break;
                    case JsonObjectType.Array:
                        WriteArray(reflector.OnSerializeGetArrayItems(objectContext, value));
                        break;
                    default:
                        // Try to serialize as a string
                        WriteString(Convert.ToString(value, CultureInfo.InvariantCulture));
                        break;
                }

                //throw new InvalidOperationException($"Unsupported object type [{value.GetType()}]");
            }

            private void WriteObject(IEnumerable<KeyValuePair<string, object>> members)
            {
                writer.Write('{');
                indentLevel++;
                bool isFirst = true;
                foreach (var keyValue in members)
                {
                    if (!isFirst)
                    {
                        writer.Write(',');
                    }

                    if (indent)
                    {
                        writer.Write('\n');
                        Indent();
                    }

                    WriteString(keyValue.Key);
                    writer.Write(':');
                    // Original jsonite code
                    //if (indent)
                    //{
                    //    writer.Write(' ');
                    //}

                    // Updated code to be in pair with the System.Text.Json code
                    writer.Write(' ');


                    Write(keyValue.Value);
                    isFirst = false;
                }
                indentLevel--;
                if (!isFirst && indent)
                {
                    writer.Write('\n');
                    Indent();
                }
                writer.Write('}');
            }

            private void WriteArray(IEnumerable list)
            {
                // Serialize list
                writer.Write('[');
                indentLevel++;
                bool isFirst = true;
                foreach (var item in list)
                {
                    if (!isFirst)
                    {
                        writer.Write(',');
                    }
                    if (indent)
                    {
                        writer.Write('\n');
                        Indent();
                    }
                    Write(item);
                    isFirst = false;
                }
                indentLevel--;
                if (!isFirst && indent)
                {
                    writer.Write('\n');
                    Indent();
                }
                writer.Write(']');
            }

            [MethodImpl((MethodImplOptions)256)]
            private void Indent()
            {
                for (int i = 0; i < indentCount * indentLevel; i++)
                {
                    writer.Write(indentChar);
                }
            }

            private void WriteString(string text)
            {
                writer.Write('"');
                for (int i = 0; i < text.Length; i++)
                {
                    var c = text[i];

                    switch (c)
                    {
                        case '"':
                            writer.Write('\\');
                            writer.Write('\"');
                            break;
                        case '\\':
                            writer.Write('\\');
                            writer.Write('\\');
                            break;
                        case '\b':
                            writer.Write('\\');
                            writer.Write('b');
                            break;
                        case '\f':
                            writer.Write('\\');
                            writer.Write('f');
                            break;
                        case '\n':
                            writer.Write('\\');
                            writer.Write('n');
                            break;
                        case '\r':
                            writer.Write('\\');
                            writer.Write('r');
                            break;
                        case '\t':
                            writer.Write('\\');
                            writer.Write('t');
                            break;
                        default:
                            // Intentionally diverging from the upstream code from xoofx/jsonite. See https://github.com/microsoft/testfx/issues/5120

                            // Also, https://datatracker.ietf.org/doc/html/rfc4627#section-2.5
                            if (c < ' ')
                            {
                                writer.Write('\\');
                                writer.Write('u');
                                writer.Write(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                            }
                            else if (IsHighSurrogate(c))
                            {
                                // If we are dealing with high surrogate, we need to lookahead.
                                // If it's followed by a low surrogate, then we encode normally.
                                if (i + 1 < text.Length && IsLowSurrogate(text[i + 1]))
                                {
                                    writer.Write('\\');
                                    writer.Write('u');
                                    writer.Write(((int)c).ToString("X4", CultureInfo.InvariantCulture));

                                    writer.Write('\\');
                                    writer.Write('u');
                                    writer.Write(((int)text[i + 1]).ToString("X4", CultureInfo.InvariantCulture));
                                    i++;
                                }
                                else
                                {
                                    // If the high surrogate is the last in the string, or is not followed by a low surrogate.
                                    // Then the string is invalid, and in that case we write a fallback char matching Rune.ReplacementChar
                                    writer.Write("\\uFFFD");
                                }
                            }
                            else if (IsLowSurrogate(c))
                            {
                                // If we hit this, it means we are dealing with a low surrogate, but the previous char wasn't a high surrogate.
                                // This is also invalid and we must write a fallback char instead.
                                writer.Write("\\uFFFD");
                            }
                            else
                            {
                                writer.Write(c);
                            }
                            break;
                    }
                }
                writer.Write('"');
            }
        }
    }
}

#endif
