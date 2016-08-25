// ---------------------------------------------------------------------------
// <copyright file="TestCase.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Stores information about a test case.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------

using System.Runtime.Serialization;
using System.Xml;
namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Stores information about a test settings.
    /// </summary>
    public abstract class TestRunSettings
    {
        private string name;

        #region Constructor

        /// <summary>
        /// Initializes with the name of the test case.
        /// </summary>
        /// <param name="name">The name of the test case.</param>
        /// <param name="executorUri">The Uri of the executor to use for running this test.</param>
        protected TestRunSettings(string name)
        {
            ValidateArg.NotNullOrEmpty(name, "name");

            this.name = name;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Name of the test settings.
        /// </summary>
        public string Name
        {
            get { return this.name; }
        }

        #endregion

#if !SILVERLIGHT
        /// <summary>
        /// Converter the setting to be an XmlElement.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1059:MembersShouldNotExposeCertainConcreteTypes", Justification = "XmlElement is required in the data collector.")]
        public abstract XmlElement ToXml();
#endif
    }
}
