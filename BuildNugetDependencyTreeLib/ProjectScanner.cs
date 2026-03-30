using System.Xml.Linq;

namespace BuildNugetDependencyTree;

public class ProjectScanner
{
    public List<ProjectInfo> ScanFolder(string folderPath)
    {
        var projectFiles = Directory.GetFiles(folderPath, "*.csproj", SearchOption.AllDirectories);
        var projects = new List<ProjectInfo>();

        foreach (var projectFile in projectFiles)
        {
            try
            {
                var projectInfo = this.ParseProject(projectFile);
                projects.Add(projectInfo);
            }
            catch (Exception ex)
            {
                OnParseError?.Invoke(projectFile, ex);
            }
        }

        return projects;
    }

    public event Action<string, Exception>? OnParseError;
    public event Action<string>? OnNuspecDetected;

    private ProjectInfo ParseProject(string projectFilePath)
    {
        var doc = XDocument.Load(projectFilePath);
        var projectInfo = new ProjectInfo { FilePath = projectFilePath };

        // Find PackageId or use AssemblyName or project file name as fallback
        var packageId = doc.Descendants()
            .Where(e => e.Name.LocalName == "PackageId")
            .Select(e => e.Value)
            .FirstOrDefault();

        var assemblyName = doc.Descendants()
            .Where(e => e.Name.LocalName == "AssemblyName")
            .Select(e => e.Value)
            .FirstOrDefault();

        // Check if project generates a NuGet package (has GeneratePackageOnBuild or IsPackable)
        var generatePackage = doc.Descendants()
            .Where(e => e.Name.LocalName == "GeneratePackageOnBuild")
            .Select(e => e.Value)
            .FirstOrDefault();

        var isPackable = doc.Descendants()
            .Where(e => e.Name.LocalName == "IsPackable")
            .Select(e => e.Value)
            .FirstOrDefault();

        // Check for PackageProjectUrl as an indicator of NuGet package generation
        var packageProjectUrl = doc.Descendants()
            .Where(e => e.Name.LocalName == "PackageProjectUrl")
            .Select(e => e.Value)
            .FirstOrDefault();

        // Check for .nuspec file in the same directory
        var projectDirectory = Path.GetDirectoryName(projectFilePath)!;

        var (nuspecPackageId, nuspecVersion) = this.GetNuspec(projectFilePath, projectInfo, projectDirectory);

        if (nuspecPackageId == null)
        {
            var parentDir = Directory.GetParent(projectDirectory);

            if (parentDir != null)
            {
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

                if (solutionFiles.Any(s => this.SolutionContainsProject(s, relativeProjectFilePath)))
                {
                    (nuspecPackageId, nuspecVersion) = this.GetNuspec(projectFilePath, projectInfo, parentDir.FullName);
                }
            }
        }

        // Check for NuspecFile property in the project
        var nuspecFileProperty = doc.Descendants()
            .Where(e => e.Name.LocalName == "NuspecFile")
            .Select(e => e.Value)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(nuspecFileProperty) && nuspecPackageId == null)
        {
            var nuspecPath = Path.Combine(projectDirectory, nuspecFileProperty);
            if (File.Exists(nuspecPath))
            {
                try
                {
                    var nuspecDoc = XDocument.Load(nuspecPath);
                    var ns = nuspecDoc.Root?.GetDefaultNamespace();
                    nuspecPackageId = nuspecDoc.Descendants(ns + "id").FirstOrDefault()?.Value;
                    nuspecVersion = nuspecDoc.Descendants(ns + "version").FirstOrDefault()?.Value;
                    projectInfo.NuspecFilePath = nuspecPath;

                    if (!string.IsNullOrEmpty(nuspecPackageId))
                    {
                        var versionInfo = !string.IsNullOrEmpty(nuspecVersion) ? $" (version: {nuspecVersion})" : "";
                        OnNuspecDetected?.Invoke($"Found .nuspec via NuspecFile property in {projectFilePath}: {Path.GetFileName(nuspecPath)} with package ID: {nuspecPackageId}{versionInfo}");
                    }
                }
                catch (Exception ex)
                {
                    OnParseError?.Invoke(nuspecPath, ex);
                }
            }
        }

        // Determine if the project generates a package
        var generatesPackage =
            string.Equals(generatePackage, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(isPackable, "true", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(packageProjectUrl) ||
            nuspecPackageId != null;

        if (generatesPackage)
        {
            // Priority: nuspec ID > csproj PackageId > AssemblyName > project filename
            projectInfo.PackageId = nuspecPackageId ?? packageId ?? assemblyName ?? Path.GetFileNameWithoutExtension(projectFilePath);

            // Extract version information from csproj
            var version = doc.Descendants()
                .Where(e => e.Name.LocalName == "Version" || e.Name.LocalName == "PackageVersion")
                .Select(e => e.Value)
                .FirstOrDefault();

            // Priority: nuspec version > csproj version
            projectInfo.PackageVersion = nuspecVersion ?? version;
        }

        // Find all PackageReference elements with versions
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

        return projectInfo;
    }

    private bool SolutionContainsProject(string solution, string relativeProjectFilePath)
    {
        var contains = File.ReadAllText(solution).Contains($"\"{relativeProjectFilePath}\"");

        return contains;
    }

    private (string? packageId, string? version) GetNuspec(string projectFilePath, ProjectInfo projectInfo, string nuspecSearchDir)
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
                    OnNuspecDetected?.Invoke($"Found .nuspec in {projectFilePath}: {Path.GetFileName(nuspecFiles[0])} with package ID: {nuspecPackageId}{versionInfo}");
                }
            }
            catch (Exception ex)
            {
                OnParseError?.Invoke(nuspecFiles[0], ex);
            }
        }

        return (nuspecPackageId, nuspecVersion);
    }
}
