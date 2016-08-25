// ---------------------------------------------------------------------------
// <copyright file="TraitCollection.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Class that holds collection of traits
    /// </summary>
#if !SILVERLIGHT
    [Serializable]
#endif
    public class TraitCollection: IEnumerable<Trait>
    {
        private const string TraitPropertyId = "TestObject.Traits";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        private static readonly TestProperty TraitsProperty = TestProperty.Register(
            TraitPropertyId,
            Resources.TestCasePropertyTraitsLabel,
            typeof(KeyValuePair<string, string>[]),
#pragma warning disable 618
            TestPropertyAttributes.Hidden | TestPropertyAttributes.Trait,
#pragma warning restore 618
            typeof(TestObject));

#if !SILVERLIGHT
        [NonSerialized]
#endif
        private TestObject testObject;

        internal TraitCollection(TestObject testObject)
        {
            ValidateArg.NotNull(testObject, "testObject");

            this.testObject = testObject;
        }

        public void Add(Trait trait)
        {
            ValidateArg.NotNull(trait, "trait");

            this.AddRange(new[] { trait });
        }

        public void Add(string name, string value)
        {
            ValidateArg.NotNull(name, "name");

            this.Add(new Trait(name, value));
        }

        public void AddRange(IEnumerable<Trait> traits)
        {
            ValidateArg.NotNull(traits, "traits");

            var existingTraits = this.GetTraits();
            this.Add(existingTraits, traits);
        }

        public IEnumerator<Trait> GetEnumerator()
        {
            var enumerable = this.GetTraits();
            return enumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private IEnumerable<Trait> GetTraits()
        {
            if (!this.testObject.Properties.Contains(TraitsProperty))
            {
                yield break;
            }

            var traits = this.testObject.GetPropertyValue<KeyValuePair<string, string>[]>(TraitsProperty, Enumerable.Empty<KeyValuePair<string, string>>().ToArray());

            foreach (var trait in traits)
            {
                yield return new Trait(trait);
            }
        }

        private void Add(IEnumerable<Trait> traits, IEnumerable<Trait> newTraits)
        {
            var newValue = traits.Union(newTraits);
            var newPairs = newValue.Select(t => new KeyValuePair<string, string>(t.Name, t.Value)).ToArray();
            this.testObject.SetPropertyValue<KeyValuePair<string, string>[]>(TraitsProperty, newPairs);
        }
    }
}
