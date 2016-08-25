// ---------------------------------------------------------------------------
// <copyright file="TestObject.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Base class for TestCase, TestResult.
// </summary>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    ///  Base class for test related classes.
    /// </summary>
#if SILVERLIGHT
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1012:AbstractTypesShouldNotHaveConstructors")]
#endif
    [DataContract]
    public abstract class TestObject
    {
        #region Fields

        [DataMember]
#if !SILVERLIGHT
        private Dictionary<TestProperty, object> store;
#else
        public Dictionary<TestProperty, object> store;
#endif

        #endregion Fields

        #region Constructors

#if !SILVERLIGHT
        protected TestObject()
#else
        public TestObject()
#endif
        {
            this.store = new Dictionary<TestProperty, object>();
        }

        [OnSerializing]
#if !SILVERLIGHT
        private void CacheLazyValuesOnSerializing(StreamingContext context)
#else
        public void CacheLazyValuesOnSerializing(StreamingContext context)
#endif
        {
            var lazyValues = this.store.Where(kvp => kvp.Value is ILazyPropertyValue).ToArray();

            foreach (var kvp in lazyValues)
            {
                var lazyValue = (ILazyPropertyValue)kvp.Value;
                var value = lazyValue.Value;
                this.store.Remove(kvp.Key);

                if (value != null)
                {
                    this.store.Add(kvp.Key, value);
                }
            }
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        ///   Returns the TestProperties currently specified in this TestObject.
        /// </summary>
        public IEnumerable<TestProperty> Properties
        {
            get { return this.store.Keys; }
        }

        /// <summary>
        /// Returns property value of the specify TestProperty
        /// </summary>
        /// <param name="property">TestObject's TestProperty</param>
        /// <returns>property value</returns>
        public object GetPropertyValue(TestProperty property)
        {
            ValidateArg.NotNull(property, "property");

            object defaultValue = null;
            var valueType = property.GetValueType();

# if SILVERLIGHT
            if (valueType != null && valueType.GetTypeInfo().IsValueType)
#else
            if (valueType != null && valueType.IsValueType)
#endif
            {
                defaultValue = Activator.CreateInstance(valueType);
            }

            return PrivateGetPropertyValue(property, defaultValue);
        }

        /// <summary>
        ///   Returns property value of the specify TestProperty
        /// </summary>
        /// <typeparam name="T">Property value type</typeparam>
        /// <param name="property">TestObject's TestProperty</param>
        /// <param name="defaultValue">default property value if property is not present</param>
        /// <returns>property value</returns>
        public T GetPropertyValue<T>(TestProperty property, T defaultValue)
        {
            return GetPropertyValue<T>(property, defaultValue, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///   Set TestProperty's value
        /// </summary>
        /// <typeparam name="T">Property value type</typeparam>
        /// <param name="property">TestObject's TestProperty</param>
        /// <param name="value">value to be set</param>
        public void SetPropertyValue<T>(TestProperty property, T value)
        {
            SetPropertyValue<T>(property, value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///   Set TestProperty's value
        /// </summary>
        /// <typeparam name="T">Property value type</typeparam>
        /// <param name="property">TestObject's TestProperty</param>
        /// <param name="value">value to be set</param>
        public void SetPropertyValue<T>(TestProperty property, LazyPropertyValue<T> value)
        {
            SetPropertyValue<T>(property, value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///   Set TestProperty's value
        /// </summary>
        /// <param name="property">TestObject's TestProperty</param>
        /// <param name="value">value to be set</param>
        public void SetPropertyValue(TestProperty property, object value)
        {
            PrivateSetPropertyValue(property, value);
        }

        /// <summary>
        ///  Remove test property from the current TestObject.
        /// </summary>
        /// <param name="property"></param>
        public void RemovePropertyValue(TestProperty property)
        {
            ValidateArg.NotNull(property, "property");

            object value;
            if (this.store.TryGetValue(property, out value))
            {
                this.store.Remove(property);
            }
        }


        /// <summary>
        ///   Returns TestProperty's value 
        /// </summary>
        /// <returns>property's value. default value is returned if the property is not present</returns>
        public T GetPropertyValue<T>(TestProperty property, T defaultValue, CultureInfo culture)
        {
            ValidateArg.NotNull(property, "property");
            ValidateArg.NotNull(culture, "culture");

            object objValue = PrivateGetPropertyValue(property, defaultValue);

            return ConvertPropertyTo<T>(property, culture, objValue);
        }

        /// <summary>
        ///   Set TestProperty's value to the specified value T.
        /// </summary>
        public void SetPropertyValue<T>(TestProperty property, T value, CultureInfo culture)
        {
            ValidateArg.NotNull(property, "property");
            ValidateArg.NotNull(culture, "culture");

            object objValue = ConvertPropertyFrom<T>(property, culture, value);

            PrivateSetPropertyValue(property, objValue);
        }

        /// <summary>
        ///   Set TestProperty's value to the specified value T.
        /// </summary>
        public void SetPropertyValue<T>(TestProperty property, LazyPropertyValue<T> value, CultureInfo culture)
        {
            ValidateArg.NotNull(property, "property");
            ValidateArg.NotNull(culture, "culture");

            object objValue = ConvertPropertyFrom<T>(property, culture, value);

            PrivateSetPropertyValue(property, objValue);
        }

        #endregion Property Values

        #region Helpers
        /// <summary>
        ///   Return TestProperty's value
        /// </summary>
        /// <returns></returns>
        private object PrivateGetPropertyValue(TestProperty property, object defaultValue)
        {
            ValidateArg.NotNull(property, "property");

            object value;
            if (!this.store.TryGetValue(property, out value))
            {
                value = defaultValue;
            }

            return value;
        }

        /// <summary>
        ///   Set TestProperty's value
        /// </summary>
        private void PrivateSetPropertyValue(TestProperty property, object value)
        {
            ValidateArg.NotNull(property, "property");

            if (property.ValidateValueCallback == null || property.ValidateValueCallback(value))
            {
                this.store[property] = value;
            }
            else
            {
                throw new ArgumentException(property.Label);
            }
        }

        /// <summary>
        ///    Convert passed in value from TestProperty's specified value type.
        /// </summary>
        /// <returns>Converted object</returns>
        private static object ConvertPropertyFrom<T>(TestProperty property, CultureInfo culture, object value)
        {
            ValidateArg.NotNull(property, "property");
            ValidateArg.NotNull(culture, "culture");

            var valueType = property.GetValueType();

#if SILVERLIGHT
            if (valueType != null && valueType.GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()))
#else
            if (valueType != null && valueType.IsAssignableFrom(typeof(T)))
#endif
            {
                return value;
            }

#if SILVERLIGHT
            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resources.ConverterNotSupported, valueType.Name));
#else
            TypeConverter converter = TypeDescriptor.GetConverter(valueType);
            if (converter == null)
            {
                throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resources.ConverterNotSupported, valueType.Name));
            }

            try
            {
                return converter.ConvertFrom(null, culture, value);
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception e)
            {
                // some type converters throw strange exceptions (eg: System.Exception by Int32Converter)
                throw new FormatException(e.Message, e);
            }
#endif
        }

        /// <summary>
        ///   Convert passed in value into the specified type when property is registered.
        /// </summary>
        /// <returns>Converted object</returns>
        private static T ConvertPropertyTo<T>(TestProperty property, CultureInfo culture, object value)
        {
            ValidateArg.NotNull(property, "property");
            ValidateArg.NotNull(culture, "culture");

            var lazyValue = value as LazyPropertyValue<T>;

            if (value == null)
            {
                return default(T);
            }
            else if (value is T)
            {
                return (T)value;
            }
            else if (lazyValue != null)
            {
                return lazyValue.Value;
            }

            var valueType = property.GetValueType();
#if !SILVERLIGHT
            TypeConverter converter = TypeDescriptor.GetConverter(valueType);

            if (converter == null)
            {
                throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resources.ConverterNotSupported, valueType.Name));
            }

            try
            {
                return (T)converter.ConvertTo(null, culture, value, typeof(T));
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception e)
            {
                // some type converters throw strange exceptions (eg: System.Exception by Int32Converter)
                throw new FormatException(e.Message, e);
            }
#else
            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resources.ConverterNotSupported, valueType.Name));
#endif
        }

        #endregion Helpers

        private TraitCollection traits;

        public TraitCollection Traits
        {
            get
            {
                if (this.traits == null)
                {
                    this.traits = new TraitCollection(this);
                }

                return this.traits;
            }
        }
    }
}
