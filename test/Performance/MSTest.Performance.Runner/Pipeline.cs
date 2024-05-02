// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Performance.Runner;

internal static class Pipeline
{
    private static readonly AsyncLocal<Context> AsyncLocal = new() { Value = new Context() };

    public static TOutput FirstStep<TOutput>(Func<IStep<NoInputOutput, TOutput>> step, IDictionary<string, object> bagParameters)
       where TOutput : class, IPayload
    {
        AsyncLocal.Value!.Init(bagParameters);
        IStep<NoInputOutput, TOutput> stepInstance = step();
        WriteConsole($"Execute step: '{stepInstance.Description}'");
        return stepInstance.ExecuteAsync(NoInputOutput.NoInputOutputInstance, AsyncLocal.Value!).Result;
    }

    public static TOutput NextStep<TInput, TOutput>(this TInput input, Func<IStep<TInput, TOutput>> step)
        where TInput : class, IPayload
        where TOutput : class, IPayload
    {
        IStep<TInput, TOutput> stepInstance = step();
        WriteConsole($"Execute step: '{stepInstance.Description}'");
        return stepInstance.ExecuteAsync(input, AsyncLocal.Value!).Result;
    }

    private static void WriteConsole(string message)
    {
        ConsoleColor color = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = color;
        }
    }
}
