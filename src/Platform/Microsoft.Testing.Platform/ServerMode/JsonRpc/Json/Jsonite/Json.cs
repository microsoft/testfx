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
    /// This partial declaration contains the public entry points and shared utility methods.
    /// Parsing, writing, and reflector-specific implementation details live in other partial declarations.
    /// </summary>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    static partial class Json
    {
        private static readonly JsonSettings DefaultSettings = new JsonSettings();
        private static readonly JsonSettings DefaultSettingsForValidate = new JsonSettings();

        /// <summary>
        /// Deserializes the specified json text into an object.
        /// </summary>
        /// <param name="text">A json text.</param>
        /// <param name="settings">The settings used to deserialize.</param>
        /// <returns>An object representing the deserialized json text</returns>
        /// <exception cref="System.ArgumentNullException">if text is null</exception>
        public static object Deserialize(string text, JsonSettings settings = null)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return Deserialize(new StringReader(text), settings);
        }

        /// <summary>
        /// Deserializes the specified json text into an object.
        /// </summary>
        /// <param name="reader">The reader providing a json text.</param>
        /// <param name="settings">The settings used to deserialize.</param>
        /// <returns>An object representing the deserialized json text</returns>
        /// <exception cref="System.ArgumentNullException">if reader is null</exception>
        public static object Deserialize(TextReader reader, JsonSettings settings = null)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var parser = new JsonReader(reader, settings ?? DefaultSettings);
            return parser.Parse(null, typeof(object), false);
        }

        /// <summary>
        /// Validates the specified json text.
        /// </summary>
        /// <param name="text">A json text.</param>
        /// <param name="settings">The settings used to deserialize.</param>
        /// <exception cref="System.ArgumentNullException">if reader is null</exception>
        /// <exception cref="JsonException">if the json text is not valid</exception>
        public static void Validate(string text, JsonSettings settings = null)
        {
            Validate(new StringReader(text), settings);
        }

        /// <summary>
        /// Validates the specified json text.
        /// </summary>
        /// <param name="reader">The reader providing a json text.</param>
        /// <param name="settings">The settings used to deserialize.</param>
        /// <exception cref="System.ArgumentNullException">if reader is null</exception>
        /// <exception cref="JsonException">if the json text is not valid</exception>
        public static void Validate(TextReader reader, JsonSettings settings = null)
        {
            settings = settings ?? DefaultSettingsForValidate;
            settings.Reflector = JsonReflectorForValidate.Default;
            Deserialize(reader, settings);
        }

        /// <summary>
        /// Serializes the specified value to a json text.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="settings">The settings used to serialize.</param>
        /// <returns>A json string representation of the serialized value</returns>
        public static string Serialize(object? value, JsonSettings settings = null)
        {
            var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            Serialize(value, stringWriter, settings);
            return stringWriter.ToString();
        }

        /// <summary>
        /// Serializes the specified value to a json text.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="writer">The output writer that will contains the serialized json text.</param>
        /// <param name="settings">The settings used to serialize.</param>
        public static void Serialize(object? value, TextWriter writer, JsonSettings settings = null)
        {
            var jsonWriter = new JsonWriter(writer, settings ?? DefaultSettings);
            jsonWriter.Write(value);
        }
        [MethodImpl((MethodImplOptions)256)]
        private static bool IsHighSurrogate(char c)
        {
            if (c >= 55296)
                return c <= 56319;
            return false;
        }

        [MethodImpl((MethodImplOptions)256)]
        private static bool IsLowSurrogate(char c)
        {
            if (c >= 56320)
                return c <= 57343;
            return false;
        }

        [MethodImpl((MethodImplOptions)256)]
        private static bool IsWhiteSpace(char c) =>
            c is ' ' or '\n' or '\t' or '\r';

        [MethodImpl((MethodImplOptions)256)]
        private static bool IsDigit(char c) =>
            c is >= '0' and <= '9';

        [MethodImpl((MethodImplOptions)256)]
        private static int HexToInt(char c)
        {
            if (c is >= '0' and <= '9')
            {
                return c - '0';
            }
            if (c is >= 'a' and <= 'f')
            {
                return c - 'a' + 10;
            }
            return c - 'A' + 10;
        }

        [MethodImpl((MethodImplOptions)256)]
        private static bool IsHex(char c) =>
            c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';


        private static string EscapeChar(char chr)
        {
            // http://stackoverflow.com/questions/12309104/how-to-print-control-characters-in-console-window
            switch (chr)
            {
                case '\'':
                    return @"\'";
                case '"':
                    return "\\\"";
                case '\\':
                    return @"\\";
                case '\0':
                    return @"\0";
                case '\a':
                    return @"\a";
                case '\b':
                    return @"\b";
                case '\f':
                    return @"\f";
                case '\n':
                    return @"\n";
                case '\r':
                    return @"\r";
                case '\t':
                    return @"\t";
                case '\v':
                    return @"\v";
                default:
                    return (char.IsControl(chr) || IsHighSurrogate(chr) || IsLowSurrogate(chr))
                        ? @"\u" + ((int)chr).ToString("X4")
                        : new string(chr, 1);
            }
        }
    }
}

#endif
