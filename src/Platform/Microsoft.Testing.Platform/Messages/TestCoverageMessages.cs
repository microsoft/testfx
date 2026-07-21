// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Represents the type of code coverage being measured.
/// </summary>
public enum CoverageMetric
{
    /// <summary>
    /// Line coverage.
    /// </summary>
    Line,

    /// <summary>
    /// Branch coverage.
    /// </summary>
    Branch,

    /// <summary>
    /// Method coverage.
    /// </summary>
    Method,
}

/// <summary>
/// Represents a test coverage data message.
/// </summary>
/// <param name="displayName">The display name.</param>
/// <param name="description">The description.</param>
public abstract class TestCoverageData(string displayName, string? description) : IData
{
    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string? Description { get; } = description;

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestCoverageData { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
            .Append(", Properties = [");

        return builder.ToString();
    }
}

/// <summary>
/// Represents a test coverage data message for a specific module.
/// </summary>
public sealed class TestCoverageMessage : TestCoverageData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCoverageMessage"/> class.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="coveragePercentage">The coverage percentage.</param>
    /// <param name="metric">The coverage metric.</param>
    public TestCoverageMessage(string moduleName, double coveragePercentage, CoverageMetric metric)
        : base("Test coverage", "Reports code coverage data for a module.")
    {
        ModuleName = moduleName;
        CoveragePercentage = coveragePercentage;
        Metric = metric;
    }

    /// <summary>
    /// Gets the name of the module.
    /// </summary>
    public string ModuleName { get; }

    /// <summary>
    /// Gets the coverage percentage.
    /// </summary>
    public double CoveragePercentage { get; }

    /// <summary>
    /// Gets the coverage metric.
    /// </summary>
    public CoverageMetric Metric { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestCoverageMessage { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
            .Append(", ModuleName = ")
            .Append(ModuleName)
            .Append(", CoveragePercentage = ")
            .Append(CoveragePercentage.ToString("F1", CultureInfo.InvariantCulture))
            .Append(", Metric = ")
            .Append(Metric)
            .Append(", Properties = [");

        return builder.ToString();
    }
}

/// <summary>
/// Represents the statistical method used for the threshold comparison.
/// </summary>
public enum CoverageThresholdStatistic
{
    /// <summary>
    /// Minimum coverage across all modules.
    /// </summary>
    Minimum,

    /// <summary>
    /// Total (aggregate) coverage.
    /// </summary>
    Total,

    /// <summary>
    /// Average coverage across all modules.
    /// </summary>
    Average,
}

/// <summary>
/// Represents a test coverage threshold evaluation result.
/// </summary>
public sealed class TestCoverageThresholdMessage : TestCoverageData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestCoverageThresholdMessage"/> class.
    /// </summary>
    /// <param name="coveragePercentage">The actual coverage value as a percentage.</param>
    /// <param name="thresholdPercentage">The required coverage threshold as a percentage.</param>
    /// <param name="metric">The coverage metric.</param>
    /// <param name="statistic">The statistical method used for comparison.</param>
    public TestCoverageThresholdMessage(double coveragePercentage, double thresholdPercentage, CoverageMetric metric, CoverageThresholdStatistic statistic)
        : base("Test coverage threshold", "Reports the result of a coverage threshold evaluation.")
    {
        CoveragePercentage = coveragePercentage;
        ThresholdPercentage = thresholdPercentage;
        Metric = metric;
        Statistic = statistic;
    }

    /// <summary>
    /// Gets the actual coverage percentage.
    /// </summary>
    public double CoveragePercentage { get; }

    /// <summary>
    /// Gets the required threshold percentage.
    /// </summary>
    public double ThresholdPercentage { get; }

    /// <summary>
    /// Gets the coverage metric.
    /// </summary>
    public CoverageMetric Metric { get; }

    /// <summary>
    /// Gets a value indicating whether the coverage met the required threshold.
    /// </summary>
    public bool Passed => CoveragePercentage >= ThresholdPercentage;

    /// <summary>
    /// Gets the statistical method used for comparison.
    /// </summary>
    public CoverageThresholdStatistic Statistic { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestCoverageThresholdMessage { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
            .Append(", CoveragePercentage = ")
            .Append(CoveragePercentage.ToString("F1", CultureInfo.InvariantCulture))
            .Append(", ThresholdPercentage = ")
            .Append(ThresholdPercentage.ToString("F1", CultureInfo.InvariantCulture))
            .Append(", Metric = ")
            .Append(Metric)
            .Append(", Passed = ")
            .Append(Passed)
            .Append(", Statistic = ")
            .Append(Statistic)
            .Append(", Properties = [");

        return builder.ToString();
    }
}
