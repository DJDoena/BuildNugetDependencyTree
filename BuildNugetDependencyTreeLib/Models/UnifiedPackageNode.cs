using System.Diagnostics;
using System.Xml.Serialization;

namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary>
/// Represents a unified view of a package showing all producers, consumed packages, and consuming packages.
/// </summary>
[DebuggerDisplay("📦 {PackageId,nq} | Producers: {ProducerProjects.Count} | Consumes: {ConsumesPackages.Count} | ConsumedBy: {ConsumedByPackages.Count}")]
public class UnifiedPackageNode
{
    /// <summary>
    /// Gets or sets the NuGet package identifier.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of projects that produce this package.
    /// </summary>
    [XmlArray("ProducerProjects")]
    [XmlArrayItem("ProducerProject")]
    public List<ProducerProject> ProducerProjects { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of packages consumed by the projects that produce this package.
    /// </summary>
    [XmlArray("ConsumesPackages")]
    [XmlArrayItem("ConsumesPackage")]
    public List<PackageConsumption> ConsumesPackages { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of packages that consume this package.
    /// </summary>
    [XmlArray("ConsumedByPackages")]
    [XmlArrayItem("ConsumedByPackage")]
    public List<UnifiedPackageNode> ConsumedByPackages { get; set; } = [];
}
