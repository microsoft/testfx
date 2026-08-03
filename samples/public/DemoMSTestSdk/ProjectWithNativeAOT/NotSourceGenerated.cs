// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This file intentionally contains no compiled code. MSTest.SourceGeneration (as of this writing, an
// early-preview 2.0.0-alpha package) cannot discover every shape MSTest allows, even though the shapes
// below are all valid for the normal, reflection-based MSTest.TestAdapter. When one of these shapes is
// used under source generation, the test is silently skipped at discovery time - it will not fail, it
// simply will not run - unless a dedicated analyzer catches it at build time. Prefer analyzer-covered
// cases (flagged below) since the compiler will tell you immediately; the rest require manual awareness
// or a `<TrimmerRootAssembly Include="$(AssemblyName)" />` fallback (see docs/source-generator/design.md
// in the microsoft/testfx repository for the full rationale).
//
// 1. A test class made a "test class" only by INHERITING [TestClass] from a base class.
//    Analyzer-covered: MSTEST0069 (warning, shipped by MSTest.SourceGeneration).
//
//        [TestClass]
//        public abstract class BaseTests { }
//
//        // MSTEST0069: DerivedTests is discoverable by the reflection-based adapter (it inherits
//        // [TestClass]) but the source generator only looks for the attribute applied directly,
//        // via ForAttributeWithMetadataName, so DerivedTests' tests never run under source gen.
//        public class DerivedTests : BaseTests
//        {
//            [TestMethod]
//            public void ThisNeverRuns() { }
//        }
//
// 2. Open generic test classes and generic test methods.
//    Analyzer-covered: AOTSG0002 (generic test class), AOTSG0004 (generic test method).
//
//        [TestClass]
//        public class GenericTests<T>
//        {
//            [TestMethod]
//            public void GenericMethod<TValue>(TValue value) { }
//        }
//
// 3. `ref` / `out` / `in` / `ref readonly` test method parameters.
//    Analyzer-covered: AOTSG0005 (by-ref parameter).
//
//        [TestClass]
//        public class ByRefTests
//        {
//            [TestMethod]
//            public void CannotBindByRef(ref int value) { }
//        }
//
// 4. `file`-local, private, or protected nested test classes, and static test classes.
//    Analyzer-covered: AOTSG0001 (static test class), AOTSG0003 (inaccessible test class).
//
//        file class FileLocalTests
//        {
//            [TestMethod]
//            public void CannotBeReferencedFromGeneratedCode() { }
//        }
//
// Bottom line for the reader: these are narrow, mostly-uncommon shapes, and most are caught by an
// analyzer at build time rather than failing silently at run time. But "silently skipped" is still the
// failure mode for the couple of cases without an analyzer (case 1's *sibling* shapes, e.g. a
// non-nested private test class in some contexts), so treat source generation as an opt-in performance
// path validated by running your suite once with source generation enabled and comparing test counts,
// not as a drop-in replacement you can assume is 100% compatible on day one.
