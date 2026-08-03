// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace ReliableTestSuite;

/// <summary>
/// A minimal "system under test": exports orders to a CSV file at a caller-supplied path.
/// Crucially, the path is a parameter - the exporter owns no ambient/global state, so two
/// callers writing to two different paths never interfere. That property is what lets the
/// tests below run in parallel with no lock at all.
///
/// Note the InvariantCulture formatting of the decimal below. Owning no ambient state also means
/// not depending on the ambient CULTURE: with a default '$"{order.Total}"' interpolation the
/// output would change shape on a comma-decimal machine (42.00 -> 42,00), corrupting the CSV.
/// Determinism is engineered here too, not assumed.
/// </summary>
public static class OrderExporter
{
    public static void ExportCsv(IEnumerable<Order> orders, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("Id,Customer,Total");
        foreach (Order order in orders)
        {
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{order.Id},{order.Customer},{order.Total}"));
        }
    }
}

public sealed record Order(int Id, string Customer, decimal Total);
