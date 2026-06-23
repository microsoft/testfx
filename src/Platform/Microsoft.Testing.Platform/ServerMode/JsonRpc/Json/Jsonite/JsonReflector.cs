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
        private sealed class JsonReflectorForValidate : IJsonReflector
        {
            public static readonly JsonReflectorForValidate Default = new JsonReflectorForValidate();

            public void Initialize(JsonSettings settings)
            {
            }

            public object OnDeserializeEnterObject(object obj, Type expectedType, out object objectContext)
            {
                objectContext = null;
                return null;
            }

            public void OnDeserializePrepareMemberForObject(object objectContext, object obj, string member, out Type memberType,
                out object memberContext, out object existingMemberValue)
            {
                memberType = typeof(object);
                memberContext = null;
                existingMemberValue = null;
            }

            public void OnDeserializeSetObjectMember(object objectContext, object obj, object memberContext, object value)
            {
            }

            public object OnDeserializeExitObject(object objectContext, object obj)
            {
                return null;
            }

            public object OnDeserializeEnterArray(object obj, Type expectedType, out Type expectedArrayTypeItem, out object arrayContext)
            {
                expectedArrayTypeItem = null;
                arrayContext = null;
                return null;
            }

            public void OnDeserializeAddArrayItem(object arrayContext, object array, int index, object value)
            {
            }

            public object OnDeserializeExitArray(object arrayContext, object obj)
            {
                return null;
            }

            public void OnDeserializeRaiseParsingError(int offset, int line, int column, string message, Exception inner)
            {
                throw new JsonException(offset, line, column, message, inner);
            }


            public JsonObjectType OnSerializeGetObjectType(object obj, Type type, out object objectContext)
            {
                throw new NotImplementedException();
            }

            public bool IsObjectType(Type type)
            {
                throw new NotImplementedException();
            }

            public bool IsArrayType(Type type)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<KeyValuePair<string, object>> OnSerializeGetObjectMembers(object objectContext, object obj)
            {
                throw new NotImplementedException();
            }

            public IEnumerable OnSerializeGetArrayItems(object objectContext, object array)
            {
                throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    /// A callback interface used during the serialization and deserialization.
    /// </summary>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    interface IJsonReflector
    {
        /// <summary>
        /// Initializes this instance with the specified settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        void Initialize(JsonSettings settings);

        /// <summary>
        /// Called when starting to deserialize an object.
        /// </summary>
        /// <param name="obj">An existing object instance (may be null).</param>
        /// <param name="expectedType">The expected type (not null).</param>
        /// <param name="objectContext">The object context that will be passed to other deserialize methods for objects.</param>
        /// <returns>The object instance to deserialize to. The return value must not be null. This instance can be the input <paramref name="obj"/> if not null, or this method could choose to replace the instance by another during the deserialization.</returns>
        object OnDeserializeEnterObject(object obj, Type expectedType, out object objectContext);

        /// <summary>
        /// Called when deserializing a member, before deserializing its value.
        /// </summary>
        /// <param name="objectContext">The object context that was returned by the <see cref="OnDeserializeEnterObject"/></param>
        /// <param name="obj">The object instance (not null).</param>
        /// <param name="member">The member name being deserialized.</param>
        /// <param name="memberType">Expected type of the member.</param>
        /// <param name="memberContext">The member context that will be passed back to <see cref="OnDeserializeSetObjectMember"/>.</param>
        /// <param name="existingMemberValue">The existing member value if any (may be null).</param>
        void OnDeserializePrepareMemberForObject(object objectContext, object obj, string member, out Type memberType, out object memberContext, out object existingMemberValue);

        /// <summary>
        /// Called when deserializing a member value to effectively set the value for the member on the specified object instance.
        /// </summary>
        /// <param name="objectContext">The object context that was returned by the <see cref="OnDeserializeEnterObject"/></param>
        /// <param name="obj">The object instance (not null).</param>
        /// <param name="memberContext">The member context that was generated by <see cref="OnDeserializePrepareMemberForObject"/>.</param>
        /// <param name="value">The value of the member to set on the object.</param>
        void OnDeserializeSetObjectMember(object objectContext, object obj, object memberContext, object value);

        /// <summary>
        /// Called when deserializing an object is done. This method allows to transform the object to another value.
        /// </summary>
        /// <param name="objectContext">The object context.</param>
        /// <param name="obj">The object instance that has been deserialized.</param>
        /// <returns>The final object deserialized (may be different from <paramref name="obj"/>)</returns>
        object OnDeserializeExitObject(object objectContext, object obj);

        /// <summary>
        /// Called when starting to deserialize an array.
        /// </summary>
        /// <param name="obj">An existing array instance (may be null).</param>
        /// <param name="expectedType">The expected type of the array.</param>
        /// <param name="expectedArrayTypeItem">The expected type of an array item.</param>
        /// <param name="arrayContext">The array context that will be passed to other deserialize methods for arrays.</param>
        /// <returns>The array instance to deserialize to. The return value must not be null.</returns>
        object OnDeserializeEnterArray(object obj, Type expectedType, out Type expectedArrayTypeItem, out object arrayContext);

        /// <summary>
        /// Called when deserializing an array item to add to the specified array instance.
        /// </summary>
        /// <param name="arrayContext">The array context that was returned by the <see cref="OnDeserializeEnterArray"/></param>
        /// <param name="array">The array being deserialized.</param>
        /// <param name="index">The index of the next element (may be used for plain arrays).</param>
        /// <param name="value">The value of the item to add to the array.</param>
        void OnDeserializeAddArrayItem(object arrayContext, object array, int index, object value);

        /// <summary>
        /// Called when deserializing an array is done. This method allows to transform the array to another value (transform a list to a plain .NET array for example)
        /// </summary>
        /// <param name="arrayContext">The array context that was returned by the <see cref="OnDeserializeEnterArray"/></param>
        /// <param name="obj">The array instance that has been deserialized.</param>
        /// <returns>The final array instance deserialized (may be different from <paramref name="obj"/>)</returns>
        object OnDeserializeExitArray(object arrayContext, object obj);

        /// <summary>
        /// Called when an error occurred when deserializing. A default implementation should throw a <see cref="JsonException"/>.
        /// </summary>
        /// <param name="offset">The character position from the beginning of the buffer being deserialized.</param>
        /// <param name="line">The line position (zero-based)</param>
        /// <param name="column">The column position (zero-based)</param>
        /// <param name="message">The error message.</param>
        /// <param name="inner">An optional inner exception.</param>
        void OnDeserializeRaiseParsingError(int offset, int line, int column, string message, Exception inner);

        /// <summary>
        /// Called when serializing an object, to determine whether the object is an array or a simple object (with members/properties).
        /// This method is then used to correctly route to <see cref="OnSerializeGetObjectMembers"/> or <see cref="OnSerializeGetArrayItems"/>.
        /// </summary>
        /// <param name="obj">The object instance being serialized</param>
        /// <param name="type">The type of the object being serialized.</param>
        /// <param name="objectContext">An object context that will be passed to other serialize methods.</param>
        /// <returns>The type of the specified object instance (array or object or unknown)</returns>
        JsonObjectType OnSerializeGetObjectType(object obj, Type type, out object objectContext);

        /// <summary>
        /// Called when serializing an object to the members value of this object.
        /// </summary>
        /// <param name="objectContext">The object context that was returned by the <see cref="OnSerializeGetObjectType"/></param>
        /// <param name="obj">The object instance being serialized.</param>
        /// <returns>An enumeration of members [name, value].</returns>
        IEnumerable<KeyValuePair<string, object>> OnSerializeGetObjectMembers(object objectContext, object obj);

        /// <summary>
        /// Called when serializing an array to get the array items.
        /// </summary>
        /// <param name="objectContext">The object context that was returned by the <see cref="OnSerializeGetObjectType"/></param>
        /// <param name="array">The object instance being serialized.</param>
        /// <returns>An enumeration of the array items to serialize.</returns>
        IEnumerable OnSerializeGetArrayItems(object objectContext, object array);
    }

    /// <summary>
    /// Defines the type of object when serializing (returned by method <see cref="IJsonReflector.OnSerializeGetObjectType"/>.
    /// </summary>
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    enum JsonObjectType
    {
        /// <summary>
        /// The object type being serialized is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// The object being serialized is an object with members.
        /// </summary>
        Object,

        /// <summary>
        /// The object being serialized is an array (providing <see cref="IEnumerable"/>)
        /// </summary>
        Array,
    }

    /// <summary>
    /// The default implementation of <see cref="IJsonReflector"/> that allows to deserialize a JSON text to a generic <see cref="IDictionary{TKey,TValue}"/> <see cref="JsonObject"/> or <see cref="JsonArray"/>.
    /// </summary>
    /// <seealso cref="IJsonReflector" />
#if JSONITE_PUBLIC
    public
#else
    internal
#endif
    sealed class JsonReflectorDefault : IJsonReflector
    {
        public static readonly JsonReflectorDefault Instance = new JsonReflectorDefault();

        private JsonReflectorDefault()
        {
        }

        public void Initialize(JsonSettings settings)
        {
        }

        public object OnDeserializeEnterObject(object obj, Type expectedType, out object objectContext)
        {
            if (!typeof(IDictionary<string, object>).GetTypeInfo().IsAssignableFrom(expectedType.GetTypeInfo()) && expectedType != typeof(object))
            {
                throw new ArgumentException($"The default reflector only supports deserializing to a Dictionary<string, object> or a JsonObject instead of [{expectedType}]");
            }

            objectContext = null;
            return expectedType == typeof(object) || expectedType == typeof(JsonObject) || expectedType.GetTypeInfo().IsInterface
                ? new JsonObject()
                : Activator.CreateInstance(expectedType);
        }

        public void OnDeserializePrepareMemberForObject(object objectContext, object obj, string member, out Type expectedMemberType, out object memberContext, out object existingMemberValue)
        {
            memberContext = member;
            expectedMemberType = typeof(object);
            existingMemberValue = null;
        }

        public void OnDeserializeSetObjectMember(object objectContext, object target, object memberContext, object value)
        {
            ((IDictionary<string, object>)target)[(string)memberContext] = value;
        }

        public object OnDeserializeExitObject(object objectContext, object obj)
        {
            return obj;
        }

        public object OnDeserializeEnterArray(object obj, Type expectedType, out Type expectedArrayItemType, out object arrayContext)
        {
            if (!typeof(IList).GetTypeInfo().IsAssignableFrom(expectedType.GetTypeInfo()) && expectedType != typeof(object))
            {
                throw new ArgumentException($"The default reflector only supports deserializing to a IList or a JsonArray instead of [{expectedType}]");
            }

            arrayContext = null;
            expectedArrayItemType = typeof(object);
            return expectedType == typeof(object) || expectedType == typeof(JsonArray) || expectedType.GetTypeInfo().IsInterface
                ? new JsonArray()
                : Activator.CreateInstance(expectedType);
        }

        public void OnDeserializeAddArrayItem(object arrayContext, object array, int index, object value)
        {
            ((IList)array).Add(value);
        }

        public object OnDeserializeExitArray(object arrayContext, object obj)
        {
            return obj;
        }
        public void OnDeserializeRaiseParsingError(int offset, int line, int column, string message, Exception inner)
        {
            throw new JsonException(offset, line, column, message, inner);
        }

        public JsonObjectType OnSerializeGetObjectType(object obj, Type type, out object objectContext)
        {
            objectContext = null;
            var typeInfo = type.GetTypeInfo();
            if (typeof(IDictionary<string, object>).GetTypeInfo().IsAssignableFrom(typeInfo))
            {
                return JsonObjectType.Object;
            }

            if (typeof(IList).GetTypeInfo().IsAssignableFrom(typeInfo) ||
                     typeof(IList<object>).GetTypeInfo().IsAssignableFrom(typeInfo))
            {
                return JsonObjectType.Array;
            }
            return JsonObjectType.Unknown;
        }

        public IEnumerable<KeyValuePair<string, object>> OnSerializeGetObjectMembers(object objectContext, object obj)
        {
            return ((IDictionary<string, object>)obj);
        }

        public IEnumerable OnSerializeGetArrayItems(object objectContext, object array)
        {
            return (IEnumerable)array;
        }
    }
#if NETPRE45
    static class ReflectionHelper
    {
        public static Type GetTypeInfo(this Type type)
        {
            return type;
        }
    }
#endif
}

#endif
