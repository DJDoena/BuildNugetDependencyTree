//using DoenaSoft.BuildNugetDependencyTree.Models;

//namespace DoenaSoft.BuildNugetDependencyTree;

///// <summary>
///// Displays unified dependency tree information showing bidirectional package relationships.
///// </summary>
//public class UnifiedTreeDisplay
//{
//    /// <summary>
//    /// Event raised to provide feedback messages during display operations.
//    /// </summary>
//    public event Action<string>? OnFeedback;

//    /// <summary>
//    /// Displays the complete unified dependency tree showing all packages, producers, and consumers.
//    /// </summary>
//    /// <param name="tree">The unified dependency tree to display.</param>
//    public void DisplayUnifiedTree(UnifiedDependencyTree tree)
//    {
//        OnFeedback?.Invoke("\n=== UNIFIED NUGET PACKAGE DEPENDENCY TREE ===\n");
//        OnFeedback?.Invoke($"Total packages: {tree.AllPackages.Count}");
//        OnFeedback?.Invoke($"Root packages (no dependencies): {tree.RootPackages.Count}\n");

//        var processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//        foreach (var rootPackage in tree.RootPackages)
//        {
//            this.DisplayPackageNode(rootPackage, 0, processedPackages);
//            OnFeedback?.Invoke(string.Empty);
//        }

//        // Display any orphaned packages (shouldn't happen normally)
//        var orphanedPackages = tree.AllPackages.Values
//            .Where(p => !processedPackages.Contains(p.PackageId))
//            .ToList();

//        if (orphanedPackages.Any())
//        {
//            OnFeedback?.Invoke("\n=== CIRCULAR OR ORPHANED PACKAGES ===\n");
//            foreach (var package in orphanedPackages)
//            {
//                this.DisplayPackageNode(package, 0, []);
//                OnFeedback?.Invoke(string.Empty);
//            }
//        }
//    }

//    /// <summary>
//    /// Displays a package node with its producers, consumed packages, and consuming packages.
//    /// </summary>
//    /// <param name="node">The package node to display.</param>
//    /// <param name="indent">The current indentation level.</param>
//    /// <param name="processedPackages">Set of already displayed package IDs to avoid duplicates.</param>
//    /// <param name="parentPackage">The parent package to check for version mismatches.</param>
//    private void DisplayPackageNode(UnifiedPackageNode node, int indent, HashSet<string> processedPackages, UnifiedPackageNode? parentPackage = null)
//    {
//        var indentStr = new string(' ', indent * 3);

//        if (this.IsPackageAlreadyProcessed(node, processedPackages, indentStr))
//        {
//            return;
//        }

//        processedPackages.Add(node.PackageId);

//        var versionMismatchWarning = GetVersionMismatchWarning(node, parentPackage);
//        this.DisplayPackageHeader(node, indentStr, versionMismatchWarning);
//        this.DisplayProducerProjects(node, indentStr);
//        this.DisplayConsumedPackages(node, indentStr);
//        this.DisplayConsumingPackages(node, indent, processedPackages);
//    }

//    /// <summary>
//    /// Checks if a package has already been processed and displays a reference if so.
//    /// </summary>
//    /// <param name="node">The package node to check.</param>
//    /// <param name="processedPackages">Set of already processed package IDs.</param>
//    /// <param name="indentStr">The indentation string for display formatting.</param>
//    /// <returns>True if the package was already processed, otherwise false.</returns>
//    private bool IsPackageAlreadyProcessed(UnifiedPackageNode node, HashSet<string> processedPackages, string indentStr)
//    {
//        if (!processedPackages.Contains(node.PackageId))
//        {
//            return false;
//        }

//        OnFeedback?.Invoke($"{indentStr}📦 {node.PackageId} (see above)");
//        return true;
//    }

//    /// <summary>
//    /// Generates a version mismatch warning string if the consumed version differs from the produced version.
//    /// </summary>
//    /// <param name="node">The package node to check.</param>
//    /// <param name="parentPackage">The parent package to compare versions against.</param>
//    /// <returns>A warning string if a mismatch is found, otherwise an empty string.</returns>
//    private static string GetVersionMismatchWarning(UnifiedPackageNode node, UnifiedPackageNode? parentPackage)
//    {
//        if (parentPackage == null)
//        {
//            return string.Empty;
//        }

//        var consumedVersion = node.ConsumesPackages
//            .FirstOrDefault(cp => cp.PackageId.Equals(parentPackage.PackageId, StringComparison.OrdinalIgnoreCase))?.Version;

//        if (string.IsNullOrEmpty(consumedVersion))
//        {
//            return string.Empty;
//        }

//        var producerVersions = parentPackage.ProducerProjects
//            .Select(p => p.Version)
//            .Where(v => !string.IsNullOrEmpty(v))
//            .Distinct()
//            .ToList();

//        if (producerVersions.Count == 0)
//        {
//            return string.Empty;
//        }

//        if (producerVersions.Contains(consumedVersion, StringComparer.OrdinalIgnoreCase))
//        {
//            return string.Empty;
//        }

//        var expectedVersions = string.Join(", ", producerVersions);
//        return $" ⚠️ VERSION MISMATCH (consumes v{consumedVersion}, expected v{expectedVersions})";
//    }

//    /// <summary>
//    /// Displays the header for a package including any version mismatch warnings.
//    /// </summary>
//    /// <param name="node">The package node to display.</param>
//    /// <param name="indentStr">The indentation string for formatting.</param>
//    /// <param name="versionMismatchWarning">The version mismatch warning string, if any.</param>
//    private void DisplayPackageHeader(UnifiedPackageNode node, string indentStr, string versionMismatchWarning)
//    {
//        OnFeedback?.Invoke($"{indentStr}📦 {node.PackageId}{versionMismatchWarning}");
//    }

//    /// <summary>
//    /// Displays all producer projects for a package, handling single and multiple producers differently.
//    /// </summary>
//    /// <param name="node">The package node whose producers to display.</param>
//    /// <param name="indentStr">The indentation string for formatting.</param>
//    private void DisplayProducerProjects(UnifiedPackageNode node, string indentStr)
//    {
//        if (node.ProducerProjects.Count == 1)
//        {
//            this.DisplaySingleProducer(node.ProducerProjects[0], indentStr);
//        }
//        else
//        {
//            this.DisplayMultipleProducers(node.ProducerProjects, indentStr);
//        }
//    }

//    /// <summary>
//    /// Displays a single producer project with its version information.
//    /// </summary>
//    /// <param name="producer">The producer project to display.</param>
//    /// <param name="indentStr">The indentation string for formatting.</param>
//    private void DisplaySingleProducer(ProducerProject producer, string indentStr)
//    {
//        var versionInfo = !string.IsNullOrEmpty(producer.Version) ? $" (v{producer.Version})" : "";
//        OnFeedback?.Invoke($"{indentStr}   Project: {producer.ProjectName}{versionInfo}");
//        OnFeedback?.Invoke($"{indentStr}   Path: {producer.ProjectFilePath}");
//    }

//    /// <summary>
//    /// Displays multiple producer projects with their version information.
//    /// </summary>
//    /// <param name="producers">The list of producer projects to display.</param>
//    /// <param name="indentStr">The indentation string for formatting.</param>
//    private void DisplayMultipleProducers(List<ProducerProject> producers, string indentStr)
//    {
//        OnFeedback?.Invoke($"{indentStr}   Produced by {producers.Count} projects:");
//        foreach (var producer in producers)
//        {
//            var versionInfo = !string.IsNullOrEmpty(producer.Version) ? $" (v{producer.Version})" : "";
//            OnFeedback?.Invoke($"{indentStr}     - {producer.ProjectName}{versionInfo}");
//            OnFeedback?.Invoke($"{indentStr}       Path: {producer.ProjectFilePath}");
//        }
//    }

//    /// <summary>
//    /// Displays the packages consumed by this package with their versions.
//    /// </summary>
//    /// <param name="node">The package node whose consumed packages to display.</param>
//    /// <param name="indentStr">The indentation string for formatting.</param>
//    private void DisplayConsumedPackages(UnifiedPackageNode node, string indentStr)
//    {
//        if (node.ConsumesPackages.Count == 0)
//        {
//            return;
//        }

//        var consumedPackagesStr = string.Join(", ", node.ConsumesPackages.Select(cp =>
//        {
//            var version = !string.IsNullOrEmpty(cp.Version) ? $" (v{cp.Version})" : "";
//            return $"{cp.PackageId}{version}";
//        }));

//        OnFeedback?.Invoke($"{indentStr}   Depends on: {consumedPackagesStr}");
//    }

//    /// <summary>
//    /// Displays the packages that consume this package, recursively showing the consumption hierarchy.
//    /// </summary>
//    /// <param name="node">The package node whose consuming packages to display.</param>
//    /// <param name="indent">The current indentation level.</param>
//    /// <param name="processedPackages">Set of already displayed package IDs to avoid duplicates.</param>
//    private void DisplayConsumingPackages(UnifiedPackageNode node, int indent, HashSet<string> processedPackages)
//    {
//        if (node.ConsumedByPackages.Count == 0)
//        {
//            return;
//        }

//        var indentStr = new string(' ', indent * 3);
//        OnFeedback?.Invoke($"{indentStr}   └─ Consumed by:");

//        foreach (var consumer in node.ConsumedByPackages)
//        {
//            this.DisplayPackageNode(consumer, indent + 2, processedPackages, node);
//        }
//    }

//    /// <summary>
//    /// Displays a summary of the unified dependency tree statistics.
//    /// </summary>
//    /// <param name="tree">The unified dependency tree to summarize.</param>
//    public void DisplaySummary(UnifiedDependencyTree tree)
//    {
//        OnFeedback?.Invoke("\n=== SUMMARY ===");
//        OnFeedback?.Invoke($"Total projects scanned: {tree.TotalProjects}");
//        OnFeedback?.Invoke($"Projects generating NuGet packages: {tree.AllPackages.Count}");
//        OnFeedback?.Invoke($"Projects with package references: {tree.ProjectsWithDependencies}");
//        OnFeedback?.Invoke($"Root packages (no internal dependencies): {tree.RootPackages.Count}");

//        var totalRelationships = tree.AllPackages.Values.Sum(p => p.ConsumedByPackages.Count);
//        OnFeedback?.Invoke($"Total dependency relationships: {totalRelationships}");
//    }
//}
