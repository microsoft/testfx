// --------------------------------------------------------------------------------------------------------------------
// <copyright company="" file="TestablePlatformServiceProvider.cs">
//   
// </copyright>
// 
// --------------------------------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using PlatformServices.Interface.ObjectModel;
    using Moq;

    public class TestablePlatformServiceProvider : IPlatformServiceProvider
    {
        public TestablePlatformServiceProvider()
        {
            this.MockTestSourceValidator = new Mock<ITestSource>();
            this.MockFileOperations = new Mock<IFileOperations>();
            this.MockTraceLogger = new Mock<IAdapterTraceLogger>();
            this.MockTestSourceHost = new Mock<ITestSourceHost>();
            this.MockTestDeployment = new Mock<ITestDeployment>();
            this.MockSettingsProvider = new Mock<ISettingsProvider>();
            this.MockTestDataSource = new Mock<ITestDataSource>();
            this.MockTraceListener = new Mock<ITraceListener>();
            this.MockTraceListenerManager = new Mock<ITraceListenerManager>();
            
        }

        #region Mock Implementations

        public Mock<ITestSource> MockTestSourceValidator
        {
            get;
            set;
        }

        public Mock<IFileOperations> MockFileOperations
        {
            get;
            set;
        }

        public Mock<IAdapterTraceLogger> MockTraceLogger
        {
            get;
            set;
        }

        public Mock<ITestSourceHost> MockTestSourceHost
        {
            get;
            set;
        }

        public Mock<ITestDeployment> MockTestDeployment
        {
            get;
            set;
        }

        public Mock<ISettingsProvider> MockSettingsProvider
        {
            get;
            set;
        }

        public Mock<ITestDataSource> MockTestDataSource
        {
            get;
            set;
        }

        public Mock<ITraceListener> MockTraceListener
        {
            get;
            set;
        }

        public Mock<ITraceListenerManager> MockTraceListenerManager
        {
            get;
            set;
        }
        #endregion

        public ITestSource TestSource => this.MockTestSourceValidator.Object;

        public IFileOperations FileOperations => this.MockFileOperations.Object;

        public IAdapterTraceLogger AdapterTraceLogger => this.MockTraceLogger.Object;

        public ITestSourceHost TestSourceHost => this.MockTestSourceHost.Object;

        public ITestDeployment TestDeployment => this.MockTestDeployment.Object;

        public ISettingsProvider SettingsProvider => this.MockSettingsProvider.Object;

        public ITestDataSource TestDataSource => this.MockTestDataSource.Object;

        public ITestContext GetTestContext(ITestMethod testMethod, StringWriter writer, IDictionary<string, object> properties)
        {
            return new TestContextImplementation(testMethod, writer, properties);
        }

        public ITraceListener GetTraceListener(TextWriter textWriter)
        {
            return this.MockTraceListener.Object;
        }

        public ITraceListenerManager GetTraceListenerManager(TextWriter standardOutputWriter, TextWriter standardErrorWriter)
        {
            return this.MockTraceListenerManager.Object;
        }
    }
}
