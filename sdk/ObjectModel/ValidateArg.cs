//------------------------------------------------------------------------------
// <copyright file='ValidateArg.cs' company='Microsoft Corporation'>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
//     Information Contained Herein is Proprietary and Confidential.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Helper to validate parameters.
    /// </summary>
    public static class ValidateArg
    {
        /// <summary>
        /// Throws ArgumentNullException if the argument is null, otherwise passes it through.
        /// </summary>
        /// <param name="arg">The argument to check.</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static T NotNull<T>([ValidatedNotNull]T arg, string parameterName)
            where T : class
        {
            if (arg == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            return arg;
        }

        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static string NotNullOrEmpty([ValidatedNotNull]string arg, string parameterName)
        {
            if (string.IsNullOrEmpty(arg))
            {
                throw new ArgumentNullException(parameterName);
            }
            return arg;
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the argument is less than zero.
        /// </summary>
        /// <param name="arg">The argument to check.</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void NotNegative(int arg, string parameterName)
        {
            if (arg < 0)
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentIsNegative);
                throw new ArgumentOutOfRangeException(parameterName, arg, message);
            }
        }

        /// <summary>
        /// Throws ArgumentOutOfRangeException if the argument is less than zero.
        /// </summary>
        /// <param name="arg">The argument to check.</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void NotNegative(long arg, string parameterName)
        {
            if (arg < 0)
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentIsNegative);
                throw new ArgumentOutOfRangeException(parameterName, arg, message);
            }
        }

        /// <summary>
        /// Throws ArgumentNullException if the string is null, ArgumentException if the string is empty.
        /// </summary>
        /// <param name="arg">The argument to check.</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void NotNullOrEmpty<T>([ValidatedNotNull]IEnumerable<T> arg, string parameterName)
        {
            NotNull(arg, parameterName);

            if (!arg.Any())
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentIsEmpty);
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Throws ArgumentNullException if the argument is null, ArgumentException if the argument is not the correct type.
        /// </summary>
        /// <param name="arg">The argument to check.</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        /// <typeparam name="T">The type of the expected argument.</typeparam>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "This is shared source. This method may not be called in the current assembly.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void TypeOf<T>([ValidatedNotNull]object arg, string parameterName) where T : class
        {
            NotNull(arg, parameterName);

            if (!(arg is T))
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentNotTypeOf, typeof(T).FullName);
                throw new ArgumentException(message, parameterName);
            }
        }
    }

    /// <summary>
    /// Helper to validate parameter properties.
    /// </summary>
    public static class ValidateArgProperty
    {
        /// <summary>
        /// Throws ArgumentException if the argument is null.
        /// </summary>
        /// <param name="arg">The argument to check (e.g. Param1.PropertyA).</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        /// <param name="propertyName">The property name of the argument.</param>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void NotNull([ValidatedNotNull]object arg, string parameterName, string propertyName)
        {
            if (arg == null)
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentPropertyIsNull, propertyName);
                throw new ArgumentNullException(parameterName, message);
            }
        }

        /// <summary>
        /// Throws ArgumentException if the argument is less than zero.
        /// </summary>
        /// <param name="arg">The argument to check (e.g. Param1.PropertyA).</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        /// <param name="propertyName">The property name of the argument.</param>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void NotNegative(int arg, string parameterName, string propertyName)
        {
            if (arg < 0)
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentPropertyIsNegative, propertyName);
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Throws ArgumentException if the argument string is null or empty.
        /// </summary>
        /// <param name="arg">The argument to check (e.g. Param1.PropertyA).</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        /// <param name="propertyName">The property name of the argument.</param>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void NotNullOrEmpty([ValidatedNotNull]string arg, string parameterName, string propertyName)
        {
            NotNull(arg, parameterName, propertyName);

            if (String.IsNullOrEmpty(arg))
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentPropertyIsEmpty, propertyName);
                throw new ArgumentException(message, parameterName);
            }
        }

        /// <summary>
        /// Throws ArgumentException if the argument is null or is not the correct type.
        /// </summary>
        /// <param name="arg">The argument to check (e.g. Param1.PropertyA).</param>
        /// <param name="parameterName">The parameter name of the argument.</param>
        /// <param name="propertyName">The property name of the argument.</param>
        /// <typeparam name="T">The type of the expected argument.</typeparam>
        [DebuggerStepThrough]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification="This simplifies the caller.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "This is shared source. This method may not be called in the current assembly.")]
        public static void TypeOf<T>([ValidatedNotNull]object arg, string parameterName, string propertyName) where T : class
        {
            NotNull(arg, parameterName, propertyName);

            if (!(arg is T))
            {
                string message = String.Format(CultureInfo.CurrentCulture, ValidateArgStrings.Error_ArgumentPropertyNotTypeOf, propertyName, typeof(T).FullName);
                throw new ArgumentException(message, parameterName);
            }
        }
    }

    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class ValidateArgStrings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal ValidateArgStrings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
#if SILVERLIGHT
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.VisualStudio.TestPlatform.ObjectModel.ValidateArgStrings", typeof(ValidateArgStrings).GetTypeInfo().Assembly);
#else
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.VisualStudio.TestPlatform.ObjectModel.ValidateArgStrings", typeof(ValidateArgStrings).Assembly);
#endif
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified argument cannot be an empty string..
        /// </summary>
        internal static string Error_ArgumentIsEmpty {
            get {
                return ResourceManager.GetString("Error_ArgumentIsEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified argument cannot be negative..
        /// </summary>
        internal static string Error_ArgumentIsNegative {
            get {
                return ResourceManager.GetString("Error_ArgumentIsNegative", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified argument must have the following type: {0}..
        /// </summary>
        internal static string Error_ArgumentNotTypeOf {
            get {
                return ResourceManager.GetString("Error_ArgumentNotTypeOf", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified argument has the following property, which cannot be an empty string: {0}..
        /// </summary>
        internal static string Error_ArgumentPropertyIsEmpty {
            get {
                return ResourceManager.GetString("Error_ArgumentPropertyIsEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The  specified argument has the following property, which cannot be negative: {0}..
        /// </summary>
        internal static string Error_ArgumentPropertyIsNegative {
            get {
                return ResourceManager.GetString("Error_ArgumentPropertyIsNegative", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified argument has the following property, which cannot be null: {0}..
        /// </summary>
        internal static string Error_ArgumentPropertyIsNull {
            get {
                return ResourceManager.GetString("Error_ArgumentPropertyIsNull", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The specified argument has the following property: {0}. This property must have the following type: {1}..
        /// </summary>
        internal static string Error_ArgumentPropertyNotTypeOf {
            get {
                return ResourceManager.GetString("Error_ArgumentPropertyNotTypeOf", resourceCulture);
            }
        }
    }

    /// <summary>
    /// Secret attribute that tells the CA1062 validate arguments rule that this method validates the argument is not null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class ValidatedNotNullAttribute : Attribute
    {
    }
}

