// ---------------------------------------------------------------------------
// <copyright file="TestResult.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      Represents the result of a test case.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Represents the result of a test case.
    /// </summary>
    [DataContract]
    [KnownType(typeof(TestOutcome))]
    [KnownType(typeof(DateTimeOffset))]
    public sealed class TestResult : TestObject
    {
        #region Constructor

        /// <summary>
        /// Initializes with the test case the result is for.
        /// </summary>
        /// <param param name="testCase">The test case the result is for.</param>
        public TestResult(TestCase testCase)
        {
            if (testCase == null)
            {
                throw new ArgumentNullException("testCase");
            }

            TestCase = testCase;
            Messages = new Collection<TestResultMessage>();
            Attachments = new Collection<AttachmentSet>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Test case that this result is for.
        /// </summary>
        [DataMember]
        public TestCase TestCase { get; private set; }

        /// <summary>
        /// List of attachmentment sets for this TestResult.
        /// </summary>
        [DataMember]
        public Collection<AttachmentSet> Attachments { get; private set; }

        /// <summary>
        /// The outcome of the test case.
        /// </summary>
        public TestOutcome Outcome
        {
            get { return GetPropertyValue(TestResultProperties.Outcome, TestOutcome.None); }
            set { SetPropertyValue(TestResultProperties.Outcome, value); }
        }

        /// <summary>
        /// The exception message.
        /// </summary>
        public string ErrorMessage 
        {
            get { return GetPropertyValue<string>(TestResultProperties.ErrorMessage, null); }
            set { SetPropertyValue(TestResultProperties.ErrorMessage, value); }
        }


        /// <summary>
        /// The exception stack trace.
        /// </summary>
        public string ErrorStackTrace 
        {
            get { return GetPropertyValue<string>(TestResultProperties.ErrorStackTrace, null); }
            set { SetPropertyValue(TestResultProperties.ErrorStackTrace, value); }
        }

        /// <summary>
        /// TestResult Dispaly name. Used for Data Driven Test (i.e. Data Driven Test. E.g. InlineData in xUnit)
        /// </summary>
        public string DisplayName
        {
            get { return GetPropertyValue<string>(TestResultProperties.DisplayName, null); }
            set { SetPropertyValue(TestResultProperties.DisplayName, value); }
        }

        /// <summary>
        /// The test messages.
        /// </summary>
        [DataMember]
        public Collection<TestResultMessage> Messages { get; private set; }

        /// <summary>
        ///   Get/Set test result ComputerName.
        /// </summary>
        public string ComputerName
        {
            get { return GetPropertyValue(TestResultProperties.ComputerName, string.Empty); }
            set { SetPropertyValue(TestResultProperties.ComputerName, value); }
        }


        /// <summary>
        ///   Get/Set test result Duration.
        /// </summary>
        public TimeSpan Duration
        {
            get { return GetPropertyValue(TestResultProperties.Duration, TimeSpan.Zero); }
            set { SetPropertyValue(TestResultProperties.Duration, value); }
        }


        /// <summary>
        ///   Get/Set test result StartTime.
        /// </summary>
        public DateTimeOffset StartTime
        {
            get { return GetPropertyValue(TestResultProperties.StartTime, DateTimeOffset.Now); }
            set { SetPropertyValue(TestResultProperties.StartTime, value); }
        }


        /// <summary>
        ///   Get/Set test result EndTime.
        /// </summary>
        public DateTimeOffset EndTime
        {
            get { return GetPropertyValue(TestResultProperties.EndTime, DateTimeOffset.Now); }
            set { SetPropertyValue(TestResultProperties.EndTime, value); }
        }

        #endregion

        #region Methods

        
        

        /// <summary>
        /// The name of the test and the outcome.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder result = new StringBuilder();

            // Add the outcome of the test and the name of the test.
            
            result.AppendFormat(
                CultureInfo.CurrentUICulture,
                Resources.BasicTestResultFormat,                
                TestCase.DisplayName,
                TestOutcomeHelper.GetOutcomeString(Outcome));            

            // Add the error message and stack trace if this is a test failure.
            if (Outcome == TestOutcome.Failed)
            {
                // Add Error message.
                result.AppendLine();
                result.AppendFormat(
                    CultureInfo.CurrentUICulture,
                    Resources.TestFailureMessageFormat,
                    StringUtilities.PrepareForOutput(ErrorMessage));

                // Add stack trace if we have one.
                if (!StringUtilities.IsNullOrWhiteSpace(ErrorStackTrace))
                {
                    result.AppendLine();
                    result.AppendFormat(
                        CultureInfo.CurrentUICulture,
                        Resources.TestFailureStackTraceFormat,
                        StringUtilities.PrepareForOutput(ErrorStackTrace));
                }
            }

            // Add any text messages we have.
            if (Messages.Count > 0)
            {
                StringBuilder testMessages = new StringBuilder();
                foreach (TestResultMessage message in this.Messages)
                {
                    if (message != null
                            && !string.IsNullOrEmpty(message.Category)
                            && !string.IsNullOrEmpty(message.Text))
                    {
                        testMessages.AppendFormat(CultureInfo.CurrentUICulture,
                                                  Resources.TestResultMessageFormat,
                                                  message.Category,
                                                  message.Text);
                    }
                }

                result.AppendLine();
                result.AppendFormat(
                    CultureInfo.CurrentUICulture,
                    Resources.TestResultTextMessagesFormat,
                    StringUtilities.PrepareForOutput(testMessages.ToString()));
            }

            return result.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Represents the test result message.
    /// </summary>
    [DataContract]
    public class TestResultMessage
    {
        // Bugfix: 297759 Moving the category from the resources to the code
        // so that it works on machines which has eng OS & non-eng VS and vice versa. 
        //

        /// <summary>
        ///     Standard Output Message Category 
        /// </summary>
        public static readonly string StandardOutCategory = "StdOutMsgs";

        /// <summary>
        ///     Standard Error Message Category
        /// </summary>
        public static readonly string StandardErrorCategory = "StdErrMsgs";

        /// <summary>
        ///     Debug Trace Message Category
        /// </summary>
        public static readonly string DebugTraceCategory = "DbgTrcMsgs";

        /// <summary>
        ///     Additional Information Message Category
        /// </summary>
        public static readonly string AdditionalInfoCategory = "AdtnlInfo";

        /// <summary>
        /// Contructor
        /// </summary>
        /// <param name="category">Category of the message</param>
        /// <param name="text">Text of the message</param>
        public TestResultMessage(string category, string text)
        {
            this.Category = category;
            this.Text = text;
        }

        /// <summary>
        /// Gets the message category
        /// </summary>
        [DataMember]
        public string Category
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the message text
        /// </summary>
        [DataMember]
        public string Text
        {
            get;
            private set;
        }
    }


    /// <summary>
    /// Well-known TestResult properties
    /// </summary>
    public static class TestResultProperties
    {
#if SILVERLIGHT
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty DisplayName = TestProperty.Register("TestResult.DisplayName", "TestResult Display Name", typeof(string), TestPropertyAttributes.Hidden, typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty ComputerName = TestProperty.Register("TestResult.ComputerName", "Computer Name", string.Empty, string.Empty, typeof(string), ValidateComputerName, TestPropertyAttributes.None, typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty Outcome = TestProperty.Register("TestResult.Outcome", "Outcome", string.Empty, string.Empty, typeof(TestOutcome), ValidateOutcome, TestPropertyAttributes.None, typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty Duration = TestProperty.Register("TestResult.Duration", "Duration", typeof(TimeSpan), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty StartTime = TestProperty.Register("TestResult.StartTime", "Start Time", typeof(DateTimeOffset), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty EndTime = TestProperty.Register("TestResult.EndTime", "End Time", typeof(DateTimeOffset), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty ErrorMessage = TestProperty.Register("TestResult.ErrorMessage", "Error Message", typeof(string), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty ErrorStackTrace = TestProperty.Register("TestResult.ErrorStackTrace", "Error Stack Trace", typeof(string), typeof(TestResult));
#else
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty DisplayName = TestProperty.Register("TestResult.DisplayName", Resources.TestResultPropertyDisplayNameLabel, typeof(string), TestPropertyAttributes.Hidden, typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty ComputerName = TestProperty.Register("TestResult.ComputerName", Resources.TestResultPropertyComputerNameLabel, string.Empty, string.Empty, typeof(string), ValidateComputerName, TestPropertyAttributes.None, typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty Outcome = TestProperty.Register("TestResult.Outcome", Resources.TestResultPropertyOutcomeLabel, string.Empty, string.Empty, typeof(TestOutcome), ValidateOutcome, TestPropertyAttributes.None, typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty Duration = TestProperty.Register("TestResult.Duration", Resources.TestResultPropertyDurationLabel, typeof(TimeSpan), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty StartTime = TestProperty.Register("TestResult.StartTime", Resources.TestResultPropertyStartTimeLabel, typeof(DateTimeOffset), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty EndTime = TestProperty.Register("TestResult.EndTime", Resources.TestResultPropertyEndTimeLabel, typeof(DateTimeOffset), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty ErrorMessage = TestProperty.Register("TestResult.ErrorMessage", Resources.TestResultPropertyErrorMessageLabel, typeof(string), typeof(TestResult));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly TestProperty ErrorStackTrace = TestProperty.Register("TestResult.ErrorStackTrace", Resources.TestResultPropertyErrorStackTraceLabel, typeof(string), typeof(TestResult));
#endif
        private static bool ValidateComputerName(object value)
        {
            return !(StringUtilities.IsNullOrWhiteSpace((string)value));
        }

        private static bool ValidateOutcome(object value)
        {
            return (TestOutcome)value <= TestOutcome.NotFound && (TestOutcome)value >= TestOutcome.None;
        }
    }
}
