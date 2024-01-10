// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1416 // Validate platform compatibility

using System.Management;

namespace MSTest.Performance.Runner.Steps;

public class WindowsProcessWatcher : ManagementEventWatcher
{
    public WindowsProcessWatcher(string processName)
    {
        Query.QueryLanguage = "WQL";
        Query.QueryString = $"""
SELECT *
FROM __InstanceOperationEvent WITHIN 1
WHERE TargetInstance ISA 'Win32_Process' and TargetInstance.Name = '{processName}'
""";
        EventArrived += new EventArrivedEventHandler(Watcher_EventArrived);
    }

    public event EventHandler<ManagementBaseObject>? ProcessCreated;

    public event EventHandler<ManagementBaseObject>? ProcessDeleted;

    public event EventHandler<ManagementBaseObject>? ProcessModified;

    private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
    {
        string eventType = e.NewEvent.ClassPath.ClassName;
        switch (eventType)
        {
            case "__InstanceCreationEvent":
                ProcessCreated?.Invoke(this, (ManagementBaseObject)e.NewEvent["TargetInstance"]);
                break;
            case "__InstanceDeletionEvent":
                ProcessDeleted?.Invoke(this, (ManagementBaseObject)e.NewEvent["TargetInstance"]);
                break;
            case "__InstanceModificationEvent":
                ProcessModified?.Invoke(this, (ManagementBaseObject)e.NewEvent["TargetInstance"]);
                break;
        }
    }
}
