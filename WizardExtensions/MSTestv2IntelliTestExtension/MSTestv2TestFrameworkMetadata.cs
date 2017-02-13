// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestv2IntelliTestExtension
{
    using Microsoft.ExtendedReflection.Metadata.Names;
    using Microsoft.ExtendedReflection.Utilities.Safe.Diagnostics;

    /// <summary>
    /// The metadata.
    /// </summary>
    internal static class MSTestv2TestFrameworkMetadata
    {
        internal static readonly ShortAssemblyName AssemblyName = ShortAssemblyName.FromName("Microsoft.VisualStudio.TestPlatform.TestFramework"); 

         internal static readonly string RootNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";

        public static TypeName AttributeName(string name)
        {
            SafeDebug.AssumeNotNullOrEmpty(name, "name");

            return TypeDefinitionName.FromName(
                AssemblyName,
                -1,
                false,
                RootNamespace,
                name + "Attribute").SelfInstantiation;
        }

        private static TypeName TypeName(string name)
        {
            SafeDebug.AssumeNotNullOrEmpty(name, "name");
            return TypeDefinitionName.FromName(
                AssemblyName,
                -1,
                false,
                RootNamespace,
                name).SelfInstantiation;
        }

        public static readonly TypeName AssertType = MSTestv2TestFrameworkMetadata.TypeName("Assert");
        public static readonly TypeDefinitionName AssertTypeDefinition = AssertType.Definition;
        public static readonly TypeName CollectionAssertType = MSTestv2TestFrameworkMetadata.TypeName("CollectionAssert");
        public static readonly TypeDefinitionName CollectionAssertTypeDefinition = CollectionAssertType.Definition;

    }
}
