// ---------------------------------------------------------------------------
// <copyright file="ITestExecutorLauncher.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//  Interface implemented to launcher for test executor.  A class that
//  implements this interface will able to customize the launch of test executor.
// </summary>
// <owner>vikrama</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
#if !SILVERLIGHT
using System.ServiceModel.Channels;
using System.ServiceModel;
#endif
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.Security;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// Interface implemented to launcher for test executor.  A class that
    ///  implements this interface will able to customize the launch of test executor.
    /// </summary>
    public interface ITestExecutorLauncher
    {
        /// <summary>
        /// Launch process with given info. Thiss function will be used to launch process in case of HostType.
        /// </summary>
        /// <param name="exeFileName">Path of process to launch.</param>
        /// <param name="commandLineArguments">Command line arguments to pass to process.</param>
        /// <param name="workingDirectory">Working directory for process.</param>
        /// <param name="environmentVariables">Environment variables for process.</param>
        /// <returns>ProcessId of launched Process. 0 means not launched.</returns>
        int LaunchProcess(string exeFileName, string commandLineArguments, string workingDirectory, IDictionary<string, string> environmentVariables);

        /// <summary>
        /// Launch test executor for executing test. This extension point is to cutomize the launch of the 
        /// test executor. For e.g. launch in AppContainer mode. This method allows multi targeting for tests.
        /// </summary>
        /// <param name="frameWorkVersion">Framework for executing tests.</param>
        /// <param name="architecture">Processor architecture for executing tests.</param>
        /// <param name="environmentVariables">Environment variables for process.</param>
        /// <returns>ProcessId of launched Process. 0 means not launched.</returns>
        int LaunchTestExecutor(FrameworkVersion frameworkVersion, Architecture architecture, IDictionary<string, string> environmentVariables);

        /// <summary>
        /// This is used by platform to check whether relaunch of executor since last launch like if framework or 
        /// architecture is changes etc. This condition will be evaluated if object ITextExxecutorLauncher implementation
        /// remain same. For different objects, executor will be launched again.
        /// </summary>
        /// <param name="frameWorkVersion">New framework version to launch.</param>
        /// <param name="architecture">New architecture to launch.</param>
        /// <param name="environmentVariables">Environment variables for process.</param>
        /// <returns>True if test executor needs to be relaunched.</returns>
        bool RestartRequired(FrameworkVersion frameworkVersion, Architecture architecture, IDictionary<string, string> environmentVariables);

#if !SILVERLIGHT
        /// <summary>
        /// Customize the binding based on the launch if required (e.g. in AppContainer mode).
        /// Else just return the baseBinding.
        /// </summary>
        /// <param name="baseBinding">Base binding for communication with test executor.</param>
        /// <returns>Cutomized binding.</returns>
        [SecuritySafeCritical] 
        Binding GetCustomBinding(Binding baseBinding);
#endif

        /// <summary>
        /// Gets whether the launcher is debug launcher. Based on this parameter IsDebug variable will be set in RunContext.
        /// </summary>
        bool IsDebugLauncher { get; }
    }

    /// <summary>
    /// Interface implemented to launcher for test executor.  A class that
    ///  implements this interface will able to customize the launch of test executor and 
    ///  additionally specify channel relationship between test executor and service client.
    /// </summary>
    public interface ITestExecutorLauncher2 : ITestExecutorLauncher
    {
#if !SILVERLIGHT
        /// <summary>
        /// This is used by the platform to get the endpoint adress which will further be used to customize proxy.
        /// </summary>
        /// <returns>End point address for communication between platform and client.</returns>
        [SecuritySafeCritical] 
        EndpointAddress GetEndPointAddress();
#endif

        /// <summary>
        /// This will be used by the executor service client to handle channel faults appropriately
        /// </summary>
        bool IgnoreChannelFaults { get; }

        /// <summary>
        /// Gets target ID where the launcher and test project gets deployed. If mode is classic then returns null.
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// Register for exit event
        /// </summary>
        void RegisterForExitNotification(Action abortCallback);

        /// <summary>
        /// Deregister for exit event
        /// </summary>
        void DeregisterForExitNotification();


        /// <summary>
        /// Setup and Launch debug engine on target.
        /// </summary>
        /// <returns></returns>
        bool SetupAndLaunchDebugEngineOnTarget();

        /// <summary>
        /// Remote ip and port information to be used by debugger.
        /// </summary>
        string DebugRemoteString { get; }


    }

    public interface IUniversalTestExecutorLauncher : ITestExecutorLauncher
    {
        IEnumerable<string> LauncherArguments { get; set; }
    }
}
