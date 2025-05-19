// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/// <summary>
/// Discovers and runs tests using the MSTest attributes, so we can run tests even when we completely break or delete the real MSTest engine.
/// </summary>
internal sealed class MinimalTestRunner
{
    public static async Task<int> RunAllAsync(string? testNameContainsFilter = null)
    {
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        IEnumerable<Type> classes = Assembly.GetExecutingAssembly().GetTypes().Where(c => c.IsPublic);
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
        object[][] emptyRow = [[]];

        int total = 0;
        int failed = 0;
        int passed = 0;
        foreach (Type? c in classes)
        {
            IList<CustomAttributeData> attributes = c.GetCustomAttributesData();

            if (!attributes.Any(a => a.AttributeType == typeof(TestClassAttribute)))
            {
                continue;
            }

            if (attributes.Any(a => a.AttributeType == typeof(IgnoreAttribute)))
            {
                Console.WriteLine($"Class {c.Name} is ignored.");
                continue;
            }

#pragma warning disable IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
            foreach (MethodInfo m in c.GetMethods())
            {
                if (!string.IsNullOrWhiteSpace(testNameContainsFilter))
                {
#pragma warning disable CA1304 // Specify CultureInfo
#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
#pragma warning disable CA1311 // Specify a culture or use an invariant version
                    if (!m.Name!.ToLower().Contains(testNameContainsFilter.ToLower()))
                    {
                        continue;
                    }
#pragma warning restore CA1311 // Specify a culture or use an invariant version
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
#pragma warning restore CA1304 // Specify CultureInfo
                }

                IList<CustomAttributeData> methodAttributes = m.GetCustomAttributesData();
                if (!methodAttributes.Any(a => a.AttributeType == typeof(TestMethodAttribute)))
                {
                    continue;
                }

                if (methodAttributes.Any(a => a.AttributeType == typeof(IgnoreAttribute)))
                {
                    Console.WriteLine($"Method {c.Name} is ignored.");
                    continue;
                }

                object?[][]? rows = null;
                if (methodAttributes.Any(a => a.AttributeType == typeof(DataRowAttribute)))
                {
                    rows = [.. methodAttributes
                        .Where(a => a.AttributeType == typeof(DataRowAttribute))
                        .SelectMany(a => a.ConstructorArguments.Select(arg =>
                        {
                            // An object that represents the value of the argument or element, or a generic ReadOnlyCollection<T> of CustomAttributeTypedArgument objects that represent the values of an array-type argument.
                            // https://learn.microsoft.com/en-us/dotnet/api/system.reflection.customattributetypedargument.value?view=net-8.0#property-value
#pragma warning disable IDE0046 // Convert to conditional expression
                            if (arg.Value is IReadOnlyCollection<CustomAttributeTypedArgument> argumentCollection)
                            {
                                return argumentCollection.Select(argv => argv.Value).ToArray();
                            }
                            else
                            {
                                return [arg.Value];
                            }
#pragma warning restore IDE0046 // Convert to conditional expression
                        }))];
                }

                foreach (object?[]? row in rows ?? emptyRow)
                {
                    ConsoleColor fg = Console.ForegroundColor;
                    try
                    {
                        total++;
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                        object? classInstance = Activator.CreateInstance(c);
#pragma warning restore IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
                        object? result = m.Invoke(classInstance, row);
                        if (result is Task task)
                        {
                            await task;
                        }
                        else if (result is ValueTask valueTask)
                        {
                            await valueTask;
                        }

                        passed++;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Passed {c.Name}.{m.Name}");
                    }
                    catch (TargetInvocationException ex)
                    {
                        failed++;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Failed {c.Name}.{m.Name}:\n{ex.InnerException}\n{ex.InnerException!.StackTrace}\n");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Failed {c.Name}.{m.Name}:\n{ex}\n{ex.StackTrace}\n");
                    }
                    finally
                    {
                        Console.ForegroundColor = fg;
                    }
                }
            }
#pragma warning restore IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
        }

        Console.WriteLine($"{(failed != 0 ? "failed" : "passed")}! - failed: {failed}, passed: {passed}, total: {total}");

        return failed == 0 ? 0 : 1;
    }
}
