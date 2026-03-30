using DoenaSoft.BuildNugetDependencyTree.Models;
using System.Xml.Linq;

namespace DoenaSoft.BuildNugetDependencyTree;

/// <summary>
/// Scans .NET project files (.csproj) to extract package and dependency information.
/// </summary>
public static class ProjectScanner
{
    /// <summary>
    /// Scans a folder recursively for all .csproj files and extracts their project information.
    /// </summary>
    /// <param name="folderPath">The root folder path to scan for projects.</param>
    /// <returns>A list of ProjectInfo objects containing information about each project found.</returns>
    public static List<ProjectInfo> ScanFolder(string folderPath)
    {
        var projectFiles = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories);

        var projects = new List<ProjectInfo>();

        foreach (var projectFile in projectFiles)
        {
            try
            {
                var projectInfo = ParseProject(projectFile);
                projects.Add(projectInfo);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(projectFile, ex);
            }
        }

        return projects;
    }

    /// <summary>
    /// Event raised when an error occurs during project scanning or parsing.
    /// </summary>
    public static event Action<string, Exception>? OnError;

    /// <summary>
    /// Event raised to provide feedback messages during scanning operations.
    /// </summary>
    public static event Action<string>? OnFeedback;

    /// <summary>
    /// Parses a .csproj file to extract project information including package generation settings and dependencies.
    /// </summary>
    /// <param name="projectFilePath">The file path to the .csproj file.</param>
    /// <returns>A ProjectInfo object containing the parsed project information.</returns>
    private static ProjectInfo ParseProject(string projectFilePath)
    {
        var doc = XDocument.Load(projectFilePath);

        var projectInfo = new ProjectInfo { FilePath = projectFilePath };

        var projectDirectory = Path.GetDirectoryName(projectFilePath)!;

        var (packageId, assemblyName) = ExtractBasicProjectIdentifiers(doc);

        var packageGenerationSettings = ExtractPackageGenerationSettings(doc);

        var (nuspecPackageId, nuspecVersion) = FindNuspecInformation(projectFilePath, projectInfo, projectDirectory, doc);

        SetPackageInformation(projectInfo, projectFilePath, doc, packageId, assemblyName, packageGenerationSettings, nuspecPackageId, nuspecVersion);

        ExtractPackageReferences(doc, projectInfo);

        return projectInfo;
    }

    /// <summary>
    /// Extracts the PackageId and AssemblyName from the project file.
    /// </summary>
    /// <param name="doc">The XDocument representing the .csproj file.</param>
    /// <returns>A tuple containing the package ID and assembly name.</returns>
    private static (string? packageId, string? assemblyName) ExtractBasicProjectIdentifiers(XDocument doc)
    {
        var packageId = doc.Descendants()
            .Where(e => e.Name.LocalName == "PackageId")
            .Select(e => e.Value)
            .FirstOrDefault();

        var assemblyName = doc.Descendants()
            .Where(e => e.Name.LocalName == "AssemblyName")
            .Select(e => e.Value)
            .FirstOrDefault();

        return (packageId, assemblyName);
    }

    /// <summary>
    /// Extracts package generation settings from the project file.
    /// </summary>
    /// <param name="doc">The XDocument representing the .csproj file.</param>
    /// <returns>A tuple containing GeneratePackageOnBuild, IsPackable, and PackageProjectUrl values.</returns>
    private static (string? generatePackage, string? isPackable, string? packageProjectUrl) ExtractPackageGenerationSettings(XDocument doc)
    {
        var generatePackage = doc.Descendants()
            .Where(e => e.Name.LocalName == "GeneratePackageOnBuild")
            .Select(e => e.Value)
            .FirstOrDefault();

        var isPackable = doc.Descendants()
            .Where(e => e.Name.LocalName == "IsPackable")
            .Select(e => e.Value)
            .FirstOrDefault();

        var packageProjectUrl = doc.Descendants()
            .Where(e => e.Name.LocalName == "PackageProjectUrl")
            .Select(e => e.Value)
            .FirstOrDefault();

        return (generatePackage, isPackable, packageProjectUrl);
    }

    /// <summary>
    /// Searches for a .nuspec file associated with the project in various locations.
    /// </summary>
    /// <param name="projectFilePath">The file path to the .csproj file.</param>
    /// <param name="projectInfo">The ProjectInfo object being populated.</param>
    /// <param name="projectDirectory">The directory containing the project file.</param>
    /// <param name="doc">The XDocument representing the .csproj file.</param>
    /// <returns>A tuple containing the package ID and version from the .nuspec file, if found.</returns>
    private static (string? packageId, string? version) FindNuspecInformation(string projectFilePath, ProjectInfo projectInfo, string projectDirectory, XDocument doc)
    {
        var (nuspecPackageId, nuspecVersion) = GetNuspec(projectFilePath, projectInfo, projectDirectory);

        if (nuspecPackageId == null)
        {
            (nuspecPackageId, nuspecVersion) = SearchNuspecInParentDirectory(projectFilePath, projectInfo, projectDirectory);
        }

        if (nuspecPackageId == null)
        {
            (nuspecPackageId, nuspecVersion) = CheckNuspecFileProperty(projectFilePath, projectInfo, projectDirectory, doc);
        }

        return (nuspecPackageId, nuspecVersion);
    }

    /// <summary>
    /// Searches for a .nuspec file in the parent directory if a solution file is present.
    /// </summary>
    /// <param name="projectFilePath">The file path to the .csproj file.</param>
    /// <param name="projectInfo">The ProjectInfo object being populated.</param>
    /// <param name="projectDirectory">The directory containing the project file.</param>
    /// <returns>A tuple containing the package ID and version from the .nuspec file, if found.</returns>
    private static (string? packageId, string? version) SearchNuspecInParentDirectory(string projectFilePath, ProjectInfo projectInfo, string projectDirectory)
    {
        var parentDir = Directory.GetParent(projectDirectory);
        if (parentDir == null)
        {
            return (null, null);
        }

        var solutionFiles = Directory.GetFiles(parentDir.FullName, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(parentDir.FullName, "*.slnx", SearchOption.TopDirectoryOnly))
            .ToArray();

        var fullProjectFilePath = Path.GetFullPath(projectFilePath);
        var fullParentDirPath = Path.GetFullPath(parentDir.FullName);
        var relativeProjectFilePath = fullProjectFilePath.Replace(fullParentDirPath, "");

        if (relativeProjectFilePath.StartsWith(Path.DirectorySeparatorChar) || relativeProjectFilePath.StartsWith(Path.AltDirectorySeparatorChar))
        {
            relativeProjectFilePath = relativeProjectFilePath.Substring(1);
        }

        if (solutionFiles.Any(s => SolutionContainsProject(s, relativeProjectFilePath)))
        {
            return GetNuspec(projectFilePath, projectInfo, parentDir.FullName);
        }

        return (null, null);
    }

    /// <summary>
    /// Checks if the project file specifies a NuspecFile property and reads it.
    /// </summary>
    /// <param name="projectFilePath">The file path to the .csproj file.</param>
    /// <param name="projectInfo">The ProjectInfo object being populated.</param>
    /// <param name="projectDirectory">The directory containing the project file.</param>
    /// <param name="doc">The XDocument representing the .csproj file.</param>
    /// <returns>A tuple containing the package ID and version from the .nuspec file, if found.</returns>
    private static (string? packageId, string? version) CheckNuspecFileProperty(string projectFilePath, ProjectInfo projectInfo, string projectDirectory, XDocument doc)
    {
        var nuspecFileProperty = doc.Descendants()
            .Where(e => e.Name.LocalName == "NuspecFile")
            .Select(e => e.Value)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(nuspecFileProperty))
        {
            return (null, null);
        }

        var nuspecPath = Path.Combine(projectDirectory, nuspecFileProperty);
        if (!File.Exists(nuspecPath))
        {
            return (null, null);
        }

        try
        {
            var nuspecDoc = XDocument.Load(nuspecPath);
            var ns = nuspecDoc.Root?.GetDefaultNamespace();
            var nuspecPackageId = nuspecDoc.Descendants(ns + "id").FirstOrDefault()?.Value;
            var nuspecVersion = nuspecDoc.Descendants(ns + "version").FirstOrDefault()?.Value;
            projectInfo.NuspecFilePath = nuspecPath;

            if (!string.IsNullOrEmpty(nuspecPackageId))
            {
                var versionInfo = !string.IsNullOrEmpty(nuspecVersion) ? $" (version: {nuspecVersion})" : "";
                OnFeedback?.Invoke($"Found .nuspec via NuspecFile property in {projectFilePath}: {Path.GetFileName(nuspecPath)} with package ID: {nuspecPackageId}{versionInfo}");
            }

            return (nuspecPackageId, nuspecVersion);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(nuspecPath, ex);
            return (null, null);
        }
    }

    /// <summary>
    /// Sets the package ID and version information on the ProjectInfo object.
    /// </summary>
    /// <param name="projectInfo">The ProjectInfo object to populate.</param>
    /// <param name="projectFilePath">The file path to the .csproj file.</param>
    /// <param name="doc">The XDocument representing the .csproj file.</param>
    /// <param name="packageId">The package ID from the project file.</param>
    /// <param name="assemblyName">The assembly name from the project file.</param>
    /// <param name="settings">The package generation settings tuple.</param>
    /// <param name="nuspecPackageId">The package ID from the .nuspec file, if any.</param>
    /// <param name="nuspecVersion">The version from the .nuspec file, if any.</param>
    private static void SetPackageInformation(
        ProjectInfo projectInfo,
        string projectFilePath,
        XDocument doc,
        string? packageId,
        string? assemblyName,
        (string? generatePackage, string? isPackable, string? packageProjectUrl) settings,
        string? nuspecPackageId,
        string? nuspecVersion)
    {
        var generatesPackage =
            string.Equals(settings.generatePackage, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(settings.isPackable, "true", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(settings.packageProjectUrl) ||
            nuspecPackageId != null;

        if (!generatesPackage)
        {
            return;
        }

        projectInfo.PackageId = nuspecPackageId ?? packageId ?? assemblyName ?? Path.GetFileNameWithoutExtension(projectFilePath);

        var csprojVersion = doc.Descendants()
            .Where(e => e.Name.LocalName == "Version" || e.Name.LocalName == "PackageVersion")
            .Select(e => e.Value)
            .FirstOrDefault();

        projectInfo.PackageVersion = nuspecVersion ?? csprojVersion;
    }

    /// <summary>
    /// Extracts all PackageReference elements from the project file.
    /// </summary>
    /// <param name="doc">The XDocument representing the .csproj file.</param>
    /// <param name="projectInfo">The ProjectInfo object to populate with package references.</param>
    private static void ExtractPackageReferences(XDocument doc, ProjectInfo projectInfo)
    {
        var packageReferences = doc.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => new PackageReference
            {
                PackageId = e.Attribute("Include")?.Value ?? e.Attribute("Update")?.Value ?? string.Empty,
                Version = e.Attribute("Version")?.Value ?? e.Element(e.Name.Namespace + "Version")?.Value
            })
            .Where(pr => !string.IsNullOrEmpty(pr.PackageId))
            .ToList();

        projectInfo.PackageReferences = packageReferences;
    }

    /// <summary>
    /// Checks if a solution file contains a reference to a specific project.
    /// </summary>
    /// <param name="solution">The path to the solution file (.sln or .slnx).</param>
    /// <param name="relativeProjectFilePath">The relative path to the project file.</param>
    /// <returns>True if the solution contains the project, otherwise false.</returns>
    private static bool SolutionContainsProject(string solution, string relativeProjectFilePath)
    {
        var contains = File.ReadAllText(solution).Contains($"\"{relativeProjectFilePath}\"");

        return contains;
    }

    /// <summary>
    /// Searches for and reads a .nuspec file in the specified directory.
    /// </summary>
    /// <param name="projectFilePath">The file path to the .csproj file (for logging purposes).</param>
    /// <param name="projectInfo">The ProjectInfo object to update with nuspec file path.</param>
    /// <param name="nuspecSearchDir">The directory to search for .nuspec files.</param>
    /// <returns>A tuple containing the package ID and version from the .nuspec file, if found.</returns>
    private static (string? packageId, string? version) GetNuspec(string projectFilePath, ProjectInfo projectInfo, string nuspecSearchDir)
    {
        var nuspecFiles = Directory.GetFiles(nuspecSearchDir, "*.nuspec", SearchOption.TopDirectoryOnly);
        string? nuspecPackageId = null;
        string? nuspecVersion = null;

        if (nuspecFiles.Length > 0)
        {
            try
            {
                var nuspecDoc = XDocument.Load(nuspecFiles[0]);
                var ns = nuspecDoc.Root?.GetDefaultNamespace();
                nuspecPackageId = nuspecDoc.Descendants(ns + "id").FirstOrDefault()?.Value;
                nuspecVersion = nuspecDoc.Descendants(ns + "version").FirstOrDefault()?.Value;
                projectInfo.NuspecFilePath = nuspecFiles[0];

                if (!string.IsNullOrEmpty(nuspecPackageId))
                {
                    var versionInfo = !string.IsNullOrEmpty(nuspecVersion) ? $" (version: {nuspecVersion})" : "";
                    OnFeedback?.Invoke($"Found .nuspec in {projectFilePath}: {Path.GetFileName(nuspecFiles[0])} with package ID: {nuspecPackageId}{versionInfo}");
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(nuspecFiles[0], ex);
            }
        }

        return (nuspecPackageId, nuspecVersion);
    }
}
