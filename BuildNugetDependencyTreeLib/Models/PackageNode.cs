using System.Diagnostics;

namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary>
/// Represents a NuGet package produced by a project and the projects that consume it.
/// </summary>
[DebuggerDisplay("📦 {PackageId,nq} | Project: {ProjectName,nq} | Consumers: {Consumers.Count}")]
public class PackageNode
{
    /// <summary>
    /// Gets or sets the NuGet package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path to the project that produces this package.
    /// </summary>
    public string ProjectFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the project that produces this package.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of projects that consume this package.
    /// </summary>
    public List<ConsumerNode> Consumers { get; set; } = [];
}
