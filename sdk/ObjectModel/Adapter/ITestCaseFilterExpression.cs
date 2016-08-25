// ---------------------------------------------------------------------------
// <copyright file="ITestCaseFilterExpression.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//   Expression for filtering test cases. 
// </summary>
// <owner>vikrama</owner> 
// ---------------------------------------------------------------------------

using System;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    /// <summary>
    /// It represents expression for filtering test cases. 
    /// </summary>
    public interface ITestCaseFilterExpression
    {
        /// <summary>
        /// Gets original string for test case filter.
        /// </summary>
        string TestCaseFilterValue { get; }

        /// <summary>
        /// Matched test case with test case filtering criteria.
        /// </summary>
        bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider);
    }
}