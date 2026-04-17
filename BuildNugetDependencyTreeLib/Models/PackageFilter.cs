namespace DoenaSoft.BuildNugetDependencyTree.Models;

/// <summary />
[Flags]
public enum PackageFilter
{
    /// <summary />
    Undefined,

    /// <summary />
    IncludePureConsumers,

    /// <summary />
    IncludePackageProducers,
}
