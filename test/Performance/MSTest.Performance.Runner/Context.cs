// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Performance.Runner;

internal class Context : IContext, IDisposable
{
    private List<IDisposable> _disposables = new();

    public IDictionary<string, object> Properties { get; private set; } = new Dictionary<string, object>();

    public void Init(IDictionary<string, object> properties)
    {
        _disposables = new();
        Properties = properties;
    }

    public void AddDisposable(IDisposable disposable) => _disposables.Add(disposable);

    public void Dispose()
    {
        foreach (IDisposable item in _disposables)
        {
            Console.WriteLine($"Disposing: '{item.GetType()}'");
            item.Dispose();
        }
    }
}
