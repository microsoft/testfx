﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MSTest.Analyzers {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class CodeFixResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal CodeFixResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("MSTest.Analyzers.CodeFixResources", typeof(CodeFixResources).Assembly);
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
        ///   Looks up a localized string similar to Add &apos;[TestMethod]&apos;.
        /// </summary>
        internal static string AddTestMethodAttributeFix {
            get {
                return ResourceManager.GetString("AddTestMethodAttributeFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fix signature.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidCodeFix {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidCodeFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Change method accessibility to &apos;private&apos;.
        /// </summary>
        internal static string ChangeMethodAccessibilityToPrivateFix {
            get {
                return ResourceManager.GetString("ChangeMethodAccessibilityToPrivateFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fix actual/expected arguments order.
        /// </summary>
        internal static string FixAssertionArgsOrder {
            get {
                return ResourceManager.GetString("FixAssertionArgsOrder", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fix signature.
        /// </summary>
        internal static string FixSignatureCodeFix {
            get {
                return ResourceManager.GetString("FixSignatureCodeFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Add &apos;[TestClass]&apos;.
        /// </summary>
        internal static string PublicTypeShouldBeTestClassFix {
            get {
                return ResourceManager.GetString("PublicTypeShouldBeTestClassFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace TestInitialize method with constructor.
        /// </summary>
        internal static string ReplaceWithConstructorFix {
            get {
                return ResourceManager.GetString("ReplaceWithConstructorFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace the assertion with &apos;Assert.Fail()&apos;.
        /// </summary>
        internal static string ReplaceWithFailAssertionFix {
            get {
                return ResourceManager.GetString("ReplaceWithFailAssertionFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace &apos;Dispose&apos; with a TestCleanup method.
        /// </summary>
        internal static string ReplaceWithTestCleanuFix {
            get {
                return ResourceManager.GetString("ReplaceWithTestCleanuFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replace constructor with TestInitialize method.
        /// </summary>
        internal static string ReplaceWithTestInitializeFix {
            get {
                return ResourceManager.GetString("ReplaceWithTestInitializeFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fix test class signature.
        /// </summary>
        internal static string TestClassShouldBeValidFix {
            get {
                return ResourceManager.GetString("TestClassShouldBeValidFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fix test context.
        /// </summary>
        internal static string TestContextShouldBeValidFix {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fix test method signature.
        /// </summary>
        internal static string TestMethodShouldBeValidFix {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidFix", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Add &apos;[TestMethod]&apos;.
        /// </summary>
        internal static string UseAttributeOnTestMethodFix {
            get {
                return ResourceManager.GetString("UseAttributeOnTestMethodFix", resourceCulture);
            }
        }
    }
}
