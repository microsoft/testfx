using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// States of the TestRun
    /// </summary>
    public enum TestRunState
    {
        /// <summary>
        /// Status is not known
        /// </summary>
        None = 0,

        /// <summary>
        /// The run is still being created.  No tests have started yet.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Tests are running.
        /// </summary>
        InProgress = 2,

        /// <summary>
        /// All tests have completed or been skipped.
        /// </summary>
        Completed = 3,

        /// <summary>
        /// Run is canceled and remaing tests have been aborted
        /// </summary>
        Canceled = 4,

         /// <summary>
        /// Run is aborted
        /// </summary>
        Aborted = 5
    }
}
