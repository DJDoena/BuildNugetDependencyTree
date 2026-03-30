using DoenaSoft.BuildNugetDependencyTree.Models;

namespace DoenaSoft.BuildNugetDependencyTree;

/// <summary>
/// Builds dependency trees from project information, showing package production and consumption relationships.
/// </summary>
public sealed class DependencyTreeBuilder
{
    /// <summary>
    /// Event raised to provide feedback messages during tree building operations.
    /// </summary>
    public event Action<string>? OnFeedback;

    /// <summary>
    /// Builds a dependency tree showing which projects consume packages produced by other projects.
    /// </summary>
    /// <param name="projects">The list of projects to analyze.</param>
    /// <returns>A DependencyTree containing package nodes and their consumer relationships.</returns>
    public DependencyTree BuildTree(List<ProjectInfo> projects)
    {
        var tree = new DependencyTree
        {
            TotalProjects = projects.Count,
            ProjectsWithDependencies = projects.Count(p => p.PackageReferences.Count != 0)
        };

        var packageProducers = projects.Where(p => !string.IsNullOrEmpty(p.PackageId)).ToList();

        foreach (var producer in packageProducers)
        {
            var node = new PackageNode
            {
                PackageId = producer.PackageId!,
                ProjectFilePath = producer.FilePath,
                ProjectName = Path.GetFileNameWithoutExtension(producer.FilePath)
            };

            // Find direct consumers
            BuildConsumerTree(node, producer, projects, new HashSet<string>());

            tree.PackageNodes.Add(node);
        }

        return tree;
    }

    /// <summary>
    /// Counts the total number of dependency relationships in a dependency tree.
    /// </summary>
    /// <param name="tree">The dependency tree to analyze.</param>
    /// <returns>The total count of all relationships including transitive dependencies.</returns>
    public static int CountAllRelationships(DependencyTree tree)
    {
        var count = 0;
        foreach (var node in tree.PackageNodes)
        {
            count += CountNodeRelationships(node);
        }
        return count;
    }

    /// <summary>
    /// Recursively builds a tree of consumers for a package, preventing circular dependencies.
    /// </summary>
    /// <param name="parentNode">The parent package node to add consumers to.</param>
    /// <param name="producerProject">The project that produces the package.</param>
    /// <param name="allProjects">The complete list of projects to search for consumers.</param>
    /// <param name="visitedProjects">Set of already visited project paths to prevent circular dependencies.</param>
    private static void BuildConsumerTree(PackageNode parentNode, ProjectInfo producerProject, List<ProjectInfo> allProjects, HashSet<string> visitedProjects)
    {
        // Prevent circular dependencies
        if (!visitedProjects.Add(producerProject.FilePath))
            return;

        var consumers = allProjects.Where(p =>
            p.PackageReferences.Any(r => r.PackageId.Equals(producerProject.PackageId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var consumer in consumers)
        {
            var consumerNode = new ConsumerNode
            {
                ProjectFilePath = consumer.FilePath,
                ProjectName = Path.GetFileNameWithoutExtension(consumer.FilePath),
                GeneratesPackage = !string.IsNullOrEmpty(consumer.PackageId),
                GeneratedPackageId = consumer.PackageId
            };

            // Recursively build transitive dependencies
            if (consumerNode.GeneratesPackage)
            {
                BuildTransitiveConsumers(consumerNode, consumer, allProjects, new HashSet<string>(visitedProjects));
            }

            parentNode.Consumers.Add(consumerNode);
        }
    }

    /// <summary>
    /// Recursively builds a tree of transitive consumers for a consumer project.
    /// </summary>
    /// <param name="parentConsumer">The parent consumer node to add transitive consumers to.</param>
    /// <param name="consumerProject">The consumer project to find transitive consumers for.</param>
    /// <param name="allProjects">The complete list of projects to search.</param>
    /// <param name="visitedProjects">Set of already visited project paths to prevent circular dependencies.</param>
    private static void BuildTransitiveConsumers(ConsumerNode parentConsumer, ProjectInfo consumerProject, List<ProjectInfo> allProjects, HashSet<string> visitedProjects)
    {
        // Prevent circular dependencies
        if (!visitedProjects.Add(consumerProject.FilePath))
            return;

        var transitiveConsumers = allProjects.Where(p =>
            p.PackageReferences.Any(r => r.PackageId.Equals(consumerProject.PackageId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var transitive in transitiveConsumers)
        {
            var transitiveNode = new ConsumerNode
            {
                ProjectFilePath = transitive.FilePath,
                ProjectName = Path.GetFileNameWithoutExtension(transitive.FilePath),
                GeneratesPackage = !string.IsNullOrEmpty(transitive.PackageId),
                GeneratedPackageId = transitive.PackageId
            };

            // Continue recursively for deeper levels
            if (transitiveNode.GeneratesPackage)
            {
                BuildTransitiveConsumers(transitiveNode, transitive, allProjects, new HashSet<string>(visitedProjects));
            }

            parentConsumer.TransitiveConsumers.Add(transitiveNode);
        }
    }

    /// <summary>
    /// Counts all relationships for a package node including all consumer relationships.
    /// </summary>
    /// <param name="node">The package node to count relationships for.</param>
    /// <returns>The total count of relationships.</returns>
    private static int CountNodeRelationships(PackageNode node)
    {
        var count = node.Consumers.Count;

        foreach (var consumer in node.Consumers)
        {
            count += CountConsumerRelationships(consumer);
        }

        return count;
    }

    /// <summary>
    /// Recursively counts all relationships for a consumer node including transitive consumers.
    /// </summary>
    /// <param name="node">The consumer node to count relationships for.</param>
    /// <returns>The total count of relationships.</returns>
    private static int CountConsumerRelationships(ConsumerNode node)
    {
        var count = node.TransitiveConsumers.Count;

        foreach (var transitive in node.TransitiveConsumers)
        {
            count += CountConsumerRelationships(transitive);
        }

        return count;
    }

    /// <summary>
    /// Filters a dependency tree to include only packages with consumers and consumers that generate packages.
    /// </summary>
    /// <param name="tree">The dependency tree to filter.</param>
    /// <returns>A filtered dependency tree containing only relevant producers and consumers.</returns>
    public static DependencyTree FilterTreeForProducersAndConsumers(DependencyTree tree)
    {
        var filteredTree = new DependencyTree
        {
            TotalProjects = tree.TotalProjects,
            ProjectsWithDependencies = tree.ProjectsWithDependencies
        };

        foreach (var packageNode in tree.PackageNodes)
        {
            // Only include packages that have consumers
            if (packageNode.Consumers.Count != 0)
            {
                var filteredNode = new PackageNode
                {
                    PackageId = packageNode.PackageId,
                    ProjectFilePath = packageNode.ProjectFilePath,
                    ProjectName = packageNode.ProjectName,
                    // Filter consumers to only include those that generate packages or have transitive consumers that do
                    Consumers = FilterConsumers(packageNode.Consumers)
                };

                // Only add the package if it still has consumers after filtering
                if (filteredNode.Consumers.Count != 0)
                {
                    filteredTree.PackageNodes.Add(filteredNode);
                }
            }
        }

        return filteredTree;
    }

    /// <summary>
    /// Recursively filters consumers to include only those that generate packages or have transitive consumers that do.
    /// </summary>
    /// <param name="consumers">The list of consumers to filter.</param>
    /// <returns>A filtered list of consumers.</returns>
    private static List<ConsumerNode> FilterConsumers(List<ConsumerNode> consumers)
    {
        var filteredConsumers = new List<ConsumerNode>();

        foreach (var consumer in consumers)
        {
            // Keep consumers that generate packages
            if (consumer.GeneratesPackage)
            {
                var filteredConsumer = new ConsumerNode
                {
                    ProjectFilePath = consumer.ProjectFilePath,
                    ProjectName = consumer.ProjectName,
                    GeneratesPackage = consumer.GeneratesPackage,
                    GeneratedPackageId = consumer.GeneratedPackageId,
                    // Recursively filter transitive consumers
                    TransitiveConsumers = FilterConsumers(consumer.TransitiveConsumers)
                };

                filteredConsumers.Add(filteredConsumer);
            }
            // Also check if consumer has transitive consumers that generate packages
            else if (consumer.TransitiveConsumers.Count != 0)
            {
                var filteredTransitives = FilterConsumers(consumer.TransitiveConsumers);

                if (filteredTransitives.Count != 0)
                {
                    var filteredConsumer = new ConsumerNode
                    {
                        ProjectFilePath = consumer.ProjectFilePath,
                        ProjectName = consumer.ProjectName,
                        GeneratesPackage = consumer.GeneratesPackage,
                        GeneratedPackageId = consumer.GeneratedPackageId,
                        TransitiveConsumers = filteredTransitives
                    };

                    filteredConsumers.Add(filteredConsumer);
                }
            }
        }

        return filteredConsumers;
    }

    /// <summary>
    /// Builds a unified dependency tree showing all packages, their producers, and bidirectional consumption relationships.
    /// </summary>
    /// <param name="projects">The list of projects to analyze.</param>
    /// <param name="packageIdPrefix">Optional prefix to filter packages by package ID.</param>
    /// <param name="includePureConsumers">If true, includes pure consumer projects (projects that don't produce packages) in the tree for version mismatch analysis.</param>
    /// <returns>A UnifiedDependencyTree containing all package relationships.</returns>
    public UnifiedDependencyTree BuildUnifiedTree(List<ProjectInfo> projects, string? packageIdPrefix = null, bool includePureConsumers = false)
    {
        var unifiedTree = InitializeUnifiedTree(projects);

        var packageProducers = GetFilteredPackageProducers(projects, packageIdPrefix);

        var producersByPackageId = GroupProducersByPackageId(packageProducers);

        CreatePackageNodes(unifiedTree, producersByPackageId, packageIdPrefix);

        if (includePureConsumers)
        {
            AddPureConsumerProjects(unifiedTree, projects, packageProducers, packageIdPrefix);
        }

        BuildConsumedByRelationships(unifiedTree);

        IdentifyRootPackages(unifiedTree);

        return unifiedTree;
    }

    /// <summary>
    /// Initializes a new unified dependency tree with project statistics.
    /// </summary>
    /// <param name="projects">The list of projects to gather statistics from.</param>
    /// <returns>A new UnifiedDependencyTree with initialized statistics.</returns>
    private static UnifiedDependencyTree InitializeUnifiedTree(List<ProjectInfo> projects)
    {
        return new UnifiedDependencyTree
        {
            TotalProjects = projects.Count,
            ProjectsWithDependencies = projects.Count(p => p.PackageReferences.Count != 0)
        };
    }

    /// <summary>
    /// Gets a list of projects that produce packages, optionally filtered by package ID prefix.
    /// </summary>
    /// <param name="projects">The list of projects to filter.</param>
    /// <param name="packageIdPrefix">Optional prefix to filter packages by package ID.</param>
    /// <returns>A filtered list of package producer projects.</returns>
    private static List<ProjectInfo> GetFilteredPackageProducers(List<ProjectInfo> projects, string? packageIdPrefix)
    {
        var packageProducers = projects
            .Where(p => !string.IsNullOrEmpty(p.PackageId))
            .ToList();

        if (!string.IsNullOrEmpty(packageIdPrefix))
        {
            packageProducers = [.. packageProducers.Where(p => p.PackageId!.StartsWith(packageIdPrefix, StringComparison.OrdinalIgnoreCase))];
        }

        return packageProducers;
    }

    /// <summary>
    /// Groups producer projects by their package ID (case-insensitive).
    /// </summary>
    /// <param name="packageProducers">The list of package producer projects.</param>
    /// <returns>An enumerable of producer groups indexed by package ID.</returns>
    private static IEnumerable<IGrouping<string, ProjectInfo>> GroupProducersByPackageId(List<ProjectInfo> packageProducers)
    {
        return packageProducers.GroupBy(p => p.PackageId!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates unified package nodes for all producers and adds them to the tree.
    /// </summary>
    /// <param name="unifiedTree">The unified tree to add package nodes to.</param>
    /// <param name="producersByPackageId">The grouped producers by package ID.</param>
    /// <param name="packageIdPrefix">Optional prefix to filter consumed packages.</param>
    private static void CreatePackageNodes(
        UnifiedDependencyTree unifiedTree,
        IEnumerable<IGrouping<string, ProjectInfo>> producersByPackageId,
        string? packageIdPrefix)
    {
        foreach (var producerGroup in producersByPackageId)
        {
            var packageId = producerGroup.Key;
            var producers = producerGroup.ToList();

            var consumedPackages = CollectPackageReferences(producers, producersByPackageId, packageIdPrefix);

            var producerProjects = CreateProducerProjects(producers);

            var node = new UnifiedPackageNode
            {
                PackageId = packageId,
                ProducerProjects = producerProjects,
                ConsumesPackages = consumedPackages
            };

            unifiedTree.AllPackages[packageId] = node;
        }
    }

    /// <summary>
    /// Adds pure consumer projects (projects that don't produce packages) to the unified tree.
    /// </summary>
    /// <param name="unifiedTree">The unified tree to add pure consumer projects to.</param>
    /// <param name="allProjects">All projects in the workspace.</param>
    /// <param name="packageProducers">The list of projects that produce packages.</param>
    /// <param name="packageIdPrefix">Optional prefix to filter consumed packages.</param>
    private void AddPureConsumerProjects(
        UnifiedDependencyTree unifiedTree,
        List<ProjectInfo> allProjects,
        List<ProjectInfo> packageProducers,
        string? packageIdPrefix)
    {
        // Get projects that don't produce packages
        var pureConsumers = allProjects
            .Where(p => string.IsNullOrEmpty(p.PackageId))
            .ToList();

        OnFeedback?.Invoke($"Found {pureConsumers.Count} pure consumer project(s)");

        foreach (var consumer in pureConsumers)
        {
            // Only add if they consume at least one tracked package
            var relevantPackageReferences = consumer.PackageReferences
                .Where(pkgRef =>
                {
                    var isTracked = unifiedTree.AllPackages.ContainsKey(pkgRef.PackageId);
                    var matchesPrefix = string.IsNullOrEmpty(packageIdPrefix) ||
                                       pkgRef.PackageId.StartsWith(packageIdPrefix, StringComparison.OrdinalIgnoreCase);
                    return isTracked && matchesPrefix;
                })
                .ToList();

            if (relevantPackageReferences.Count != 0)
            {
                // Create a virtual package node for this consumer
                var virtualPackageId = $"[Consumer] {Path.GetFileNameWithoutExtension(consumer.FilePath)}";

                var consumedPackages = relevantPackageReferences
                    .GroupBy(pkgRef => pkgRef.PackageId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new PackageConsumption
                    {
                        PackageId = g.Key,
                        Version = g.Select(pr => pr.Version).FirstOrDefault(v => !string.IsNullOrEmpty(v))
                    })
                    .ToList();

                var node = new UnifiedPackageNode
                {
                    PackageId = virtualPackageId,
                    ProducerProjects =
                    [
                        new ProducerProject
                        {
                            ProjectFilePath = consumer.FilePath,
                            ProjectName = Path.GetFileNameWithoutExtension(consumer.FilePath),
                            Version = null,
                            PackageReferences = consumer.PackageReferences
                        }
                    ],
                    ConsumesPackages = consumedPackages
                };

                unifiedTree.AllPackages[virtualPackageId] = node;

                OnFeedback?.Invoke($"  Added pure consumer: {Path.GetFileNameWithoutExtension(consumer.FilePath)} (consumes {consumedPackages.Count} tracked package(s))");
            }
        }
    }

    /// <summary>
    /// Collects all package references from producers, filtering for internal packages and optional prefix.
    /// </summary>
    /// <param name="producers">The list of producer projects.</param>
    /// <param name="producersByPackageId">All producers grouped by package ID for filtering.</param>
    /// <param name="packageIdPrefix">Optional prefix to filter packages.</param>
    /// <returns>A list of unique package consumptions.</returns>
    private static List<PackageConsumption> CollectPackageReferences(
        List<ProjectInfo> producers,
        IEnumerable<IGrouping<string, ProjectInfo>> producersByPackageId,
        string? packageIdPrefix)
    {
        var result = producers
            .SelectMany(p => p.PackageReferences)
            .Where(pkgRef => FilterForPackageId(pkgRef, producersByPackageId))
            .Where(pkgRef => FilterForPrefix(packageIdPrefix, pkgRef))
            .GroupBy(pkgRef => pkgRef.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PackageConsumption
            {
                PackageId = g.Key,
                Version = g.Select(pr => pr.Version).FirstOrDefault(v => !string.IsNullOrEmpty(v))
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// Creates ProducerProject objects from ProjectInfo objects.
    /// </summary>
    /// <param name="producers">The list of producer project info objects.</param>
    /// <returns>A list of ProducerProject objects.</returns>
    private static List<ProducerProject> CreateProducerProjects(List<ProjectInfo> producers)
    {
        return
        [
            .. producers.Select(p => new ProducerProject
            {
                ProjectFilePath = p.FilePath,
                ProjectName = Path.GetFileNameWithoutExtension(p.FilePath),
                Version = p.PackageVersion,
                PackageReferences = p.PackageReferences
            })
        ];
    }

    /// <summary>
    /// Builds the ConsumedBy relationships by analyzing which packages consume each package.
    /// </summary>
    /// <param name="unifiedTree">The unified tree to build relationships for.</param>
    private static void BuildConsumedByRelationships(UnifiedDependencyTree unifiedTree)
    {
        foreach (var node in unifiedTree.AllPackages.Values)
        {
            foreach (var consumedPkg in node.ConsumesPackages)
            {
                if (unifiedTree.AllPackages.TryGetValue(consumedPkg.PackageId, out var consumedPackage))
                {
                    consumedPackage.ConsumedByPackages.Add(node);
                }
            }
        }
    }

    /// <summary>
    /// Identifies root packages (packages that don't consume any other internal packages).
    /// </summary>
    /// <param name="unifiedTree">The unified tree to identify root packages in.</param>
    private static void IdentifyRootPackages(UnifiedDependencyTree unifiedTree)
    {
        unifiedTree.RootPackages = [.. unifiedTree.AllPackages.Values.Where(p => p.ConsumesPackages.Count == 0)];
    }

    /// <summary>
    /// Filters a package reference to include only if it's produced by one of the internal projects.
    /// </summary>
    /// <param name="pkgRef">The package reference to check.</param>
    /// <param name="producersByPackageId">All producers grouped by package ID.</param>
    /// <returns>True if the package is produced internally, otherwise false.</returns>
    private static bool FilterForPackageId(PackageReference pkgRef, IEnumerable<IGrouping<string, ProjectInfo>> producersByPackageId)
        => producersByPackageId.Any(g => g.Key.Equals(pkgRef.PackageId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Filters a package reference based on an optional package ID prefix.
    /// </summary>
    /// <param name="packageIdPrefix">Optional prefix to filter by.</param>
    /// <param name="pkgRef">The package reference to check.</param>
    /// <returns>True if the package passes the prefix filter or no prefix is specified.</returns>
    private static bool FilterForPrefix(string? packageIdPrefix, PackageReference pkgRef)
    {
        if (string.IsNullOrEmpty(packageIdPrefix))
        {
            return true;
        }
        else
        {
            var contains = pkgRef.PackageId.StartsWith(packageIdPrefix, StringComparison.OrdinalIgnoreCase);

            return contains;
        }
    }

    /// <summary>
    /// Filters out leaf producers (packages that neither consume nor are consumed by other packages).
    /// </summary>
    /// <param name="tree">The unified tree to filter.</param>
    /// <returns>A filtered unified tree excluding isolated leaf packages.</returns>
    public static UnifiedDependencyTree FilterLeafProducers(UnifiedDependencyTree tree)
    {
        var filteredTree = new UnifiedDependencyTree
        {
            TotalProjects = tree.TotalProjects,
            ProjectsWithDependencies = tree.ProjectsWithDependencies
        };

        // Only include packages that are either consumed by something OR consume something
        foreach (var package in tree.AllPackages.Values)
        {
            if (package.ConsumedByPackages.Count != 0 || package.ConsumesPackages.Count != 0)
            {
                filteredTree.AllPackages[package.PackageId] = package;
            }
        }

        // Update root packages to only include filtered packages
        filteredTree.RootPackages = [.. filteredTree.AllPackages.Values.Where(p => !p.ConsumesPackages.Any())];

        return filteredTree;
    }
}
