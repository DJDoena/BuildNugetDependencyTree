using DoenaSoft.BuildNugetDependencyTree;

var folderPath = args?.FirstOrDefault();

while (!Directory.Exists(folderPath))
{
    Console.WriteLine("Enter the folder path to scan for .csproj files:");

    folderPath = Console.ReadLine();
}

Console.WriteLine($"Scanning folder: {folderPath}");
Console.WriteLine("=".PadRight(80, '='));

// Scan all .csproj files
var projectFiles = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories);
Console.WriteLine($"Found {projectFiles.Length} project file(s)\n");

// Scan and parse projects
ProjectScanner.OnError += (file, ex) => Console.WriteLine($"Warning: Failed to parse {file}: {ex.Message}");

ProjectScanner.OnFeedback += Console.WriteLine;

var projects = ProjectScanner.ScanFolder(folderPath);

// Build unified dependency tree
var builder = new DependencyTreeBuilder();
builder.OnFeedback += Console.WriteLine;
var unifiedTree = builder.BuildUnifiedTree(projects, "DoenaSoft");
builder.OnFeedback -= Console.WriteLine;

// Display results - wire up event handler
var display = new UnifiedTreeDisplay();
display.OnFeedback += Console.WriteLine;
display.DisplayUnifiedTree(unifiedTree);
display.DisplaySummary(unifiedTree);
display.OnFeedback -= Console.WriteLine;

var noLeafsTree = DependencyTreeBuilder.FilterLeafProducers(unifiedTree);

// Export to XML - wire up event handler
var xmlExporter = new XmlExporter();
xmlExporter.OnFeedback += Console.WriteLine;

xmlExporter.ExportUnifiedToXml(unifiedTree, Path.Combine(folderPath, "NuGetDependencyTree_full.xml"), null);

xmlExporter.ExportUnifiedToXml(noLeafsTree, Path.Combine(folderPath, "NuGetDependencyTree_noleafs.xml"), "NoLeafProducers");

// Export version mismatches to separate XML

xmlExporter.ExportMismatchesToXml(noLeafsTree, Path.Combine(folderPath, "NuGetDependencyTree_Mismatches.xml"));

xmlExporter.OnFeedback -= Console.WriteLine;

Console.WriteLine("Press <enter> to exit.");
Console.ReadLine();
