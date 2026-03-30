using System.Diagnostics;

namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary>
/// Represents a dependency tree showing which projects consume packages produced by other projects.
/// </summary>
[DebuggerDisplay("Packages: {PackageNodes.Count} | Total Projects: {TotalProjects} | With Dependencies: {ProjectsWithDependencies}")]
public class DependencyTree
{
    /// <summary>
    /// Gets or sets the list of package nodes representing packages and their consumers.
    /// </summary>
    public List<PackageNode> PackageNodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of projects scanned.
    /// </summary>
    public int TotalProjects { get; set; }

    /// <summary>
    /// Gets or sets the number of projects that have package dependencies.
    /// </summary>
    public int ProjectsWithDependencies { get; set; }
}
