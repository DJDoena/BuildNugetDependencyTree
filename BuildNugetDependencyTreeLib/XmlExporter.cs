using System.Xml;
using System.Xml.Linq;

namespace BuildNugetDependencyTree;

public class XmlExporter
{
    public void ExportToXml(DependencyTree tree, string outputPath)
    {
        var root = new XElement("NuGetDependencyTree",
            new XAttribute("TotalProjects", tree.TotalProjects),
            new XAttribute("ProjectsWithDependencies", tree.ProjectsWithDependencies),
            new XAttribute("PackagesProduced", tree.PackageNodes.Count)
        );

        foreach (var packageNode in tree.PackageNodes)
        {
            var packageElement = CreatePackageElement(packageNode);
            root.Add(packageElement);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root
        );

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(outputPath, settings);
        document.Save(writer);

        Console.WriteLine($"\nXML exported to: {outputPath}");
    }

    private XElement CreatePackageElement(PackageNode packageNode)
    {
        var packageElement = new XElement("Package",
            new XAttribute("PackageId", packageNode.PackageId),
            new XElement("Producer",
                new XAttribute("ProjectName", packageNode.ProjectName),
                new XAttribute("CsProjPath", packageNode.ProjectFilePath)
            )
        );

        if (packageNode.Consumers.Any())
        {
            var consumersElement = new XElement("Consumers");
            foreach (var consumer in packageNode.Consumers)
            {
                var consumerElement = CreateConsumerElement(consumer);
                consumersElement.Add(consumerElement);
            }
            packageElement.Add(consumersElement);
        }

        return packageElement;
    }

    private XElement CreateConsumerElement(ConsumerNode consumerNode)
    {
        var consumerElement = new XElement("Consumer",
            new XAttribute("ProjectName", consumerNode.ProjectName),
            new XAttribute("CsProjPath", consumerNode.ProjectFilePath),
            new XAttribute("GeneratesPackage", consumerNode.GeneratesPackage)
        );

        if (consumerNode.GeneratesPackage && !string.IsNullOrEmpty(consumerNode.GeneratedPackageId))
        {
            consumerElement.Add(new XAttribute("GeneratedPackageId", consumerNode.GeneratedPackageId));
        }

        if (consumerNode.TransitiveConsumers.Any())
        {
            var transitiveConsumersElement = new XElement("TransitiveConsumers");
            foreach (var transitive in consumerNode.TransitiveConsumers)
            {
                var transitiveElement = CreateConsumerElement(transitive);
                transitiveConsumersElement.Add(transitiveElement);
            }
            consumerElement.Add(transitiveConsumersElement);
        }

        return consumerElement;
    }

    public void ExportUnifiedToXml(UnifiedDependencyTree tree, string outputPath, string? filteredView = null)
    {
        var root = new XElement("NuGetDependencyTree");

        if (!string.IsNullOrEmpty(filteredView))
        {
            root.Add(new XAttribute("FilteredView", filteredView));
        }

        var processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Start with root packages and build hierarchy
        foreach (var rootPackage in tree.RootPackages)
        {
            var packageElement = CreateUnifiedPackageElement(rootPackage, processedPackages, null);
            root.Add(packageElement);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root
        );

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(outputPath, settings);
        document.Save(writer);

        Console.WriteLine($"\nXML exported to: {outputPath}");
    }

    public void ExportMismatchesToXml(UnifiedDependencyTree tree, string outputPath)
    {
        var processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Start with root packages and build hierarchy, filtering for mismatches
        var rootPackagesWithMismatches = new List<XElement>();
        foreach (var rootPackage in tree.RootPackages)
        {
            var packageElement = CreateMismatchFilteredElement(rootPackage, processedPackages, null);
            if (packageElement != null)
            {
                rootPackagesWithMismatches.Add(packageElement);
            }
        }

        // Only create the file if there are mismatches
        if (!rootPackagesWithMismatches.Any())
        {
            Console.WriteLine($"\nNo version mismatches found. Skipping mismatch XML export.");
            return;
        }

        var root = new XElement("NuGetDependencyTree",
            new XAttribute("FilteredView", "VersionMismatchesOnly")
        );

        foreach (var packageElement in rootPackagesWithMismatches)
        {
            root.Add(packageElement);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root
        );

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(outputPath, settings);
        document.Save(writer);

        Console.WriteLine($"Mismatch XML exported to: {outputPath}");
    }

    private XElement? CreateMismatchFilteredElement(UnifiedPackageNode packageNode, HashSet<string> processedPackages, UnifiedPackageNode? parentPackage)
    {
        // Check if this package has a version mismatch with its parent
        bool hasMismatch = false;
        string? consumedVersion = null;
        List<string> producerVersions = new();

        if (parentPackage != null)
        {
            consumedVersion = packageNode.ConsumesPackages
                .FirstOrDefault(cp => cp.PackageId.Equals(parentPackage.PackageId, StringComparison.OrdinalIgnoreCase))?.Version;

            producerVersions = parentPackage.ProducerProjects
                .Select(p => p.Version)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .ToList();

            if (!string.IsNullOrEmpty(consumedVersion) && producerVersions.Any())
            {
                hasMismatch = !producerVersions.Contains(consumedVersion, StringComparer.OrdinalIgnoreCase);
            }
        }

        // Check if already processed
        if (processedPackages.Contains(packageNode.PackageId))
        {
            // If this package was already included and has a mismatch, return a reference
            if (hasMismatch)
            {
                return new XElement("PackageReference",
                    new XAttribute("PackageId", packageNode.PackageId),
                    new XAttribute("Note", "Already listed above")
                );
            }
            return null;
        }

        // Process children first to see if any have mismatches
        var childrenWithMismatches = new List<XElement>();
        foreach (var consumer in packageNode.ConsumedByPackages)
        {
            var childElement = CreateMismatchFilteredElement(consumer, processedPackages, packageNode);
            if (childElement != null)
            {
                childrenWithMismatches.Add(childElement);
            }
        }

        // Include this node if it has a mismatch OR if any children have mismatches
        if (!hasMismatch && !childrenWithMismatches.Any())
        {
            return null;
        }

        processedPackages.Add(packageNode.PackageId);

        var packageElement = new XElement("Package",
            new XAttribute("PackageId", packageNode.PackageId)
        );

        // Add version mismatch attributes if applicable
        if (hasMismatch)
        {
            packageElement.Add(new XAttribute("ConsumesVersion", consumedVersion!));
            var expectedVersions = string.Join(", ", producerVersions);
            packageElement.Add(new XAttribute("ExpectedVersion", expectedVersions));
        }

        // Add producer projects
        var producersElement = new XElement("Producers");
        foreach (var producer in packageNode.ProducerProjects)
        {
            var producerElement = new XElement("Producer",
                new XAttribute("ProjectName", producer.ProjectName),
                new XAttribute("CsProjPath", producer.ProjectFilePath)
            );

            if (!string.IsNullOrEmpty(producer.Version))
            {
                producerElement.Add(new XAttribute("Version", producer.Version));
            }

            producersElement.Add(producerElement);
        }
        packageElement.Add(producersElement);

        // Add consumed packages info
        if (packageNode.ConsumesPackages.Any())
        {
            var consumesElement = new XElement("Consumes");
            foreach (var consumedPkg in packageNode.ConsumesPackages)
            {
                var pkgRefElement = new XElement("PackageReference",
                    new XAttribute("PackageId", consumedPkg.PackageId)
                );

                if (!string.IsNullOrEmpty(consumedPkg.Version))
                {
                    pkgRefElement.Add(new XAttribute("Version", consumedPkg.Version));
                }

                consumesElement.Add(pkgRefElement);
            }
            packageElement.Add(consumesElement);
        }

        // Add filtered children
        if (childrenWithMismatches.Any())
        {
            var consumedByElement = new XElement("ConsumedBy");
            foreach (var childElement in childrenWithMismatches)
            {
                consumedByElement.Add(childElement);
            }
            packageElement.Add(consumedByElement);
        }

        return packageElement;
    }

    private XElement CreateUnifiedPackageElement(UnifiedPackageNode packageNode, HashSet<string> processedPackages, UnifiedPackageNode? parentPackage)
    {
        var packageElement = new XElement("Package",
            new XAttribute("PackageId", packageNode.PackageId)
        );

        // Check for version mismatch if this is a consumer of a parent package
        if (parentPackage != null)
        {
            var consumedVersion = packageNode.ConsumesPackages
                .FirstOrDefault(cp => cp.PackageId.Equals(parentPackage.PackageId, StringComparison.OrdinalIgnoreCase))?.Version;

            var producerVersions = parentPackage.ProducerProjects
                .Select(p => p.Version)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .ToList();

            // Add consumed version to package element
            if (!string.IsNullOrEmpty(consumedVersion))
            {
                packageElement.Add(new XAttribute("ConsumesVersion", consumedVersion));

                // Check if consumed version differs from any producer version
                if (producerVersions.Any() && !producerVersions.Contains(consumedVersion, StringComparer.OrdinalIgnoreCase))
                {
                    var expectedVersions = string.Join(", ", producerVersions);
                    packageElement.Add(new XAttribute("ExpectedVersion", expectedVersions));
                }
            }
        }

        // Add producer projects
        var producersElement = new XElement("Producers");
        foreach (var producer in packageNode.ProducerProjects)
        {
            var producerElement = new XElement("Producer",
                new XAttribute("ProjectName", producer.ProjectName),
                new XAttribute("CsProjPath", producer.ProjectFilePath)
            );

            if (!string.IsNullOrEmpty(producer.Version))
            {
                producerElement.Add(new XAttribute("Version", producer.Version));
            }

            producersElement.Add(producerElement);
        }
        packageElement.Add(producersElement);

        // Add consumed packages info
        if (packageNode.ConsumesPackages.Any())
        {
            var consumesElement = new XElement("Consumes");
            foreach (var consumedPkg in packageNode.ConsumesPackages)
            {
                var pkgRefElement = new XElement("PackageReference",
                    new XAttribute("PackageId", consumedPkg.PackageId)
                );

                if (!string.IsNullOrEmpty(consumedPkg.Version))
                {
                    pkgRefElement.Add(new XAttribute("Version", consumedPkg.Version));
                }

                consumesElement.Add(pkgRefElement);
            }
            packageElement.Add(consumesElement);
        }

        // Add packages that consume this package (children in the hierarchy)
        if (packageNode.ConsumedByPackages.Any())
        {
            var consumedByElement = new XElement("ConsumedBy");
            foreach (var consumer in packageNode.ConsumedByPackages)
            {
                // Only process if not already processed to avoid duplicates
                if (!processedPackages.Contains(consumer.PackageId))
                {
                    processedPackages.Add(consumer.PackageId);
                    var childElement = CreateUnifiedPackageElement(consumer, processedPackages, packageNode);
                    consumedByElement.Add(childElement);
                }
                else
                {
                    // Add a reference to already processed package
                    consumedByElement.Add(new XElement("PackageReference",
                        new XAttribute("PackageId", consumer.PackageId),
                        new XAttribute("Note", "Already listed above")
                    ));
                }
            }
            packageElement.Add(consumedByElement);
        }

        return packageElement;
    }
}
