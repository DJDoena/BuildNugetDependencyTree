using System.Diagnostics;

namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary>
/// Represents a project that produces a NuGet package.
/// </summary>
[DebuggerDisplay("{ProjectName,nq} | {ProjectFilePath,nq}")]
public class ProducerProject
{
    /// <summary>
    /// Gets or sets the file path to the producer project.
    /// </summary>
    public string ProjectFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the producer project.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the package produced by this project.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the list of NuGet packages referenced by this producer project.
    /// </summary>
    public List<PackageReference> PackageReferences { get; set; } = [];
}
