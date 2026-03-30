using BuildNugetDependencyTree;

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
var scanner = new ProjectScanner();
scanner.OnParseError += (file, ex) =>
{
    Console.WriteLine($"Warning: Failed to parse {file}: {ex.Message}");
};
scanner.OnNuspecDetected += (message) =>
{
    //Console.WriteLine($"Info: {message}");
};

var projects = scanner.ScanFolder(folderPath);

// Build unified dependency tree
var builder = new DependencyTreeBuilder();

var unifiedTree = builder.BuildUnifiedTree(projects);

// Display results
//var display = new UnifiedTreeDisplay();
//display.DisplayUnifiedTree(unifiedTree);
//display.DisplaySummary(unifiedTree);

var noLeafsTree = builder.FilterLeafProducers(unifiedTree);

// Export to XML
var xmlExporter = new XmlExporter();

//xmlExporter.ExportUnifiedToXml(unifiedTree, Path.Combine(folderPath, "NuGetDependencyTree_full.xml"));

//xmlExporter.ExportUnifiedToXml(noLeafsTree, Path.Combine(folderPath, "NuGetDependencyTree_noleafs.xml"), "NoLeafProducers");

xmlExporter.ExportMismatchesToXml(noLeafsTree, Path.Combine(folderPath, "NuGetDependencyTree_Mismatches.xml"));

Console.WriteLine("Press <enter> to exit.");
Console.ReadLine();
