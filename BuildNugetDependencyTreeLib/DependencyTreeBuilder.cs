namespace BuildNugetDependencyTree;

public class DependencyTreeBuilder
{
    public DependencyTree BuildTree(List<ProjectInfo> projects)
    {
        var tree = new DependencyTree
        {
            TotalProjects = projects.Count,
            ProjectsWithDependencies = projects.Count(p => p.PackageReferences.Any())
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

    public int CountAllRelationships(DependencyTree tree)
    {
        int count = 0;
        foreach (var node in tree.PackageNodes)
        {
            count += CountNodeRelationships(node);
        }
        return count;
    }

    private void BuildConsumerTree(PackageNode parentNode, ProjectInfo producerProject, List<ProjectInfo> allProjects, HashSet<string> visitedProjects)
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

    private void BuildTransitiveConsumers(ConsumerNode parentConsumer, ProjectInfo consumerProject, List<ProjectInfo> allProjects, HashSet<string> visitedProjects)
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

    private int CountNodeRelationships(PackageNode node)
    {
        int count = node.Consumers.Count;
        foreach (var consumer in node.Consumers)
        {
            count += CountConsumerRelationships(consumer);
        }
        return count;
    }

    private int CountConsumerRelationships(ConsumerNode node)
    {
        int count = node.TransitiveConsumers.Count;
        foreach (var transitive in node.TransitiveConsumers)
        {
            count += CountConsumerRelationships(transitive);
        }
        return count;
    }

    public DependencyTree FilterTreeForProducersAndConsumers(DependencyTree tree)
    {
        var filteredTree = new DependencyTree
        {
            TotalProjects = tree.TotalProjects,
            ProjectsWithDependencies = tree.ProjectsWithDependencies
        };

        foreach (var packageNode in tree.PackageNodes)
        {
            // Only include packages that have consumers
            if (packageNode.Consumers.Any())
            {
                var filteredNode = new PackageNode
                {
                    PackageId = packageNode.PackageId,
                    ProjectFilePath = packageNode.ProjectFilePath,
                    ProjectName = packageNode.ProjectName
                };

                // Filter consumers to only include those that generate packages or have transitive consumers that do
                filteredNode.Consumers = FilterConsumers(packageNode.Consumers);

                // Only add the package if it still has consumers after filtering
                if (filteredNode.Consumers.Any())
                {
                    filteredTree.PackageNodes.Add(filteredNode);
                }
            }
        }

        return filteredTree;
    }

    private List<ConsumerNode> FilterConsumers(List<ConsumerNode> consumers)
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
                    GeneratedPackageId = consumer.GeneratedPackageId
                };

                // Recursively filter transitive consumers
                filteredConsumer.TransitiveConsumers = FilterConsumers(consumer.TransitiveConsumers);

                filteredConsumers.Add(filteredConsumer);
            }
            // Also check if consumer has transitive consumers that generate packages
            else if (consumer.TransitiveConsumers.Any())
            {
                var filteredTransitives = FilterConsumers(consumer.TransitiveConsumers);
                if (filteredTransitives.Any())
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

    public UnifiedDependencyTree BuildUnifiedTree(List<ProjectInfo> projects)
    {
        var unifiedTree = new UnifiedDependencyTree
        {
            TotalProjects = projects.Count,
            ProjectsWithDependencies = projects.Count(p => p.PackageReferences.Any())
        };

        var packageProducers = projects.Where(p => !string.IsNullOrEmpty(p.PackageId)).ToList();

        // Group producers by PackageId to handle multiple projects producing the same package
        var producersByPackageId = packageProducers
            .GroupBy(p => p.PackageId!, StringComparer.OrdinalIgnoreCase);

        // First, create nodes for all packages
        foreach (var producerGroup in producersByPackageId)
        {
            var packageId = producerGroup.Key;
            var producers = producerGroup.ToList();

            // Collect all package references from all producers with version information
            var allPackageReferences = producers
                .SelectMany(p => p.PackageReferences)
                .Where(pkgRef => producersByPackageId.Any(g => g.Key.Equals(pkgRef.PackageId, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(pkgRef => pkgRef.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new PackageConsumption
                {
                    PackageId = g.Key,
                    Version = g.Select(pr => pr.Version).FirstOrDefault(v => !string.IsNullOrEmpty(v))
                })
                .ToList();

            var node = new UnifiedPackageNode
            {
                PackageId = packageId,
                ProducerProjects = producers.Select(p => new ProducerProject
                {
                    ProjectFilePath = p.FilePath,
                    ProjectName = Path.GetFileNameWithoutExtension(p.FilePath),
                    Version = p.PackageVersion,
                    PackageReferences = p.PackageReferences
                }).ToList(),
                ConsumesPackages = allPackageReferences
            };

            unifiedTree.AllPackages[packageId] = node;
        }

        // Build the ConsumedBy relationships
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

        // Identify root packages (packages that don't consume any other packages in the tree)
        unifiedTree.RootPackages = unifiedTree.AllPackages.Values
            .Where(p => !p.ConsumesPackages.Any())
            .ToList();

        return unifiedTree;
    }

    public UnifiedDependencyTree FilterLeafProducers(UnifiedDependencyTree tree)
    {
        var filteredTree = new UnifiedDependencyTree
        {
            TotalProjects = tree.TotalProjects,
            ProjectsWithDependencies = tree.ProjectsWithDependencies
        };

        // Only include packages that are either consumed by something OR consume something
        foreach (var package in tree.AllPackages.Values)
        {
            if (package.ConsumedByPackages.Any() || package.ConsumesPackages.Any())
            {
                filteredTree.AllPackages[package.PackageId] = package;
            }
        }

        // Update root packages to only include filtered packages
        filteredTree.RootPackages = filteredTree.AllPackages.Values
            .Where(p => !p.ConsumesPackages.Any())
            .ToList();

        return filteredTree;
    }
}
