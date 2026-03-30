namespace BuildNugetDependencyTree;

public class UnifiedTreeDisplay
{
    public void DisplayUnifiedTree(UnifiedDependencyTree tree)
    {
        Console.WriteLine("\n=== UNIFIED NUGET PACKAGE DEPENDENCY TREE ===\n");
        Console.WriteLine($"Total packages: {tree.AllPackages.Count}");
        Console.WriteLine($"Root packages (no dependencies): {tree.RootPackages.Count}\n");

        var processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rootPackage in tree.RootPackages)
        {
            DisplayPackageNode(rootPackage, 0, processedPackages);
            Console.WriteLine();
        }

        // Display any orphaned packages (shouldn't happen normally)
        var orphanedPackages = tree.AllPackages.Values
            .Where(p => !processedPackages.Contains(p.PackageId))
            .ToList();

        if (orphanedPackages.Any())
        {
            Console.WriteLine("\n=== CIRCULAR OR ORPHANED PACKAGES ===\n");
            foreach (var package in orphanedPackages)
            {
                DisplayPackageNode(package, 0, new HashSet<string>());
                Console.WriteLine();
            }
        }
    }

    private void DisplayPackageNode(UnifiedPackageNode node, int indent, HashSet<string> processedPackages, UnifiedPackageNode? parentPackage = null)
    {
        string indentStr = new string(' ', indent * 3);

        if (processedPackages.Contains(node.PackageId))
        {
            Console.WriteLine($"{indentStr}📦 {node.PackageId} (see above)");
            return;
        }

        processedPackages.Add(node.PackageId);

        // Check for version mismatch if this is a consumer of a parent package
        string versionMismatchWarning = "";
        if (parentPackage != null)
        {
            var consumedVersion = node.ConsumesPackages
                .FirstOrDefault(cp => cp.PackageId.Equals(parentPackage.PackageId, StringComparison.OrdinalIgnoreCase))?.Version;

            var producerVersions = parentPackage.ProducerProjects
                .Select(p => p.Version)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .ToList();

            if (!string.IsNullOrEmpty(consumedVersion) && producerVersions.Any())
            {
                if (!producerVersions.Contains(consumedVersion, StringComparer.OrdinalIgnoreCase))
                {
                    var expectedVersions = string.Join(", ", producerVersions);
                    versionMismatchWarning = $" ⚠️ VERSION MISMATCH (consumes v{consumedVersion}, expected v{expectedVersions})";
                }
            }
        }

        Console.WriteLine($"{indentStr}📦 {node.PackageId}{versionMismatchWarning}");

        // Display all producer projects
        if (node.ProducerProjects.Count == 1)
        {
            var producer = node.ProducerProjects[0];
            var versionInfo = !string.IsNullOrEmpty(producer.Version) ? $" (v{producer.Version})" : "";
            Console.WriteLine($"{indentStr}   Project: {producer.ProjectName}{versionInfo}");
            Console.WriteLine($"{indentStr}   Path: {producer.ProjectFilePath}");
        }
        else
        {
            Console.WriteLine($"{indentStr}   Produced by {node.ProducerProjects.Count} projects:");
            foreach (var producer in node.ProducerProjects)
            {
                var versionInfo = !string.IsNullOrEmpty(producer.Version) ? $" (v{producer.Version})" : "";
                Console.WriteLine($"{indentStr}     - {producer.ProjectName}{versionInfo}");
                Console.WriteLine($"{indentStr}       Path: {producer.ProjectFilePath}");
            }
        }

        if (node.ConsumesPackages.Any())
        {
            var consumedPackagesStr = string.Join(", ", node.ConsumesPackages.Select(cp => 
            {
                var version = !string.IsNullOrEmpty(cp.Version) ? $" (v{cp.Version})" : "";
                return $"{cp.PackageId}{version}";
            }));
            Console.WriteLine($"{indentStr}   Depends on: {consumedPackagesStr}");
        }

        if (node.ConsumedByPackages.Any())
        {
            Console.WriteLine($"{indentStr}   └─ Consumed by:");
            foreach (var consumer in node.ConsumedByPackages)
            {
                DisplayPackageNode(consumer, indent + 2, processedPackages, node);
            }
        }
    }

    public void DisplaySummary(UnifiedDependencyTree tree)
    {
        Console.WriteLine("\n=== SUMMARY ===");
        Console.WriteLine($"Total projects scanned: {tree.TotalProjects}");
        Console.WriteLine($"Projects generating NuGet packages: {tree.AllPackages.Count}");
        Console.WriteLine($"Projects with package references: {tree.ProjectsWithDependencies}");
        Console.WriteLine($"Root packages (no internal dependencies): {tree.RootPackages.Count}");

        int totalRelationships = tree.AllPackages.Values.Sum(p => p.ConsumedByPackages.Count);
        Console.WriteLine($"Total dependency relationships: {totalRelationships}");
    }
}
