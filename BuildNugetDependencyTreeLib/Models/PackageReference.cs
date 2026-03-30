using System.Diagnostics;

namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary>
/// Represents a reference to a NuGet package with its version.
/// </summary>
[DebuggerDisplay("{PackageId,nq} | Version: {Version,nq}")]
public class PackageReference
{
    /// <summary>
    /// Gets or sets the NuGet package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the referenced package.
    /// </summary>
    public string? Version { get; set; }
}
