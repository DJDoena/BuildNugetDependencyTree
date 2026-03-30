using System.Diagnostics;

namespace BuildNugetDependencyTree;

[DebuggerDisplay("{ProjectName,nq} | PackageId: {PackageId,nq} | Refs: {PackageReferences.Count}")]
public class ProjectInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string? PackageId { get; set; }
    public string? PackageVersion { get; set; }
    public string? NuspecFilePath { get; set; }
    public List<PackageReference> PackageReferences { get; set; } = [];

    private string ProjectName => Path.GetFileNameWithoutExtension(FilePath);
}

[DebuggerDisplay("{PackageId,nq} | Version: {Version,nq}")]
public class PackageReference
{
    public string PackageId { get; set; } = string.Empty;
    public string? Version { get; set; }
}

[DebuggerDisplay("Packages: {PackageNodes.Count} | Total Projects: {TotalProjects} | With Dependencies: {ProjectsWithDependencies}")]
public class DependencyTree
{
    public List<PackageNode> PackageNodes { get; set; } = [];
    public int TotalProjects { get; set; }
    public int ProjectsWithDependencies { get; set; }
}

[DebuggerDisplay("📦 {PackageId,nq} | Project: {ProjectName,nq} | Consumers: {Consumers.Count}")]
public class PackageNode
{
    public string PackageId { get; set; } = string.Empty;
    public string ProjectFilePath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public List<ConsumerNode> Consumers { get; set; } = [];
}

[DebuggerDisplay("{ProjectName,nq} | Generates: {GeneratedPackageId,nq} | Transitive: {TransitiveConsumers.Count}")]
public class ConsumerNode
{
    public string ProjectFilePath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public bool GeneratesPackage { get; set; }
    public string? GeneratedPackageId { get; set; }
    public List<ConsumerNode> TransitiveConsumers { get; set; } = [];
}

[DebuggerDisplay("📦 {PackageId,nq} | Producers: {ProducerProjects.Count} | Consumes: {ConsumesPackages.Count} | ConsumedBy: {ConsumedByPackages.Count}")]
public class UnifiedPackageNode
{
    public string PackageId { get; set; } = string.Empty;
    public List<ProducerProject> ProducerProjects { get; set; } = [];
    public List<PackageConsumption> ConsumesPackages { get; set; } = [];
    public List<UnifiedPackageNode> ConsumedByPackages { get; set; } = [];
}

[DebuggerDisplay("{PackageId,nq} | Version: {Version,nq}")]
public class PackageConsumption
{
    public string PackageId { get; set; } = string.Empty;
    public string? Version { get; set; }
}

[DebuggerDisplay("{ProjectName,nq} | {ProjectFilePath,nq}")]
public class ProducerProject
{
    public string ProjectFilePath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public List<PackageReference> PackageReferences { get; set; } = [];
}

[DebuggerDisplay("Unified Packages: {RootPackages.Count} | All Packages: {AllPackages.Count}")]
public class UnifiedDependencyTree
{
    public List<UnifiedPackageNode> RootPackages { get; set; } = [];
    public Dictionary<string, UnifiedPackageNode> AllPackages { get; set; } = [];
    public int TotalProjects { get; set; }
    public int ProjectsWithDependencies { get; set; }
}
