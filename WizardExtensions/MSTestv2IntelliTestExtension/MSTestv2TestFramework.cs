// Copyright (c) Microsoft. All rights reserved.

namespace MSTestv2IntelliTestExtension
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ExtendedReflection.Asserts;
    using Microsoft.ExtendedReflection.Collections;
    using Microsoft.ExtendedReflection.Metadata;
    using Microsoft.ExtendedReflection.Metadata.Names;
    using Microsoft.ExtendedReflection.Monitoring;
    using Microsoft.ExtendedReflection.Utilities.Safe;
    using Microsoft.Pex.Engine;
    using Microsoft.Pex.Engine.ComponentModel;
    using Microsoft.Pex.Engine.TestFrameworks;
    using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;

    /// <summary>
    /// MSTestv2 test framework
    /// </summary>
    [Serializable]
    public class MSTestv2TestFramework : AttributeBasedTestFrameworkBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestv2TestFramework"/> class.
        /// </summary>
        /// <param name="host">
        /// </param>
        public MSTestv2TestFramework(IPexComponent host) : base(host)
        {
        }

        static readonly bool net2process = typeof(object).Assembly.GetName().Version.Major == 2;

        /// <summary>
        /// identify of the test framework
        /// </summary>
        /// <value></value>
        public override string Name
        {
            get { return "MSTestv2"; }
        }

        /// <summary>
        /// Gets the assembly name of the framework main's assembly. This name is used
        /// to automatically discover test frameworks, based the assembly references
        /// </summary>
        /// <value></value>
        public override ShortAssemblyName AssemblyName
        {
            get { return MSTestv2TestFrameworkMetadata.AssemblyName; }
        }

        /// <summary>
        /// Gets the root namespace.
        /// </summary>
        /// <value>The root namespace.</value>
        public override string RootNamespace
        {
            get { return MSTestv2TestFrameworkMetadata.RootNamespace; }
        }

        /// <summary>
        /// The adapter and test framework references.
        /// </summary>
        public override ICountable<ShortReferenceAssemblyName> References
        {
            get
            {
                return Indexable.Two(new ShortReferenceAssemblyName(ShortAssemblyName.FromName("MSTest.TestAdapter"), "1.1.10-rc2", AssemblyReferenceType.NugetReference),
                    new ShortReferenceAssemblyName(ShortAssemblyName.FromName("MSTest.TestFramework"), "1.0.8-rc2", AssemblyReferenceType.NugetReference));
            }
        }

        /// <summary>
        /// Gets a value indicating whether
        /// partial test classes
        /// </summary>
        /// <value></value>
        public override bool SupportsPartialClasses
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating if the bitness is supported
        /// </summary>
        /// <param name="bitness"></param>
        /// <returns></returns>
        public override bool SupportsProjectBitness(Bitness bitness)
        {
            SafeDebug.Assume(bitness != Bitness.Unsupported, "bitness != Bitness.Unsupported");
            if (net2process)
                return bitness == Bitness.AnyCpu || bitness == Bitness.x86;
            else
                return true;
        }

        /// <summary>
        /// The _expected exception attribute.
        /// </summary>
        [NonSerialized]
        TypeName _expectedExceptionAttribute;

        /// <summary>
        /// Gets the ExpectedException attribute.
        /// </summary>
        /// <value>The expected exception attribute.</value>
        public override TypeName ExpectedExceptionAttribute
        {
            get
            {
                if (this._expectedExceptionAttribute == null)
                    this._expectedExceptionAttribute = MSTestv2TestFrameworkMetadata.AttributeName("ExpectedExceptionAttribute");
                return this._expectedExceptionAttribute;
            }
        }

        [NonSerialized]
        TypeName _assemblySetUpAttribute;
        /// <summary>
        /// Gets the assembly set up attribute.
        /// </summary>
        /// <value>The assembly set up attribute.</value>
        public TypeName AssemblySetUpAttribute
        {
            get
            {
                if (this._assemblySetUpAttribute == null)
                    this._assemblySetUpAttribute = MSTestv2TestFrameworkMetadata.AttributeName("AssemblyInitializeAttribute");
                return this._assemblySetUpAttribute;
            }
        }

        [NonSerialized]
        TypeName _assemblyTearDownAttribute;
        /// <summary>
        /// Gets the assembly tear down attribute.
        /// </summary>
        /// <value>The assembly tear down attribute.</value>
        public TypeName AssemblyTearDownAttribute
        {
            get
            {
                if (this._assemblyTearDownAttribute == null)
                    this._assemblyTearDownAttribute = MSTestv2TestFrameworkMetadata.AttributeName("AssemblyCleanupAttribute");
                return this._assemblyTearDownAttribute;
            }
        }

        /// <summary>
        /// Tries to get the assembly set up tear down attribute.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="setUp">The set up.</param>
        /// <param name="tearDown">The tear down.</param>
        /// <returns></returns>
        public override bool TryGetAssemblySetupTeardownMethods(
            AssemblyEx assembly,
            out Method setUp,
            out Method tearDown)
        {
            // <preconditions>
            SafeDebug.AssumeNotNull((object)assembly, "assembly");
            // </preconditions>
            setUp = tearDown = null;

            // look for [TestClass] types
            foreach (var typeDefinition in assembly.TypeDefinitions)
            {
                if (typeDefinition.IsVisible(VisibilityContext.Exported))
                {
                    if (typeDefinition.IsEnumType ||
                        typeDefinition.GenericTypeParameters.Length > 0)
                        continue;

                    if (!this.IsFixture(typeDefinition))
                        continue;

                    // looking for assembly setup/teardown methods
                    foreach (var methodDefinition in typeDefinition.DeclaredStaticMethods)
                        if (methodDefinition.IsVisible(VisibilityContext.Exported) &&
                            methodDefinition.GenericMethodParameters.Length == 0)
                        {
                            if (AttributeHelper.IsDefined(methodDefinition, this.AssemblySetUpAttribute, true))
                                setUp = methodDefinition.Instantiate(TypeEx.NoTypes, TypeEx.NoTypes);
                            if (AttributeHelper.IsDefined(methodDefinition, this.AssemblyTearDownAttribute, true))
                                tearDown = methodDefinition.Instantiate(TypeEx.NoTypes, TypeEx.NoTypes);

                            // nothing else to look for
                            if (setUp != null && tearDown != null)
                                break;
                        }

                    // nothing else to look for
                    if (setUp != null && tearDown != null)
                        break;
                }
            }

            return setUp != null || tearDown != null;
        }

        /// <summary>
        /// Gets a value indicating whether fixture set up tear down are instance methods.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if [fixture set up tear down instance]; otherwise, <c>false</c>.
        /// </value>
        public override bool FixtureSetupTeardownInstance
        {
            get { return false; }
        }

        [NonSerialized]
        TypeName _fixtureAttribute;
        /// <summary>
        /// Gets the name of the fixture attribute.
        /// </summary>
        /// <value>The fixture attribute.</value>
        public override TypeName FixtureAttribute
        {
            get
            {
                if (this._fixtureAttribute == null)
                    this._fixtureAttribute = MSTestv2TestFrameworkMetadata.AttributeName("TestClassAttribute");
                return this._fixtureAttribute;
            }
        }

        [NonSerialized]
        TypeName _fixtureSetUpAttribute;
        /// <summary>
        /// Gets the name of the fixture setup attribute
        /// </summary>
        /// <value>The fixture set up attribute.</value>
        public override TypeName FixtureSetupAttribute
        {
            get
            {
                if (this._fixtureSetUpAttribute == null)
                    this._fixtureSetUpAttribute = MSTestv2TestFrameworkMetadata.AttributeName("ClassInitializeAttribute");
                return this._fixtureSetUpAttribute;
            }
        }

        [NonSerialized]
        TypeName _fixtureTearDownAttribute;
        /// <summary>
        /// Gets the name of the fixture teardown attribute
        /// </summary>
        /// <value>The fixture tear down attribute.</value>
        public override TypeName FixtureTeardownAttribute
        {
            get
            {
                if (this._fixtureTearDownAttribute == null)
                    this._fixtureTearDownAttribute = MSTestv2TestFrameworkMetadata.AttributeName("ClassCleanupAttribute");
                return this._fixtureTearDownAttribute;
            }
        }

        [NonSerialized]
        TypeName _setUpAttribute;
        /// <summary>
        /// Gets the name of the test setup attribute.
        /// </summary>
        /// <value>The set up attribute.</value>
        public override TypeName SetupAttribute
        {
            get
            {
                if (this._setUpAttribute == null)
                    this._setUpAttribute = MSTestv2TestFrameworkMetadata.AttributeName("TestInitializeAttribute");
                return this._setUpAttribute;
            }
        }

        [NonSerialized]
        TypeName _testAttribute;
        /// <summary>
        /// Gets the name of the test attribute.
        /// </summary>
        /// <value>The set up attribute.</value>
        public override TypeName TestAttribute
        {
            get
            {
                if (this._testAttribute == null)
                    this._testAttribute = MSTestv2TestFrameworkMetadata.AttributeName("TestMethodAttribute");
                return this._testAttribute;
            }
        }

        [NonSerialized]
        TypeName _tearDownAttribute;
        /// <summary>
        /// Gets the name of the test teardown attribute.
        /// </summary>
        /// <value>The tear down attribute.</value>
        public override TypeName TeardownAttribute
        {
            get
            {
                if (this._tearDownAttribute == null)
                    this._tearDownAttribute = MSTestv2TestFrameworkMetadata.AttributeName("TestCleanupAttribute");
                return this._tearDownAttribute;
            }
        }

        [NonSerialized]
        TypeName _ignoreAttribute;
        /// <summary>
        /// Gets the ignore attribute.
        /// </summary>
        /// <value>The ignore attribute.</value>
        public override TypeName IgnoreAttribute
        {
            get
            {
                if (this._ignoreAttribute == null)
                    this._ignoreAttribute = MSTestv2TestFrameworkMetadata.AttributeName("IgnoreAttribute");
                return _ignoreAttribute;
            }
        }

        /// <summary>
        /// Whether the ignore attribute constructor takes a message as its first argument.
        /// </summary>
        /// <value></value>
        protected override bool HasIgnoreAttributeMessage
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the ignore message property.
        /// </summary>
        /// <value>The ignore message property.</value>
        protected override string IgnoreMessageProperty
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the expected exception property name.
        /// </summary>
        /// <value>The expected exception property.</value>
        protected override string ExpectedExceptionProperty
        {
            get { return "ExceptionType"; }
        }

        /// <summary>
        /// Gets a list of attribute that should be duplicated from the
        /// pex test to the parameterized test
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<TypeName> GetSatelliteAttributeTypes()
        {
            return Indexable.Array<TypeName>(
                    MSTestv2TestFrameworkMetadata.AttributeName("DeploymentItemAttribute"),
                    MSTestv2TestFrameworkMetadata.AttributeName("OwnerAttribute"),
                    MSTestv2TestFrameworkMetadata.AttributeName("PriorityAttribute"),
                    MSTestv2TestFrameworkMetadata.AttributeName("TestPropertyAttribute"),
                    MSTestv2TestFrameworkMetadata.AttributeName("TimeoutAttribute"),
                    MSTestv2TestFrameworkMetadata.AttributeName("WorkItemAttribute"),
                    MSTestv2TestFrameworkMetadata.AttributeName("CssIterationAttribute"),
                    MSTestv2TestFrameworkMetadata.AttributeName("CssProjectStructureAttributeCtor")
                );
        }

        /// <summary>
        /// Tries to query the categories.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="names">The names.</param>
        /// <returns></returns>
        protected override bool TryGetCategories(
            ICustomAttributeProviderEx element,
            out IEnumerable<string> names)
        {
            names = null;
            return false;
        }

        [NonSerialized]
        TypeName _assertionExceptionType;
        /// <summary>
        /// Gets the type of the assertion exception.
        /// </summary>
        /// <value>The type of the assertion exception.</value>
        public override TypeName AssertionExceptionType
        {
            get
            {
                if (this._assertionExceptionType == null)
                    this._assertionExceptionType = MSTestv2TestFrameworkMetadata.AttributeName("AssertFailedException");
                return this._assertionExceptionType;
            }
        }
        /// <summary>
        /// Gets a value that indicates if this framework support static unit tests
        /// </summary>
        public override bool SupportsStaticTestMethods
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the assert method filters.
        /// </summary>
        public override IIndexable<IAssertMethodFilter> AssertMethodFilters
        {
            get
            {
                return Indexable.One<IAssertMethodFilter>(MSTestv2AssertMethodFilter.Instance);
            }
        }
    }
}
