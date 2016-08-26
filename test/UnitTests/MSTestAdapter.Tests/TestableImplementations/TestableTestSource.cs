using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.ProviderServices.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.Tests.TestableImplementations
{
    public class TestableTestSource : ITestSource
    {
        public bool AreValidSources(IEnumerable<string> sources)
        {
            return AreValidSourcesReturnValue;
        }

        internal bool AreValidSourcesReturnValue
        {
            get;
            set;
        }
    }
}
