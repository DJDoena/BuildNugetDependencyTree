using System.Diagnostics;
using DoenaSoft.BuildNugetDependencyTree;
using DoenaSoft.BuildNugetDependencyTree.Models;
using DoenaSoft.ToolBox.Generics;

var folderPath = args?.FirstOrDefault();

while (!Directory.Exists(folderPath))
{
    Console.WriteLine("Enter the folder path to scan for .csproj files:");

    folderPath = Console.ReadLine();
}

Console.WriteLine($"Scanning folder: {folderPath}");
Console.WriteLine("=".PadRight(80, '='));

var projectFiles = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories);

Console.WriteLine($"Found {projectFiles.Length} project file(s)\n");

ProjectScanner.OnError += (file, ex) => Console.WriteLine($"Warning: Failed to parse {file}: {ex.Message}");

var projects = ProjectScanner.ScanFolder(folderPath);

var builder = new DependencyTreeBuilder();

var fullTree = builder.BuildUnifiedTree(projects, includePureConsumers: true);

var toplLevelNugetMismatches = fullTree.AllPackages.Values.FilterForDirectVersionMismatches("DoenaSoft.", PackageFilter.IncludePackageProducers).ToList();

var topLevelNugetMismatchesFileName = "TopLevelNugetMismatches.xml";

XmlSerializer<List<UnifiedPackageNode>>.Serialize(topLevelNugetMismatchesFileName, toplLevelNugetMismatches);

Process.Start("explorer.exe", topLevelNugetMismatchesFileName);