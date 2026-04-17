using System.Diagnostics;
using System.Xml.Serialization;

namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary>
/// Represents a unified dependency tree showing all packages, their producers, and consumption relationships.
/// </summary>
[DebuggerDisplay("Unified Packages: {RootPackages.Count} | All Packages: {AllPackages.Count}")]
public class UnifiedDependencyTree
{
    /// <summary>
    /// Gets or sets the list of root packages that don't depend on other internal packages.
    /// </summary>
    [XmlArray("RootPackages")]
    [XmlArrayItem("RootPackage")]
    public List<UnifiedPackageNode> RootPackages { get; set; } = [];

    /// <summary>
    /// Gets or sets the dictionary of all packages indexed by package ID.
    /// </summary>
    [XmlArray("AllPackages")]
    [XmlArrayItem("AllPackage")]
    public Dictionary<string, UnifiedPackageNode> AllPackages { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of projects scanned.
    /// </summary>
    public int TotalProjects { get; set; }

    /// <summary>
    /// Gets or sets the number of projects that have package dependencies.
    /// </summary>
    public int ProjectsWithDependencies { get; set; }
}
