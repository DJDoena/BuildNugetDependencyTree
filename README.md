# BuildNugetDependencyTree
 
A .NET library for analyzing and visualizing NuGet package dependencies across multiple projects in a solution or folder. This library helps you understand the complex web of internal package dependencies, identify version mismatches, and visualize producer-consumer relationships.
 
## Features
 
- 🔍 **Project Scanning**: Recursively scans folders for `.csproj` files
- 📦 **Package Analysis**: Identifies which projects produce NuGet packages and their dependencies
- 🌳 **Dependency Tree Building**: Creates comprehensive dependency trees showing package relationships
- 🔄 **Bidirectional Relationships**: Tracks both "consumes" and "consumed-by" relationships
- ⚠️ **Version Mismatch Detection**: Identifies when projects consume different versions of the same package
- 📊 **Multiple Export Formats**: Exports to XML with various filtering options
- 🎯 **Prefix Filtering**: Filter packages by package ID prefix (e.g., only show internal packages)
- 🚀 **Pure Consumer Support**: Include projects that only consume packages (don't produce them) for comprehensive mismatch analysis
- 📄 **Rich Output Display**: Visual tree display with indicators and hierarchical formatting
 
## Getting Started
 
### Adding to Your Solution
 
1. Clone or download this repository
2. Add the `BuildNugetDependencyTreeLib` project to your solution:
   ```bash
   dotnet sln add path/to/BuildNugetDependencyTreeLib/BuildNugetDependencyTreeLib.csproj
   ```
3. Reference it from your project:
   ```bash
   dotnet add reference path/to/BuildNugetDependencyTreeLib/BuildNugetDependencyTreeLib.csproj
   ```
 
### Quick Start
 
```csharp
using DoenaSoft.BuildNugetDependencyTree;
 
// Scan all projects in a folder
var projects = ProjectScanner.ScanFolder(@"c:\my-solution");
 
// Build unified dependency tree
var builder = new DependencyTreeBuilder();
var unifiedTree = builder.BuildUnifiedTree(
    projects, 
    packageIdPrefix: "MyCompany",  // Optional: filter by package prefix
    includePureConsumers: true     // Include non-package-producing projects
);
 
// Display the tree
var display = new UnifiedTreeDisplay();
display.OnFeedback += message => { /* Handle output */ };
display.DisplayUnifiedTree(unifiedTree);
display.DisplaySummary(unifiedTree);
 
// Export to XML
var exporter = new XmlExporter();
exporter.ExportUnifiedToXml(unifiedTree, "dependencies.xml", null);
 
// Export version mismatches only
exporter.ExportMismatchesToXml(unifiedTree, "mismatches.xml");
```
 
## Core Components
 
### ProjectScanner
 
Scans `.csproj` files and extracts package information.
 
```csharp
// Subscribe to events for feedback
ProjectScanner.OnError += (file, ex) => { /* Handle error */ };
ProjectScanner.OnFeedback += message => { /* Handle feedback */ };
 
// Scan folder
var projects = ProjectScanner.ScanFolder(@"c:\source");
```
 
**Features:**
- Extracts `PackageId`, `PackageVersion`, `AssemblyName`
- Parses `<PackageReference>` elements
- Supports `.nuspec` files for package metadata
- Event-driven error handling and feedback
 
### DependencyTreeBuilder
 
Builds dependency trees showing package relationships.
 
```csharp
var builder = new DependencyTreeBuilder();
builder.OnFeedback += message => { /* Handle feedback */ };
 
// Build unified tree
var tree = builder.BuildUnifiedTree(
    projects,
    packageIdPrefix: "MyCompany",    // Optional: only include packages starting with prefix
    includePureConsumers: true       // Include consumer-only projects
);
 
// Filter out leaf packages (packages with no relationships)
var filtered = DependencyTreeBuilder.FilterLeafProducers(tree);
```
 
**Supported Tree Types:**
 
1. **Standard Dependency Tree** (`BuildTree`): Shows producer→consumer relationships
2. **Unified Dependency Tree** (`BuildUnifiedTree`): Shows bidirectional relationships with all producers per package
3. **Filtered Tree** (`FilterLeafProducers`): Removes isolated packages
 
**Tree Statistics:**
- Total projects scanned
- Projects with dependencies
- Root packages (no internal dependencies)
- All packages with producer/consumer counts
 
### UnifiedTreeDisplay
 
Displays dependency trees in a readable format with visual indicators.
 
```csharp
var display = new UnifiedTreeDisplay();
display.OnFeedback += message => { /* Handle output */ };
 
display.DisplayUnifiedTree(unifiedTree);
display.DisplaySummary(unifiedTree);
```
 
**Output Features:**
- 📦 Visual indicators for packages
- ⚠️ Version mismatch warnings
- Hierarchical indentation
- Producer/consumer counts
- Circular reference detection
 
**Example Output:**
```
📦 MyCompany.Core
   Project: Core (v1.2.3)
   Path: c:\src\Core\Core.csproj
 
📦 MyCompany.Services ⚠️ VERSION MISMATCH (consumes v1.2.0, expected v1.2.3)
   Project: Services (v2.0.0)
   Path: c:\src\Services\Services.csproj
   Consumes → MyCompany.Core (v1.2.0)
   Consumed by:
     → MyCompany.Api
```
 
### XmlExporter
 
Exports dependency trees to XML format.
 
```csharp
var exporter = new XmlExporter();
exporter.OnFeedback += message => { /* Handle feedback */ };
 
// Export full tree
exporter.ExportUnifiedToXml(tree, "dependencies_full.xml", null);
 
// Export filtered tree
var noLeafs = DependencyTreeBuilder.FilterLeafProducers(tree);
exporter.ExportUnifiedToXml(noLeafs, "dependencies_filtered.xml", "NoLeafProducers");
 
// Export only version mismatches
exporter.ExportMismatchesToXml(tree, "mismatches.xml");
```
 
**Export Options:**
- **Full Tree**: All packages and relationships
- **Filtered Tree**: Exclude leaf packages (no consumers/dependencies)
- **Mismatches Only**: Only packages with version discrepancies
 
## Models
 
### ProjectInfo
 
Represents a parsed `.csproj` file with package information:
 
```csharp
public class ProjectInfo
{
    public string FilePath { get; set; }
    public string? PackageId { get; set; }
    public string? PackageVersion { get; set; }
    public string? NuspecFilePath { get; set; }
    public List<PackageReference> PackageReferences { get; set; }
}
```
 
### UnifiedDependencyTree
 
The main tree structure containing all package relationships:
 
```csharp
public class UnifiedDependencyTree
{
    public List<UnifiedPackageNode> RootPackages { get; set; }
    public Dictionary<string, UnifiedPackageNode> AllPackages { get; set; }
    public int TotalProjects { get; set; }
    public int ProjectsWithDependencies { get; set; }
}
```
 
### UnifiedPackageNode
 
Represents a package with its producers and relationships:
 
```csharp
public class UnifiedPackageNode
{
    public string PackageId { get; set; }
    public List<ProducerProject> ProducerProjects { get; set; }
    public List<PackageConsumption> ConsumesPackages { get; set; }
    public List<UnifiedPackageNode> ConsumedByPackages { get; set; }
}
```
 
## Use Cases
 
### 1. Version Mismatch Detection
 
Identify when different projects in your solution use different versions of the same internal package:
 
```csharp
var projects = ProjectScanner.ScanFolder(folderPath);
var builder = new DependencyTreeBuilder();
var tree = builder.BuildUnifiedTree(projects, includePureConsumers: true);
 
// Export only mismatches for easy review
var exporter = new XmlExporter();
exporter.ExportMismatchesToXml(tree, "version_mismatches.xml");
```
 
### 2. Internal Package Dependency Analysis
 
Understand how your internal packages depend on each other:
 
```csharp
var tree = builder.BuildUnifiedTree(
    projects, 
    packageIdPrefix: "MyCompany"  // Only show MyCompany.* packages
);
 
var display = new UnifiedTreeDisplay();
display.DisplayUnifiedTree(tree);
```
 
### 3. Impact Analysis
 
Find all projects affected by a package change:
 
```csharp
var tree = builder.BuildUnifiedTree(projects);
 
// Find a specific package
var targetPackage = tree.AllPackages["MyCompany.Core"];
 
// See all packages that consume it
var affectedPackages = targetPackage.ConsumedByPackages
    .Select(c => c.PackageId)
    .ToList();
```
 
### 4. Dependency Cleanup
 
Identify leaf packages that aren't consumed by anything:
 
```csharp
var fullTree = builder.BuildUnifiedTree(projects);
var activeTree = DependencyTreeBuilder.FilterLeafProducers(fullTree);
 
// Packages removed from activeTree are leaf packages
var leafPackages = fullTree.AllPackages.Keys
    .Except(activeTree.AllPackages.Keys)
    .ToList();
 
// leafPackages now contains all unused leaf packages
```
 
## Advanced Features
 
### Pure Consumer Projects
 
By default, only projects that produce packages are included in the tree. Enable `includePureConsumers` to add projects that only consume packages:
 
```csharp
var tree = builder.BuildUnifiedTree(
    projects, 
    packageIdPrefix: "MyCompany",
    includePureConsumers: true  // Include non-package projects
);
```
 
Pure consumers appear in the tree with a `[Consumer]` prefix:
```
📦 [Consumer] MyWebApp
   Consumes → MyCompany.Core (v1.2.3)
   Consumes → MyCompany.Services (v2.0.0)
```
 
This is useful for:
- Finding version mismatches in application projects
- Understanding which apps consume which internal packages
- Complete dependency analysis across the entire solution
 
### Event-Driven Feedback
 
All major components support event-driven feedback for logging and progress tracking:
 
```csharp
// ProjectScanner events
ProjectScanner.OnError += (file, ex) => Logger.LogError($"Failed: {file}", ex);
ProjectScanner.OnFeedback += message => Logger.LogInfo(message);
 
// DependencyTreeBuilder events
builder.OnFeedback += message => Logger.LogInfo(message);
 
// Display events
display.OnFeedback += message => Logger.LogInfo(message);
 
// XmlExporter events
exporter.OnFeedback += message => Logger.LogInfo(message);
```
 
### Custom Filtering
 
Filter trees based on your specific needs:
 
```csharp
// Get only root packages (no dependencies)
var rootPackages = tree.RootPackages;
 
// Get packages that consume a specific package
var coreConsumers = tree.AllPackages["MyCompany.Core"].ConsumedByPackages;
 
// Get packages with multiple producers
var multiProducerPackages = tree.AllPackages.Values
    .Where(p => p.ProducerProjects.Count > 1)
    .ToList();
 
// Get packages with version mismatches
var mismatches = tree.AllPackages.Values
    .Where(pkg => pkg.ConsumedByPackages.Any(consumer =>
    {
        var consumedVersion = consumer.ConsumesPackages
            .FirstOrDefault(c => c.PackageId.Equals(pkg.PackageId, StringComparison.OrdinalIgnoreCase))
            ?.Version;
        
        var producerVersions = pkg.ProducerProjects
            .Select(p => p.Version)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .ToList();
        
        return consumedVersion != null && 
               producerVersions.Any() && 
               !producerVersions.Contains(consumedVersion);
    }))
    .ToList();
```
 
## XML Export Format
 
### Full Tree Export
 
```xml
<NuGetDependencyTree>
  <Package PackageId="MyCompany.Core">
    <ProducerProjects>
      <Producer ProjectName="Core" CsProjPath="c:\src\Core\Core.csproj" Version="1.2.3" />
    </ProducerProjects>
    <ConsumedBy>
      <Package PackageId="MyCompany.Services">
        <Consumption Version="1.2.3" />
        <!-- Nested consumers -->
      </Package>
    </ConsumedBy>
  </Package>
</NuGetDependencyTree>
```
 
### Version Mismatches Export
 
```xml
<NuGetDependencyTree FilteredView="VersionMismatchesOnly">
  <Package PackageId="MyCompany.Core">
    <ProducerProjects>
      <Producer ProjectName="Core" Version="1.2.3" />
    </ProducerProjects>
    <ConsumedBy>
      <Package PackageId="MyCompany.Services" HasVersionMismatch="true">
        <Consumption Version="1.2.0" ExpectedVersions="1.2.3" />
      </Package>
    </ConsumedBy>
  </Package>
</NuGetDependencyTree>
```
 
## Requirements
 
- .NET 10 or later
- C# 14.0
 
## Architecture
 
The library follows a modular design with clear separation of concerns:
 
```
┌─────────────────┐
│ ProjectScanner  │ ─→ Scans .csproj files
└────────┬────────┘
         │
         ↓
┌─────────────────────┐
│ DependencyTreeBuilder│ ─→ Builds relationship trees
└────────┬────────────┘
         │
         ├──→ UnifiedTreeDisplay ─→ Tree visualization
         │
         └──→ XmlExporter ─→ XML export
```
 
### Key Classes
 
| Class | Purpose |
|-------|---------|
| `ProjectScanner` | Parses `.csproj` files and extracts package information |
| `DependencyTreeBuilder` | Builds dependency trees from project information |
| `UnifiedTreeDisplay` | Displays trees with visual formatting |
| `XmlExporter` | Exports trees to XML with filtering options |
| `UnifiedDependencyTree` | Main tree data structure |
| `UnifiedPackageNode` | Represents a package with all its relationships |
 
## Common Scenarios
 
### Scenario 1: Find All Version Mismatches
 
```csharp
var projects = ProjectScanner.ScanFolder(@"c:\my-solution");
var builder = new DependencyTreeBuilder();
var tree = builder.BuildUnifiedTree(projects, includePureConsumers: true);
 
var exporter = new XmlExporter();
exporter.ExportMismatchesToXml(tree, "mismatches.xml");
```
 
### Scenario 2: Analyze Internal Package Dependencies
 
```csharp
var projects = ProjectScanner.ScanFolder(@"c:\my-solution");
var builder = new DependencyTreeBuilder();
 
// Only show packages with your company prefix
var tree = builder.BuildUnifiedTree(projects, packageIdPrefix: "MyCompany");
 
// Remove isolated packages
var activeTree = DependencyTreeBuilder.FilterLeafProducers(tree);
 
var display = new UnifiedTreeDisplay();
display.DisplayUnifiedTree(activeTree);
```
 
### Scenario 3: Export Multiple Views
 
```csharp
var projects = ProjectScanner.ScanFolder(folderPath);
var builder = new DependencyTreeBuilder();
var tree = builder.BuildUnifiedTree(projects, "MyCompany", includePureConsumers: true);
var exporter = new XmlExporter();
 
// Full tree with all packages
exporter.ExportUnifiedToXml(tree, "full_tree.xml", null);
 
// Filter out leaf packages
var noLeafs = DependencyTreeBuilder.FilterLeafProducers(tree);
exporter.ExportUnifiedToXml(noLeafs, "active_tree.xml", "NoLeafProducers");
 
// Only version mismatches
exporter.ExportMismatchesToXml(noLeafs, "mismatches.xml");
```
 
### Scenario 4: Programmatic Analysis
 
```csharp
var projects = ProjectScanner.ScanFolder(folderPath);
var builder = new DependencyTreeBuilder();
var tree = builder.BuildUnifiedTree(projects);
 
// Find packages with most consumers
var popularPackages = tree.AllPackages.Values
    .OrderByDescending(p => p.ConsumedByPackages.Count)
    .Take(5)
    .ToList();
 
// Find packages with multiple producers (potential issue)
var multiProducer = tree.AllPackages.Values
    .Where(p => p.ProducerProjects.Count > 1)
    .ToList();
 
// Find circular dependencies
var circularDependencies = tree.AllPackages.Values
    .Where(package => HasCircularDependency(package, new HashSet<string>()))
    .Select(p => p.PackageId)
    .ToList();
```
 
## Event Handling
 
All components provide events for feedback and error handling:
 
```csharp
// Global error handling
ProjectScanner.OnError += (file, ex) =>
{
    Logger.Error($"Failed to parse {file}: {ex.Message}");
};
 
// Progress feedback
builder.OnFeedback += message => Logger.Info($"[Builder] {message}");
display.OnFeedback += message => Logger.Info($"[Display] {message}");
exporter.OnFeedback += message => Logger.Info($"[Export] {message}");
```
 
## Understanding the Output
 
### Package Node Structure
 
Each package in the tree contains:
- **PackageId**: The NuGet package identifier
- **ProducerProjects**: All projects that produce this package (with versions)
- **ConsumesPackages**: Packages consumed by the producer projects
- **ConsumedByPackages**: Other packages that consume this package
 
### Version Mismatch Warnings
 
A version mismatch occurs when:
1. Package A produces version X
2. Package B consumes Package A version Y
3. X ≠ Y
 
This typically indicates:
- Outdated package references
- Need to update dependencies
- Potential runtime issues
 
### Root Packages
 
Root packages are packages that:
- Don't consume any other internal packages
- May be consumed by other packages
- Represent the foundation of your dependency hierarchy
 
### Leaf Packages
 
Leaf packages are packages that:
- Neither consume nor are consumed by any other packages
- Often utility packages or standalone tools
- Can be filtered out with `FilterLeafProducers`
 
## API Reference
 
### ProjectScanner
 
| Method | Description |
|--------|-------------|
| `ScanFolder(string)` | Scans a folder recursively for all `.csproj` files |
| `OnError` | Event triggered when a project file fails to parse |
| `OnFeedback` | Event for progress feedback messages |
 
### DependencyTreeBuilder
 
| Method | Description |
|--------|-------------|
| `BuildTree(List<ProjectInfo>)` | Builds a simple dependency tree |
| `BuildUnifiedTree(List<ProjectInfo>, string?, bool)` | Builds a unified bidirectional dependency tree |
| `FilterLeafProducers(UnifiedDependencyTree)` | Removes isolated packages from the tree |
| `CountAllRelationships(DependencyTree)` | Counts total relationships in a tree |
| `OnFeedback` | Event for progress feedback messages |
 
### UnifiedTreeDisplay
 
| Method | Description |
|--------|-------------|
| `DisplayUnifiedTree(UnifiedDependencyTree)` | Displays the complete tree to console |
| `DisplaySummary(UnifiedDependencyTree)` | Displays summary statistics |
| `OnFeedback` | Event for display output messages |
 
### XmlExporter
 
| Method | Description |
|--------|-------------|
| `ExportToXml(DependencyTree, string)` | Exports a standard dependency tree |
| `ExportUnifiedToXml(UnifiedDependencyTree, string, string?)` | Exports a unified tree with optional filter label |
| `ExportMismatchesToXml(UnifiedDependencyTree, string)` | Exports only packages with version mismatches |
| `OnFeedback` | Event for export feedback messages |
 
## Best Practices
 
1. **Always handle errors**: Subscribe to `OnError` events to catch parsing failures
2. **Use prefix filtering**: Focus on internal packages with `packageIdPrefix`
3. **Include pure consumers**: Enable `includePureConsumers` for complete mismatch analysis
4. **Export multiple views**: Generate full, filtered, and mismatch exports for different audiences
5. **Check for circular dependencies**: Look for packages listed multiple times in the hierarchy
 
## Performance Considerations
 
- Scanning large folders with many projects can take time
- The library loads and parses all `.csproj` files into memory
- Consider filtering by prefix for very large monorepos
- XML exports are formatted for readability (not optimized for size)
 
## Troubleshooting
 
### Issue: Projects not appearing in tree
 
**Cause**: Projects without a `PackageId` are not included by default
 
**Solution**: Use `includePureConsumers: true` or ensure projects have `<PackageId>` in their `.csproj`
 
### Issue: Version mismatches not detected
 
**Cause**: Projects without version information can't be checked
 
**Solution**: Ensure projects have `<Version>` or `<PackageVersion>` in their `.csproj` files
 
### Issue: Circular dependencies cause stack overflow
 
**Solution**: The library has circular dependency protection built-in. Check output for circular reference warnings.
 
## Building the Library
 
```bash
cd BuildNugetDependencyTreeLib
dotnet build
```
 
The library requires:
- .NET 10 SDK or later
- C# 14.0 compiler support
 
## Contributing
 
Contributions are welcome! The library follows these conventions:
- .NET 10 target framework
- C# 14.0 language features
- Nullable reference types enabled
- XML documentation for all public APIs (enforced with `CS1591` as error)
- Clean code with no warnings
 
## License
 
[Specify your license here]
 
## Author
 
DoenaSoft
 
## Related Projects
 
This library can be used as a foundation for:
- NuGet package version management tools
- Solution-wide dependency analyzers
- CI/CD pipeline validation
- Dependency graph visualization tools
- Package update planners
