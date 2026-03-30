using System.Xml;
using System.Xml.Linq;
using DoenaSoft.BuildNugetDependencyTree.Models;

namespace DoenaSoft.BuildNugetDependencyTree;

/// <summary>
/// Exports NuGet dependency trees to XML format with various filtering options.
/// </summary>
public sealed class XmlExporter
{
    /// <summary>
    /// Event raised to provide feedback messages during export operations.
    /// </summary>
    public event Action<string>? OnFeedback;

    /// <summary>
    /// Exports a dependency tree to an XML file with package and consumer information.
    /// </summary>
    /// <param name="tree">The dependency tree to export.</param>
    /// <param name="outputPath">The file path where the XML will be saved.</param>
    public void ExportToXml(DependencyTree tree, string outputPath)
    {
        var root = new XElement("NuGetDependencyTree",
            new XAttribute("TotalProjects", tree.TotalProjects),
            new XAttribute("ProjectsWithDependencies", tree.ProjectsWithDependencies),
            new XAttribute("PackagesProduced", tree.PackageNodes.Count)
        );

        foreach (var packageNode in tree.PackageNodes)
        {
            var packageElement = this.CreatePackageElement(packageNode);
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

        OnFeedback?.Invoke($"\nXML exported to: {outputPath}");
    }

    /// <summary>
    /// Creates an XML element for a package node including its producer and consumers.
    /// </summary>
    /// <param name="packageNode">The package node to convert to XML.</param>
    /// <returns>An XElement representing the package.</returns>
    private XElement CreatePackageElement(PackageNode packageNode)
    {
        var packageElement = new XElement("Package",
            new XAttribute("PackageId", packageNode.PackageId),
            new XElement("Producer",
                new XAttribute("ProjectName", packageNode.ProjectName),
                new XAttribute("CsProjPath", packageNode.ProjectFilePath)
            )
        );

        if (packageNode.Consumers.Count != 0)
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

    /// <summary>
    /// Creates an XML element for a consumer node including transitive consumers.
    /// </summary>
    /// <param name="consumerNode">The consumer node to convert to XML.</param>
    /// <returns>An XElement representing the consumer.</returns>
    private static XElement CreateConsumerElement(ConsumerNode consumerNode)
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

    /// <summary>
    /// Exports a unified dependency tree to an XML file with optional filtering.
    /// </summary>
    /// <param name="tree">The unified dependency tree to export.</param>
    /// <param name="outputPath">The file path where the XML will be saved.</param>
    /// <param name="filteredView">Optional filter view identifier to include in the XML.</param>
    public void ExportUnifiedToXml(UnifiedDependencyTree tree, string outputPath, string? filteredView)
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

        OnFeedback?.Invoke($"\nXML exported to: {outputPath}");
    }

    /// <summary>
    /// Exports only packages with version mismatches to an XML file.
    /// Skips export if no mismatches are found.
    /// </summary>
    /// <param name="tree">The unified dependency tree to analyze for mismatches.</param>
    /// <param name="outputPath">The file path where the mismatch XML will be saved.</param>
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
        if (rootPackagesWithMismatches.Count == 0)
        {
            OnFeedback?.Invoke($"\nNo version mismatches found. Skipping mismatch XML export.");
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

        OnFeedback?.Invoke($"Mismatch XML exported to: {outputPath}");
    }

    /// <summary>
    /// Creates an XML element for a package node, including only if it has version mismatches or children with mismatches.
    /// </summary>
    /// <param name="packageNode">The package node to process.</param>
    /// <param name="processedPackages">Set of already processed package IDs to avoid duplicates.</param>
    /// <param name="parentPackage">The parent package node to check for version mismatches.</param>
    /// <returns>An XElement representing the package with mismatches, or null if no mismatches exist.</returns>
    private static XElement? CreateMismatchFilteredElement(UnifiedPackageNode packageNode, HashSet<string> processedPackages, UnifiedPackageNode? parentPackage)
    {
        var (hasMismatch, consumedVersion, producerVersions) = CheckForVersionMismatch(packageNode, parentPackage);

        if (processedPackages.Contains(packageNode.PackageId))
        {
            return HandleAlreadyProcessedPackage(packageNode.PackageId, hasMismatch);
        }

        var childrenWithMismatches = CollectChildrenWithMismatches(packageNode, processedPackages);

        if (!hasMismatch && childrenWithMismatches.Count == 0)
        {
            return null;
        }

        processedPackages.Add(packageNode.PackageId);

        var packageElement = CreateMismatchPackageElement(packageNode, hasMismatch, consumedVersion, producerVersions);

        AddConsumesElementIfNeeded(packageElement, packageNode);

        AddConsumedByElementIfNeeded(packageElement, childrenWithMismatches);

        return packageElement;
    }

    /// <summary>
    /// Checks if there is a version mismatch between the consumed package version and the parent's produced versions.
    /// </summary>
    /// <param name="packageNode">The package node to check.</param>
    /// <param name="parentPackage">The parent package node to compare versions against.</param>
    /// <returns>A tuple containing whether a mismatch exists, the consumed version, and the list of producer versions.</returns>
    private static (bool hasMismatch, string? consumedVersion, List<string> producerVersions) CheckForVersionMismatch(UnifiedPackageNode packageNode, UnifiedPackageNode? parentPackage)
    {
        var hasMismatch = false;
        string? consumedVersion = null;
        List<string> producerVersions = [];

        if (parentPackage != null)
        {
            consumedVersion = packageNode.ConsumesPackages
                .FirstOrDefault(cp => cp.PackageId.Equals(parentPackage.PackageId, StringComparison.OrdinalIgnoreCase))?.Version;

            producerVersions = [.. parentPackage.ProducerProjects
                .Select(p => p.Version)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .Distinct()];

            if (!string.IsNullOrEmpty(consumedVersion) && producerVersions.Any())
            {
                hasMismatch = !producerVersions.Contains(consumedVersion, StringComparer.OrdinalIgnoreCase);
            }
        }

        return (hasMismatch, consumedVersion, producerVersions);
    }

    /// <summary>
    /// Handles packages that have already been processed by returning a reference element if a mismatch exists.
    /// </summary>
    /// <param name="packageId">The ID of the package that was already processed.</param>
    /// <param name="hasMismatch">Whether the package has a version mismatch.</param>
    /// <returns>A PackageReference element if a mismatch exists, otherwise null.</returns>
    private static XElement? HandleAlreadyProcessedPackage(string packageId, bool hasMismatch)
    {
        if (hasMismatch)
        {
            return new XElement("PackageReference",
                new XAttribute("PackageId", packageId),
                new XAttribute("Note", "Already listed above")
            );
        }

        return null;
    }

    /// <summary>
    /// Recursively collects child packages that have version mismatches.
    /// </summary>
    /// <param name="packageNode">The package node whose children to collect.</param>
    /// <param name="processedPackages">Set of already processed package IDs.</param>
    /// <returns>A list of XElements representing children with mismatches.</returns>
    private static List<XElement> CollectChildrenWithMismatches(UnifiedPackageNode packageNode, HashSet<string> processedPackages)
    {
        var childrenWithMismatches = new List<XElement>();

        foreach (var consumer in packageNode.ConsumedByPackages)
        {
            var childElement = CreateMismatchFilteredElement(consumer, processedPackages, packageNode);

            if (childElement != null)
            {
                childrenWithMismatches.Add(childElement);
            }
        }

        return childrenWithMismatches;
    }

    /// <summary>
    /// Creates a package XML element with version mismatch attributes and producer information.
    /// </summary>
    /// <param name="packageNode">The package node to create an element for.</param>
    /// <param name="hasMismatch">Whether the package has a version mismatch.</param>
    /// <param name="consumedVersion">The version consumed by the package.</param>
    /// <param name="producerVersions">The list of versions produced by the parent package.</param>
    /// <returns>An XElement representing the package with mismatch information.</returns>
    private static XElement CreateMismatchPackageElement(UnifiedPackageNode packageNode, bool hasMismatch, string? consumedVersion, List<string> producerVersions)
    {
        var packageElement = new XElement("Package",
            new XAttribute("PackageId", packageNode.PackageId)
        );

        if (hasMismatch)
        {
            packageElement.Add(new XAttribute("ConsumesVersion", consumedVersion!));
            var expectedVersions = string.Join(", ", producerVersions);
            packageElement.Add(new XAttribute("ExpectedVersion", expectedVersions));
        }

        var producersElement = CreateProducersElement(packageNode);
        packageElement.Add(producersElement);

        return packageElement;
    }

    /// <summary>
    /// Creates an XML element containing all producer projects for a package.
    /// </summary>
    /// <param name="packageNode">The package node whose producers to export.</param>
    /// <returns>An XElement containing the Producers section.</returns>
    private static XElement CreateProducersElement(UnifiedPackageNode packageNode)
    {
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
        return producersElement;
    }

    /// <summary>
    /// Adds a Consumes section to the package element if the package consumes other packages.
    /// </summary>
    /// <param name="packageElement">The package element to add the section to.</param>
    /// <param name="packageNode">The package node containing consumed package information.</param>
    private static void AddConsumesElementIfNeeded(XElement packageElement, UnifiedPackageNode packageNode)
    {
        if (packageNode.ConsumesPackages.Count == 0)
        {
            return;
        }

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

    /// <summary>
    /// Adds a ConsumedBy section to the package element if there are children with mismatches.
    /// </summary>
    /// <param name="packageElement">The package element to add the section to.</param>
    /// <param name="childrenWithMismatches">The list of child elements that have mismatches.</param>
    private static void AddConsumedByElementIfNeeded(XElement packageElement, List<XElement> childrenWithMismatches)
    {
        if (childrenWithMismatches.Count == 0)
        {
            return;
        }

        var consumedByElement = new XElement("ConsumedBy");
        foreach (var childElement in childrenWithMismatches)
        {
            consumedByElement.Add(childElement);
        }
        packageElement.Add(consumedByElement);
    }

    /// <summary>
    /// Creates an XML element for a unified package node including producers, consumers, and hierarchy.
    /// </summary>
    /// <param name="packageNode">The package node to convert to XML.</param>
    /// <param name="processedPackages">Set of already processed package IDs to avoid duplicates.</param>
    /// <param name="parentPackage">The parent package node to check for version mismatches.</param>
    /// <returns>An XElement representing the complete package structure.</returns>
    private static XElement CreateUnifiedPackageElement(UnifiedPackageNode packageNode, HashSet<string> processedPackages, UnifiedPackageNode? parentPackage)
    {
        var packageElement = new XElement("Package",
            new XAttribute("PackageId", packageNode.PackageId)
        );

        AddVersionMismatchAttributesIfNeeded(packageElement, packageNode, parentPackage);

        var producersElement = CreateProducersElement(packageNode);

        packageElement.Add(producersElement);

        AddConsumesElementIfNeeded(packageElement, packageNode);

        AddConsumedByHierarchy(packageElement, packageNode, processedPackages);

        return packageElement;
    }

    /// <summary>
    /// Adds version mismatch attributes (ConsumesVersion and ExpectedVersion) to a package element if applicable.
    /// </summary>
    /// <param name="packageElement">The package element to add attributes to.</param>
    /// <param name="packageNode">The package node being processed.</param>
    /// <param name="parentPackage">The parent package node to compare versions against.</param>
    private static void AddVersionMismatchAttributesIfNeeded(XElement packageElement, UnifiedPackageNode packageNode, UnifiedPackageNode? parentPackage)
    {
        if (parentPackage == null)
        {
            return;
        }

        var consumedVersion = packageNode.ConsumesPackages
            .FirstOrDefault(cp => cp.PackageId.Equals(parentPackage.PackageId, StringComparison.OrdinalIgnoreCase))?.Version;

        if (string.IsNullOrEmpty(consumedVersion))
        {
            return;
        }

        packageElement.Add(new XAttribute("ConsumesVersion", consumedVersion));

        var producerVersions = parentPackage.ProducerProjects
            .Select(p => p.Version)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .ToList();

        if (producerVersions.Count != 0 && !producerVersions.Contains(consumedVersion, StringComparer.OrdinalIgnoreCase))
        {
            var expectedVersions = string.Join(", ", producerVersions);

            packageElement.Add(new XAttribute("ExpectedVersion", expectedVersions));
        }
    }

    /// <summary>
    /// Adds a ConsumedBy section with the complete hierarchy of packages that consume this package.
    /// Handles duplicate detection and creates references for already processed packages.
    /// </summary>
    /// <param name="packageElement">The package element to add the section to.</param>
    /// <param name="packageNode">The package node whose consumers to process.</param>
    /// <param name="processedPackages">Set of already processed package IDs to avoid duplicates.</param>
    private static void AddConsumedByHierarchy(XElement packageElement, UnifiedPackageNode packageNode, HashSet<string> processedPackages)
    {
        if (!packageNode.ConsumedByPackages.Any())
        {
            return;
        }

        var consumedByElement = new XElement("ConsumedBy");

        foreach (var consumer in packageNode.ConsumedByPackages)
        {
            if (!processedPackages.Contains(consumer.PackageId))
            {
                processedPackages.Add(consumer.PackageId);

                var childElement = CreateUnifiedPackageElement(consumer, processedPackages, packageNode);

                consumedByElement.Add(childElement);
            }
            else
            {
                consumedByElement.Add(new XElement("PackageReference",
                    new XAttribute("PackageId", consumer.PackageId),
                    new XAttribute("Note", "Already listed above")
                ));
            }
        }

        packageElement.Add(consumedByElement);
    }
}
