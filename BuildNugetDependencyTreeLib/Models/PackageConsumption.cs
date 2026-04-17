using System.Diagnostics;

namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary>
/// Represents a package consumption with its version information.
/// </summary>
[DebuggerDisplay("{PackageId,nq} | Version: {Version,nq}")]
public class PackageConsumption
{
    /// <summary>
    /// Gets or sets the NuGet package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the consumed package.
    /// </summary>
    public string? Version { get; set; }

    /// <summary />
    public string? ExpectedVersion { get; set; }
}
