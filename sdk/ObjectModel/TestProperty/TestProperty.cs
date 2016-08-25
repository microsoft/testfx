// ---------------------------------------------------------------------------
// <copyright file="TestProperty.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
// Defines the test property metadata which could be associated with test case/test result objects. 
// </summary>
// ---------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Reflection;
#if !SILVERLIGHT
using System.Security.Permissions;
#endif

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    public delegate bool ValidateValueCallback(object value);

    [DataContract]
    public partial class TestProperty : IEquatable<TestProperty>
    {
        #region Fields

        [DataMember]
#if !SILVERLIGHT
        private string _id;
#else
        public string _id;
#endif

        [DataMember]
#if !SILVERLIGHT
        private string _label;
#else
        public string _label;
#endif

        [DataMember]
#if !SILVERLIGHT
        private string _description;
#else
        public string _description;
#endif

        [DataMember]
#if !SILVERLIGHT
        private string _category;
#else
        public string _category;
#endif

        private Type valueType;

        [DataMember]
#if !SILVERLIGHT
        private string _strValueType;
#else
        public string _strValueType;
#endif

        [DataMember]
#if !SILVERLIGHT
        private TestPropertyAttributes _attributes;
#else
        public TestPropertyAttributes _attributes;
#endif

        private ValidateValueCallback validateValueCallback;

        #endregion Fields

        #region Constructors

        private TestProperty(string id, string label, string category, string description, Type valueType, ValidateValueCallback validateValueCallback, TestPropertyAttributes attributes)
        {
            ValidateArg.NotNullOrEmpty(id, "id");
            ValidateArg.NotNull(label, "label");
            ValidateArg.NotNull(category, "category");
            ValidateArg.NotNull(description, "description");
            ValidateArg.NotNull(valueType, "valueType");

            // If the type of property is unexpected, then fail as otherwise we will not be to serialize it over the wcf channel and serialize it in db. Fixed bug #754475
            if(!
#if SILVERLIGHT
                (valueType.GetTypeInfo().IsValueType
#else
                (valueType.IsValueType
#endif
                  ||
                valueType.Equals(typeof(String))
                   ||
                valueType.Equals(typeof(Uri))
                   ||
                valueType.Equals(typeof(string[]))
                   ||
                valueType.Equals(typeof(KeyValuePair<string, string>[])))
                )
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.UnexpectedTypeOfProperty, valueType, id));
            }

            this._id = id;
            this._label = label;
            this._category = category;
            this._description = description;
            this._strValueType = valueType.AssemblyQualifiedName;
            this.validateValueCallback = validateValueCallback;
            this._attributes = attributes;
            this.valueType = valueType;
        }

        #endregion Constructors

        #region Properties

        public string Id
        {
            get { return this._id; }
            set { this._id = value; }
        }

        public string Label
        {
            get { return this._label; }
            set { this._label = value; }
        }

        public string Category
        {
            get { return this._category; }
            set { this._category = value; }
        }

        public string Description
        {
            get { return this._description; }
            set { this._description = value; }
        }

        /// <remarks>This property is not required at the client side.</remarks>
        public ValidateValueCallback ValidateValueCallback
        {
            get { return this.validateValueCallback; }
        }

        public TestPropertyAttributes Attributes
        {
            get { return this._attributes; }
            set { this._attributes = value; }
        }

        #endregion Properties

        #region IEquatable

        public override int GetHashCode()
        {
            return this._id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TestProperty);
        }

        public bool Equals(TestProperty other)
        {
            return (other != null) && (this._id == other._id);
        }

        #endregion IEquatable

        #region Methods

        public override string ToString()
        {
            return this._id;
        }

        /// <summary>
        /// Gets the valueType. 
        /// </summary>
        /// <remarks>Only works for the valueType that is in the currently executing assembly or in Mscorlib.dll. The default valueType is of string valueType.</remarks>
        /// <returns>The valueType of the test property</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This could take a bit time, is not simple enough to be a property.")]
        public Type GetValueType()
        {
            if (valueType == null)
                valueType = GetType(_strValueType);
            return valueType;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Use this in the body in debug mode")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We use the default type whenever exception thrown")]
        private Type GetType(string typeName)
        {
            ValidateArg.NotNull(typeName, "typeName");

            Type type = null;

            try
            {
                // This only works for the type is in the currently executing assembly or in Mscorlib.dll.
                type = Type.GetType(typeName);

                if (type == null)
                {
                    type = Type.GetType(typeName.Replace("Version=4.0.0.0", "Version=2.0.0.0")); // Try 2.0 version as discovery returns version of 4.0 for all cases
                }
				
				// For UAP the type namespace for System.Uri,System.TimeSpan and System.DateTimeOffset differs from the desktop version.
				// Hacking to set the correct type only for these two types.
				// [Todo : aajohn] Clean this up.
				if(type == null && typeName.StartsWith("System.Uri"))
				{
					type = typeof(System.Uri);
				}
				else if(type == null && typeName.StartsWith("System.TimeSpan"))
				{
					type = typeof(System.TimeSpan);
				}
				else if(type == null && typeName.StartsWith("System.DateTimeOffset"))
				{
					type = typeof(System.DateTimeOffset);
				}
                // For LineNumber property - Int is required
                else if (type == null && typeName.StartsWith("System.Int16"))
                {
                    type = typeof(System.Int16);
                }
                else if (type == null && typeName.StartsWith("System.Int32"))
                {
                    type = typeof(System.Int32);
                }
                else if (type == null && typeName.StartsWith("System.Int64"))
                {
                    type = typeof(System.Int64);
                }
            }
            catch (Exception)
            {
#if !SILVERLIGHT
                // Try to see if the typeName contains Windows Phone PKT in that case load it from
                // desktop side
                if (typeName.Contains(s_windowsPhonePKT))
                {
                    type = this.GetType(typeName.Replace(s_windowsPhonePKT, s_visualStudioPKT));
                }

                if (type == null)
                {
                    System.Diagnostics.Debug.Fail("The test property type " + typeName + " of property " + this._id + "is not supported.");
#else
                    System.Diagnostics.Debug.WriteLine("The test property type " + typeName + " of property " + this._id + "is not supported.");
#endif
#if !SILVERLIGHT
                }
#endif
            }
            finally
            {
                // default is of string type.
                if (type == null)
                {
                    type = typeof(string);
                }
            }

            return type;
        }

        #endregion Methods
    }

    public partial class TestProperty
    {
        #region Static Fields

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies")]
        private static Dictionary<string, KeyValuePair<TestProperty, HashSet<Type>>> s_properties = new Dictionary<string, KeyValuePair<TestProperty, HashSet<Type>>>();

#if !SILVERLIGHT
        private static string s_visualStudioPKT = "b03f5f7f11d50a3a";
        private static string s_windowsPhonePKT = "7cec85d7bea7798e";
#endif

        #endregion Static Fields

        #region Static Methods

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies")]
        public static void ClearRegisteredProperties()
        {
            lock (s_properties)
            {
                s_properties.Clear();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies")]
        public static TestProperty Find(string id)
        {
            ValidateArg.NotNull(id, "id");

            TestProperty result = null;

            KeyValuePair<TestProperty, HashSet<Type>> propertyTypePair;
            lock (s_properties)
            {
                if (s_properties.TryGetValue(id, out propertyTypePair))
                {
                    result = propertyTypePair.Key;
                }
            }

            return result;
        }

        public static TestProperty Register(string id, string label, Type valueType, Type owner)
        {
            ValidateArg.NotNullOrEmpty(id, "id");
            ValidateArg.NotNull(label, "label");
            ValidateArg.NotNull(valueType, "valueType");
            ValidateArg.NotNull(owner, "owner");

            return Register(id, label, string.Empty, string.Empty, valueType, null, TestPropertyAttributes.None, owner);
        }

        public static TestProperty Register(string id, string label, Type valueType, TestPropertyAttributes attributes, Type owner)
        {
            ValidateArg.NotNullOrEmpty(id, "id");
            ValidateArg.NotNull(label, "label");
            ValidateArg.NotNull(valueType, "valueType");
            ValidateArg.NotNull(owner, "owner");

            return Register(id, label, string.Empty, string.Empty, valueType, null, attributes, owner);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.MSInternal", "CA908:AvoidTypesThatRequireJitCompilationInPrecompiledAssemblies")]
        public static TestProperty Register(string id, string label, string category, string description, Type valueType, ValidateValueCallback validateValueCallback, TestPropertyAttributes attributes, Type owner)
        {
            ValidateArg.NotNullOrEmpty(id, "id");
            ValidateArg.NotNull(label, "label");
            ValidateArg.NotNull(category, "category");
            ValidateArg.NotNull(description, "description");
            ValidateArg.NotNull(valueType, "valueType");
            ValidateArg.NotNull(owner, "owner");

            TestProperty result;

            KeyValuePair<TestProperty, HashSet<Type>> propertyTypePair;

            lock (s_properties)
            {
                if (s_properties.TryGetValue(id, out propertyTypePair))
                {
                    // verify the data valueType is valid
                    if (propertyTypePair.Key._strValueType == valueType.AssemblyQualifiedName)
                    {
                        // add the owner to set of owners for this GraphProperty object
                        propertyTypePair.Value.Add(owner);
                        result = propertyTypePair.Key;
                    }
                    else
                    {
                        // not the data valueType we expect, throw an exception
                        string message = string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.Exception_RegisteredTestPropertyHasDifferentValueType,
                            id,
                            valueType.ToString(),
                            propertyTypePair.Key._strValueType);

                        throw new InvalidOperationException(message);
                    }
                }
                else
                {
                    // create a new TestProperty object
                    result = new TestProperty(id, label, category, description, valueType, validateValueCallback, attributes);

                    // setup the data pair used to track owners of this GraphProperty
                    propertyTypePair = new KeyValuePair<TestProperty, HashSet<Type>>(result, new HashSet<Type>());
                    propertyTypePair.Value.Add(owner);

                    // add to the dictionary
                    s_properties[id] = propertyTypePair;
                }
            }

            return result;
        }

        public static bool TryUnregister(string id, out KeyValuePair<TestProperty, HashSet<Type>> propertyTypePair)
        {
            ValidateArg.NotNullOrEmpty(id, "id");

            lock (s_properties)
            {
                if (s_properties.TryGetValue(id, out propertyTypePair))
                {
                    return s_properties.Remove(id);
                }
            }
            return false;
        }

        #endregion Static Methods
    }

#if !SILVERLIGHT
    partial class TestProperty : IObjectReference
#else
    partial class TestProperty
#endif
    {
#if !SILVERLIGHT
        [SecurityPermission(
            SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
#endif
        public object GetRealObject(StreamingContext context)
        {
            var registeredProperty = TestProperty.Find(this._id);
            if (registeredProperty == null)
            {
                registeredProperty = TestProperty.Register(
                    this._id,
                    this._label,
                    this._category,
                    this._description,
                    this.GetValueType(),
                    this.validateValueCallback,
                    this._attributes,
                    typeof(TestObject));
            }

            return registeredProperty;
        }
    }
}
