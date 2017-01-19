// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Base class for the "Category" attribute
    /// </summary>
    /// <remarks>
    /// The reason for this attribute is to let the users create their own implementation of test categories.
    /// - test framework (discovery, etc) deals with TestCategoryBaseAttribute.
    /// - The reason that TestCategories property is a collection rather than a string, 
    ///   is to give more flexibility to the user. For instance the implementation may be based on enums for which the values can be OR'ed
    ///   in which case it makes sense to have single attribute rather than multiple ones on the same test.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
    public abstract class TestCategoryBaseAttribute : Attribute
    {
        /// <summary>
        /// Applies the category to the test. The strings returned by TestCategories
        /// are used with the /category command to filter tests
        /// </summary>
        protected TestCategoryBaseAttribute()
        {
        }

        /// <summary>
        /// The test category that has been applied to the test.
        /// </summary>
        public abstract IList<string> TestCategories
        {
            get;
        }
    }

    /// <summary>
    /// TestCategory attribute; used to specify the category of a unit test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
    [SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments", Justification = "The TestCategories accessor propety exposes the testCategory argument.")]
    public sealed class TestCategoryAttribute : TestCategoryBaseAttribute
    {
        /// <summary>
        /// Applies the category to the test.
        /// </summary>
        /// <param name="testCategory">
        /// The test Category.
        /// </param>
        public TestCategoryAttribute(string testCategory)
        {
            List<string> categories = new List<string>(1);
            categories.Add(testCategory);
            this.testCategories = categories;
        }

        /// <summary>
        /// The test categories that has been applied to the test.
        /// </summary>
        public override IList<String> TestCategories
        {
            get { return this.testCategories; }
        }

        private IList<string> testCategories;
    }
}