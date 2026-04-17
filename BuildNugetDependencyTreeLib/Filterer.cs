using DoenaSoft.BuildNugetDependencyTree.Models;

namespace DoenaSoft.BuildNugetDependencyTree;

/// <summary />
public static class Filterer
{
    /// <summary />
    /// <param name="projects" />
    /// <param name="packageIdPrefix" />
    /// <param name="packageFilter" />
    /// <returns />
    public static IEnumerable<UnifiedPackageNode> FilterForDirectVersionMismatches(this IEnumerable<UnifiedPackageNode> projects
        , string? packageIdPrefix = null
        , PackageFilter packageFilter = PackageFilter.IncludePackageProducers | PackageFilter.IncludePureConsumers)
    {
        var filtered = projects;

        if (!string.IsNullOrEmpty(packageIdPrefix))
        {
            filtered = filtered.Where(p => p.PackageId.StartsWith(packageIdPrefix, StringComparison.OrdinalIgnoreCase));
        }

        filtered = projects
            .Select(p => CreateForOnlyMismatches(p, packageFilter))
            .Where(p => p.ConsumedByPackages.Count > 0);

        return filtered;
    }

    private static UnifiedPackageNode CreateForOnlyMismatches(UnifiedPackageNode package
        , PackageFilter packageFilter)
    {
        var result = new UnifiedPackageNode()
        {
            PackageId = package.PackageId,
            ProducerProjects = CleanProducerProjects(package),
            ConsumesPackages = null!,
            ConsumedByPackages = [.. FilterConsumedByPackages(package, packageFilter)],
        };

        return result;
    }

    private static List<ProducerProject> CleanProducerProjects(UnifiedPackageNode package)
    {
        var result = package.ProducerProjects
            .Select(p => new ProducerProject()
            {
                ProjectFilePath = p.ProjectFilePath,
                ProjectName = p.ProjectName,
                Version = p.Version,
                PackageReferences = null!,
            })
            .ToList();

        return result;
    }

    private static IEnumerable<UnifiedPackageNode> FilterConsumedByPackages(UnifiedPackageNode package
        , PackageFilter packageFilter)
    {
        if (package.ConsumedByPackages is null || package.ConsumedByPackages.Count == 0)
        {
            yield break;
        }

        foreach (var consumer in package.ConsumedByPackages)
        {
            var producesPackage = consumer.ProducerProjects is not null && consumer.ProducerProjects.Count > 0;

            if (!packageFilter.HasFlag(PackageFilter.IncludePureConsumers) && !producesPackage)
            {
                continue;
            }

            if (!packageFilter.HasFlag(PackageFilter.IncludePackageProducers) && producesPackage)
            {
                continue;
            }

            var consumesPackage = consumer.ConsumesPackages.First(p => p.PackageId == package.PackageId);

            if (consumesPackage.Version != package.ProducerProjects[0].Version)
            {
                var resultConsumesPackage = new PackageConsumption()
                {
                    PackageId = consumesPackage.PackageId,
                    Version = consumesPackage.Version,
                    ExpectedVersion = package.ProducerProjects[0].Version,
                };

                var resultConsumer = new UnifiedPackageNode()
                {
                    PackageId = consumer.PackageId,
                    ConsumesPackages = [resultConsumesPackage],
                    ConsumedByPackages = null!,
                };

                yield return resultConsumer;
            }
        }
    }
}
