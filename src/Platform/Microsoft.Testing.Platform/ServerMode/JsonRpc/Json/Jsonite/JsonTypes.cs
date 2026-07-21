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
    /// The default object used when a deserializing to an object type.
    /// </summary>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    class JsonObject : Dictionary<string, object?>
    {
        public override string ToString()
        {
            return Json.Serialize(this);
        }
    }

    /// <summary>
    /// The default array used when deserializing to an array type.
    /// </summary>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    class JsonArray : List<object>
    {
        public override string ToString()
        {
            return Json.Serialize(this);
        }
    }

    /// <summary>
    /// Instance exception used when a parsing exception occurred.
    /// </summary>
    /// <remarks>
    /// This exception can be overridden by overriding the method <see cref="IJsonReflector.OnDeserializeRaiseParsingError"/>.
    /// </remarks>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    class JsonException : Exception
    {
        public JsonException(int offset, int line, int column, string message, Exception inner = null) : base(message, inner)
        {
            Offset = offset;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Character offset from the beginning of the text being parsed.
        /// </summary>
        public readonly int Offset;

        /// <summary>
        /// Line position (zero-based) where the error occurred from the beginning of the text being parsed.
        /// </summary>
        public readonly int Line;

        /// <summary>
        /// Column position (zero-based) where the error occurred.
        /// </summary>
        public readonly int Column;

        /// <summary>
        /// Prints the line (1-based) and column (1-based).
        /// </summary>
        /// <returns>A string representation of this object</returns>
        public override string ToString()
        {
            var innerMessage = InnerException != null ? " Check inner exception for more details" : string.Empty;
            return $"({Line + 1},{Column + 1}) : error : {Message}{innerMessage}";
        }
    }

    /// <summary>
    /// Defines serialization and deserialization settings used by <see cref="Json.Parse"/> and <see cref="Json.Serialize"/>
    /// </summary>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    class JsonSettings
    {
        public JsonSettings()
        {
            IndentCount = 2;
            IndentChar = ' ';
            Reflector = JsonReflectorDefault.Instance;
        }

        /// <summary>
        /// Gets or sets the maximum depth used when serializing or deserializing.
        /// </summary>
        public int MaxDepth { get; set; }

        /// <summary>
        /// Gets or sets a value indicating to indent the text when serializing. Default is <c>false</c>.
        /// </summary>
        public bool Indent { get; set; }

        /// <summary>
        /// Gets or sets the number of <see cref="IndentChar"/> used to indent a json output when <see cref="Indent"/> is <c>true</c>.
        /// </summary>
        public int IndentCount { get; set; }

        /// <summary>
        /// Gets or sets the indent character used when <see cref="Indent"/> is <c>true</c>.
        /// </summary>
        public char IndentChar { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether floats should be deserialized to decimal instead of double (default).
        /// </summary>
        public bool ParseFloatAsDecimal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all values should be deserialized to strings instead of numbers.
        /// </summary>
        public bool ParseValuesAsStrings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to allow trailing commas in object and array declaration.
        /// </summary>
        public bool AllowTrailingCommas { get; set; }

        /// <summary>
        /// Gets or sets the reflector used for interfacing the json text to an object graph.
        /// </summary>
        public IJsonReflector Reflector { get; set; }
    }
}

#endif
