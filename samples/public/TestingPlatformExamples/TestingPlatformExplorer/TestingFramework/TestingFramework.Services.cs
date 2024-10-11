// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace TestingPlatformExplorer.TestingFramework;

public interface ITestOutputHelper
{
    void WriteLine(string message);
    void WriteErrorLine(string message);
}

internal sealed class TestOutputHelper : ITestOutputHelper
{
    public StringBuilder Output { get; set; } = new StringBuilder();
    public StringBuilder Error { get; set; } = new StringBuilder();

    public void WriteErrorLine(string message) => Error.AppendLine(message);

    public void WriteLine(string message) => Output.AppendLine(message);
}
