[CmdletBinding()]
param(
    [string]$VersionsPath = "release/PackageVersions.props",
    [string]$Configuration = "Release",
    [string]$Version,
    [string]$OutputPath,
    [string]$DependencyOutputPath,
    [string]$ArtifactsPath = "artifacts/cli-bundled-tools",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$hostProjectPath = Join-Path $repoRoot "cli/Reimaginate.DataHub.CLI/Reimaginate.DataHub.CLI.csproj"
$toolsProjectPath = Join-Path $repoRoot "cli/Reimaginate.DataHub.CLI.Tools/Reimaginate.DataHub.CLI.Tools.csproj"
$toolsPackageId = "Reimaginate.DataHub.CLI.Tools"
$thirdPartyLicencesPath = Join-Path $repoRoot "licenses/third-party"
$thirdPartyCopyrightOverrides = @{
    "ClosedXML/0.105.0" = "Copyright (c) 2016 ClosedXML"
    "ClosedXML.Parser/2.0.0" = "Copyright (c) 2023, Jan Havlíček"
    "ExcelNumberFormat/1.1.0" = "Copyright (c) 2017 andersnm"
    "OneOf/3.0.271" = "Copyright (c) 2016 Harry McIntyre"
}
$hostProvidedPackageIds = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        "Reimaginate.CLI.Base",
        "System.CommandLine",
        "Microsoft.Extensions.Configuration.Abstractions",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Logging.Abstractions",
        "Reimaginate.Mediator.Abstractions",
        "System.Text.Json"
    ),
    [StringComparer]::OrdinalIgnoreCase)

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$versionsXmlForDefault = Get-Content -Path (Join-Path $repoRoot $VersionsPath) -Raw
    $versionNode = $versionsXmlForDefault.SelectSingleNode("//PropertyGroup[@Condition=""'`$(MSBuildProjectName)' == '$toolsPackageId'""]/VersionPrefix")
    if ($null -ne $versionNode) {
        $Version = [string]$versionNode.InnerText
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version is required and could not be inferred from '$VersionsPath'."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    throw "OutputPath is required."
}

if ([string]::IsNullOrWhiteSpace($DependencyOutputPath)) {
    $DependencyOutputPath = Join-Path $OutputPath "dependencies"
}

function Resolve-RepoPath {
    param([Parameter(Mandatory)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-ProjectTargetFramework {
    param([Parameter(Mandatory)][string]$ProjectFilePath)

    [xml]$projectXml = Get-Content -Path $ProjectFilePath -Raw
    $targetFramework = [string]$projectXml.SelectSingleNode("//TargetFramework").InnerText
    if ([string]::IsNullOrWhiteSpace($targetFramework)) {
        throw "Could not determine the target framework from '$ProjectFilePath'."
    }

    return $targetFramework
}

function Get-PublicPackages {
    param([Parameter(Mandatory)][string]$VersionsFilePath)

    [xml]$versionsXml = Get-Content -Path $VersionsFilePath -Raw
    $packages = [System.Collections.Specialized.OrderedDictionary]::new()

    foreach ($node in $versionsXml.SelectNodes("//DataHubPublicPackage")) {
        $packageId = [string]$node.Include
        $projectPath = [string]$node.ProjectPath
        if ([string]::IsNullOrWhiteSpace($packageId) -or [string]::IsNullOrWhiteSpace($projectPath)) {
            continue
        }

        $packages[$packageId] = [pscustomobject]@{
            PackageId = $packageId
            ProjectPath = $projectPath
            FullProjectPath = Resolve-RepoPath -Path $projectPath
        }
    }

    return $packages
}

function Get-GlobalPackagesDirectory {
    if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        return Join-Path $env:USERPROFILE ".nuget/packages"
    }

    return $env:NUGET_PACKAGES
}

function Get-PackageCacheDirectory {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion
    )

    return Join-Path (Get-GlobalPackagesDirectory) (Join-Path $PackageId.ToLowerInvariant() $PackageVersion.ToLowerInvariant())
}

function Get-PackageFilePath {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion
    )

    $packageDirectory = Get-PackageCacheDirectory -PackageId $PackageId -PackageVersion $PackageVersion
    $packageFileName = "$($PackageId.ToLowerInvariant()).$($PackageVersion.ToLowerInvariant()).nupkg"
    return Join-Path $packageDirectory $packageFileName
}

function Get-PackageNoticeFiles {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion
    )

    $packageDirectory = Get-PackageCacheDirectory -PackageId $PackageId -PackageVersion $PackageVersion
    if (-not (Test-Path -LiteralPath $packageDirectory -PathType Container)) {
        throw "Package '$PackageId' version '$PackageVersion' was not found at '$packageDirectory'. Run restore before staging bundled CLI tools."
    }

    $noticeFiles = [System.Collections.Generic.List[object]]::new()
    foreach ($noticeFile in @(
        Get-ChildItem -LiteralPath $packageDirectory -File -Recurse -ErrorAction Stop |
            Where-Object {
                $_.Name -match '^(?:NOTICES?|THIRD[-_. ]?PARTY[-_. ]?NOTICES?)(?:\.[^.]+)?$'
            } |
            Sort-Object FullName
    )) {
        $noticeBytes = [System.IO.File]::ReadAllBytes($noticeFile.FullName)
        $noticeFiles.Add([pscustomobject]@{
            RelativePath = [System.IO.Path]::GetRelativePath(
                $packageDirectory,
                $noticeFile.FullName
            ).Replace("\", "/")
            Hash = [Convert]::ToHexString(
                [System.Security.Cryptography.SHA256]::HashData($noticeBytes)
            ).ToLowerInvariant()
            Text = [System.IO.File]::ReadAllText($noticeFile.FullName).Trim()
        })
    }

    return $noticeFiles.ToArray()
}

function Copy-PackageFromGlobalCache {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$DestinationDirectory
    )

    $packageFilePath = Get-PackageFilePath -PackageId $PackageId -PackageVersion $PackageVersion
    $packageFileName = [System.IO.Path]::GetFileName($packageFilePath)

    if (-not (Test-Path -Path $packageFilePath)) {
        throw "Package '$PackageId' version '$PackageVersion' was not found in the NuGet global packages folder at '$packageFilePath'. Run restore before staging bundled CLI tools."
    }

    Copy-Item -Path $packageFilePath -Destination (Join-Path $DestinationDirectory $packageFileName) -Force
}

function Get-OptionalXmlValue {
    param([System.Xml.XmlNode]$Node)

    if ($null -eq $Node) {
        return $null
    }

    $value = [string]$Node.InnerText
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value.Trim()
}

function Get-PackageLicenceText {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$LicenceFile
    )

    $packageDirectory = Get-PackageCacheDirectory -PackageId $PackageId -PackageVersion $PackageVersion
    $unpackedLicencePath = Join-Path $packageDirectory $LicenceFile
    if (Test-Path -LiteralPath $unpackedLicencePath) {
        return (Get-Content -LiteralPath $unpackedLicencePath -Raw).Trim()
    }

    $packageFilePath = Get-PackageFilePath -PackageId $PackageId -PackageVersion $PackageVersion
    if (-not (Test-Path -LiteralPath $packageFilePath)) {
        throw "Package '$PackageId' version '$PackageVersion' was not found at '$packageFilePath'. Run restore before staging bundled CLI tools."
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($packageFilePath)
    try {
        $licenceEntry = $archive.Entries |
            Where-Object { [string]::Equals($_.FullName, $LicenceFile, [StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1

        if ($null -eq $licenceEntry) {
            throw "Package '$PackageId' version '$PackageVersion' declares licence file '$LicenceFile', but the file was not found in the package."
        }

        $reader = [System.IO.StreamReader]::new($licenceEntry.Open())
        try {
            return $reader.ReadToEnd().Trim()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-ThirdPartyPackageMetadata {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion
    )

    $packageDirectory = Get-PackageCacheDirectory -PackageId $PackageId -PackageVersion $PackageVersion
    $nuspecPath = Join-Path $packageDirectory "$($PackageId.ToLowerInvariant()).nuspec"
    if (-not (Test-Path -LiteralPath $nuspecPath)) {
        $nuspecPath = Get-ChildItem -LiteralPath $packageDirectory -Filter "*.nuspec" -File -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }

    if ([string]::IsNullOrWhiteSpace($nuspecPath) -or -not (Test-Path -LiteralPath $nuspecPath)) {
        throw "Package '$PackageId' version '$PackageVersion' does not have nuspec metadata in '$packageDirectory'. Run restore before staging bundled CLI tools."
    }

    [xml]$nuspecXml = Get-Content -LiteralPath $nuspecPath -Raw
    $metadataNode = $nuspecXml.SelectSingleNode("//*[local-name()='metadata']")
    if ($null -eq $metadataNode) {
        throw "Package '$PackageId' version '$PackageVersion' does not contain nuspec metadata."
    }

    $licenceNode = $metadataNode.SelectSingleNode("*[local-name()='license']")
    $licenceType = if ($null -eq $licenceNode -or $null -eq $licenceNode.Attributes["type"]) {
        $null
    }
    else {
        [string]$licenceNode.Attributes["type"].Value
    }
    $licenceValue = Get-OptionalXmlValue -Node $licenceNode
    $licenceUrl = Get-OptionalXmlValue -Node $metadataNode.SelectSingleNode("*[local-name()='licenseUrl']")
    $selectedLicence = $null
    $packageLicenceText = $null
    $overrideKey = "$PackageId/$PackageVersion"

    if ([string]::Equals($overrideKey, "OneOf/3.0.271", [StringComparison]::OrdinalIgnoreCase)) {
        $selectedLicence = "MIT"
        $licenceValue = "MIT (reviewed legacy metadata override)"
        if ([string]::IsNullOrWhiteSpace($licenceUrl)) {
            $licenceUrl = "https://licenses.nuget.org/MIT"
        }
    }
    elseif ([string]::Equals($licenceType, "file", [StringComparison]::OrdinalIgnoreCase)) {
        $selectedLicence = "Package-specific"
        $packageLicenceText = Get-PackageLicenceText -PackageId $PackageId -PackageVersion $PackageVersion -LicenceFile $licenceValue
    }
    elseif ([string]::Equals($licenceValue, "MIT", [StringComparison]::OrdinalIgnoreCase)) {
        $selectedLicence = "MIT"
    }
    elseif ([string]::Equals($licenceValue, "Apache-2.0", [StringComparison]::OrdinalIgnoreCase)) {
        $selectedLicence = "Apache-2.0"
    }
    elseif ([string]::Equals($licenceValue, "BSD-3-Clause", [StringComparison]::OrdinalIgnoreCase)) {
        $selectedLicence = "BSD-3-Clause"
    }
    elseif ([string]::Equals($licenceValue, "MS-PL OR Apache-2.0", [StringComparison]::OrdinalIgnoreCase)) {
        $selectedLicence = "Apache-2.0"
    }
    else {
        $declaredLicence = if ([string]::IsNullOrWhiteSpace($licenceValue)) { "<missing>" } else { $licenceValue }
        throw "Package '$PackageId' version '$PackageVersion' declares unsupported licence '$declaredLicence'. Review the licence and add an explicit selection before redistributing it in the CLI package."
    }

    $repositoryNode = $metadataNode.SelectSingleNode("*[local-name()='repository']")
    $repositoryUrl = if ($null -eq $repositoryNode -or $null -eq $repositoryNode.Attributes["url"]) {
        $null
    }
    else {
        [string]$repositoryNode.Attributes["url"].Value
    }

    $copyright = Get-OptionalXmlValue -Node $metadataNode.SelectSingleNode("*[local-name()='copyright']")
    if ($thirdPartyCopyrightOverrides.ContainsKey($overrideKey)) {
        $copyright = [string]$thirdPartyCopyrightOverrides[$overrideKey]
    }

    if ($selectedLicence -in @("MIT", "BSD-3-Clause") -and [string]::IsNullOrWhiteSpace($copyright)) {
        throw "Package '$PackageId' version '$PackageVersion' requires a copyright notice for '$selectedLicence', but its nuspec does not provide one. Add a reviewed package/version copyright override before redistributing it."
    }

    return [pscustomobject]@{
        PackageId = $PackageId
        Version = $PackageVersion
        Authors = Get-OptionalXmlValue -Node $metadataNode.SelectSingleNode("*[local-name()='authors']")
        Copyright = $copyright
        DeclaredLicence = $licenceValue
        SelectedLicence = $selectedLicence
        LicenceUrl = $licenceUrl
        ProjectUrl = Get-OptionalXmlValue -Node $metadataNode.SelectSingleNode("*[local-name()='projectUrl']")
        RepositoryUrl = $repositoryUrl
        PackageLicenceText = $packageLicenceText
        NoticeFiles = @(
            Get-PackageNoticeFiles -PackageId $PackageId -PackageVersion $PackageVersion
        )
    }
}

function Write-ThirdPartyNotices {
    param(
        [Parameter(Mandatory)][hashtable]$PackageIdentities,
        [Parameter(Mandatory)][string]$OutputFilePath
    )

    $metadata = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $PackageIdentities.GetEnumerator()) {
        $packageId = [string]$entry.Value.PackageId
        if ($packageId.StartsWith("Reimaginate.DataHub", [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $metadata.Add((Get-ThirdPartyPackageMetadata -PackageId $packageId -PackageVersion ([string]$entry.Value.Version)))
    }

    $orderedMetadata = @($metadata | Sort-Object PackageId, Version)
    if ($orderedMetadata.Count -eq 0) {
        throw "No third-party package metadata was found for the bundled CLI."
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("DATAHUB CLI THIRD-PARTY NOTICES")
    $lines.Add("")
    $lines.Add("The DataHub CLI package includes third-party components. Those components are not")
    $lines.Add("licensed under the DataHub Business Source License and remain subject to the")
    $lines.Add("licence terms identified below.")
    $lines.Add("")
    $lines.Add("COMPONENT INVENTORY")

    foreach ($package in $orderedMetadata) {
        $lines.Add("")
        $lines.Add("-------------------------------------------------------------------------------")
        $lines.Add("Package: $($package.PackageId)")
        $lines.Add("Version: $($package.Version)")
        foreach ($field in @(
            [pscustomobject]@{ Label = "Authors"; Value = $package.Authors },
            [pscustomobject]@{ Label = "Copyright"; Value = $package.Copyright },
            [pscustomobject]@{ Label = "Declared licence"; Value = $package.DeclaredLicence }
        )) {
            if (-not [string]::IsNullOrWhiteSpace($field.Value)) {
                $lines.Add("$($field.Label): $($field.Value)")
            }
        }

        $lines.Add("Selected distribution licence: $($package.SelectedLicence)")
        foreach ($field in @(
            [pscustomobject]@{ Label = "Licence URL"; Value = $package.LicenceUrl },
            [pscustomobject]@{ Label = "Project URL"; Value = $package.ProjectUrl },
            [pscustomobject]@{ Label = "Repository URL"; Value = $package.RepositoryUrl }
        )) {
            if (-not [string]::IsNullOrWhiteSpace($field.Value)) {
                $lines.Add("$($field.Label): $($field.Value)")
            }
        }
    }

    foreach ($package in ($orderedMetadata | Where-Object { -not [string]::IsNullOrWhiteSpace($_.PackageLicenceText) })) {
        $lines.Add("")
        $lines.Add("===============================================================================")
        $lines.Add("PACKAGE-SPECIFIC LICENCE: $($package.PackageId) $($package.Version)")
        $lines.Add("===============================================================================")
        $lines.Add("")
        $lines.Add($package.PackageLicenceText)
    }

    $upstreamNoticeGroups = @{}
    foreach ($package in $orderedMetadata) {
        foreach ($noticeFile in @($package.NoticeFiles)) {
            if (-not $upstreamNoticeGroups.ContainsKey($noticeFile.Hash)) {
                $upstreamNoticeGroups[$noticeFile.Hash] = [pscustomobject]@{
                    Hash = $noticeFile.Hash
                    Text = $noticeFile.Text
                    Sources = [System.Collections.Generic.List[string]]::new()
                }
            }

            $upstreamNoticeGroups[$noticeFile.Hash].Sources.Add(
                "$($package.PackageId) $($package.Version) ($($noticeFile.RelativePath))"
            )
        }
    }

    foreach ($noticeGroup in @($upstreamNoticeGroups.Values | Sort-Object Hash)) {
        $lines.Add("")
        $lines.Add("===============================================================================")
        $lines.Add("UPSTREAM THIRD-PARTY NOTICE")
        $lines.Add("===============================================================================")
        $lines.Add("Provided by packages:")
        foreach ($source in @($noticeGroup.Sources | Sort-Object -Unique)) {
            $lines.Add("- $source")
        }
        $lines.Add("")
        $lines.Add($noticeGroup.Text)
    }

    $standardLicences = @(
        [pscustomobject]@{ Id = "MIT"; FileName = "MIT.txt" },
        [pscustomobject]@{ Id = "Apache-2.0"; FileName = "Apache-2.0.txt" },
        [pscustomobject]@{ Id = "BSD-3-Clause"; FileName = "BSD-3-Clause.txt" }
    )

    foreach ($licence in $standardLicences) {
        if ($orderedMetadata.SelectedLicence -notcontains $licence.Id) {
            continue
        }

        $licencePath = Join-Path $thirdPartyLicencesPath $licence.FileName
        if (-not (Test-Path -LiteralPath $licencePath)) {
            throw "Required third-party licence text '$licencePath' was not found."
        }

        $lines.Add("")
        $lines.Add("===============================================================================")
        $lines.Add("$($licence.Id) LICENCE TEXT")
        $lines.Add("===============================================================================")
        $lines.Add("")
        $lines.Add((Get-Content -LiteralPath $licencePath -Raw).Trim())
    }

    $outputParent = Split-Path -Path $OutputFilePath -Parent
    New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
    [System.IO.File]::WriteAllText(
        $OutputFilePath,
        ($lines -join "`n") + "`n",
        [System.Text.UTF8Encoding]::new($false))
}

function Get-ResolvedPackageDependencies {
    param([Parameter(Mandatory)][string]$ProjectFilePath)

    $projectDirectory = Split-Path -Path $ProjectFilePath -Parent
    $targetFramework = Get-ProjectTargetFramework -ProjectFilePath $ProjectFilePath
    $assetsPath = Join-Path $projectDirectory "obj/project.assets.json"
    if (-not (Test-Path -Path $assetsPath)) {
        Invoke-Dotnet -Arguments @("restore", $ProjectFilePath, "--nologo", "--verbosity", "minimal")
    }

    $assets = Get-Content -Path $assetsPath -Raw | ConvertFrom-Json -Depth 100
    $target = $assets.targets.PSObject.Properties |
        Where-Object { $_.Name -like "*$targetFramework*" } |
        Select-Object -First 1

    if ($null -eq $target) {
        throw "Could not locate a $targetFramework target in '$assetsPath'."
    }

    $dependencies = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $target.Value.PSObject.Properties) {
        $value = $entry.Value
        if ([string]$value.type -ne "package") {
            continue
        }

        $parts = $entry.Name.Split("/", 2)
        if ($parts.Count -ne 2) {
            continue
        }

        $dependencies[$parts[0]] = $parts[1]
    }

    return $dependencies
}

function Normalize-PathKey {
    param([Parameter(Mandatory)][string]$Path)
    return ([System.IO.Path]::GetFullPath($Path)).ToLowerInvariant()
}

function Get-ProjectReferencePackageClosure {
    param(
        [Parameter(Mandatory)][string]$RootProjectPath,
        [Parameter(Mandatory)][System.Collections.Specialized.OrderedDictionary]$Packages
    )

    $publicProjectsByPath = @{}
    foreach ($entry in $Packages.GetEnumerator()) {
        $publicProjectsByPath[(Normalize-PathKey -Path $entry.Value.FullProjectPath)] = [string]$entry.Key
    }

    $queue = [System.Collections.Generic.Queue[string]]::new()
    $visitedProjects = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $packageIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $queue.Enqueue([System.IO.Path]::GetFullPath($RootProjectPath))

    while ($queue.Count -gt 0) {
        $projectPath = $queue.Dequeue()
        if (-not $visitedProjects.Add($projectPath)) {
            continue
        }

        $projectKey = Normalize-PathKey -Path $projectPath
        if ($publicProjectsByPath.ContainsKey($projectKey)) {
            [void]$packageIds.Add($publicProjectsByPath[$projectKey])
        }

        [xml]$projectXml = Get-Content -Path $projectPath -Raw
        $projectDirectory = Split-Path -Path $projectPath -Parent
        foreach ($node in $projectXml.SelectNodes("//ProjectReference")) {
            $includePath = [string]$node.Include
            if ([string]::IsNullOrWhiteSpace($includePath)) {
                continue
            }

            $queue.Enqueue([System.IO.Path]::GetFullPath((Join-Path $projectDirectory $includePath)))
        }
    }

    return @($packageIds | Sort-Object)
}

function Add-Or-UpdateDependency {
    param(
        [Parameter(Mandatory)][xml]$NuspecXml,
        [Parameter(Mandatory)][System.Xml.XmlElement]$GroupNode,
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$PackageVersion
    )

    $namespace = $NuspecXml.DocumentElement.NamespaceURI
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($NuspecXml.NameTable)
    $namespaceManager.AddNamespace("ns", $namespace)
    $existing = $GroupNode.SelectSingleNode("ns:dependency[@id='$PackageId']", $namespaceManager)

    if ($null -eq $existing) {
        $existing = $NuspecXml.CreateElement("dependency", $namespace)
        $existing.SetAttribute("id", $PackageId)
        [void]$GroupNode.AppendChild($existing)
    }

    $existing.SetAttribute("version", $PackageVersion)
    $existing.RemoveAttribute("exclude")
}

function Flatten-ToolsPackageDependencies {
    param(
        [Parameter(Mandatory)][string]$PackageFilePath,
        [Parameter(Mandatory)][hashtable]$Dependencies,
        [Parameter(Mandatory)][System.Collections.Generic.HashSet[string]]$ExcludedDependencies,
        [Parameter(Mandatory)][string]$TargetFramework
    )

    $tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("datahub-cli-tools-package-" + [guid]::NewGuid().ToString("N"))
    $rewrittenPackagePath = Join-Path ([System.IO.Path]::GetDirectoryName($PackageFilePath)) ([System.IO.Path]::GetFileNameWithoutExtension($PackageFilePath) + ".flattened.nupkg")

    try {
        [System.IO.Compression.ZipFile]::ExtractToDirectory($PackageFilePath, $tempDirectory)
        $nuspecPath = Get-ChildItem -Path $tempDirectory -Filter "*.nuspec" | Select-Object -First 1
        if ($null -eq $nuspecPath) {
            throw "Package '$PackageFilePath' did not contain a nuspec."
        }

        [xml]$nuspecXml = Get-Content -Path $nuspecPath.FullName -Raw
        $namespace = $nuspecXml.DocumentElement.NamespaceURI
        $namespaceManager = New-Object System.Xml.XmlNamespaceManager($nuspecXml.NameTable)
        $namespaceManager.AddNamespace("ns", $namespace)
        $dependenciesNode = $nuspecXml.SelectSingleNode("//ns:metadata/ns:dependencies", $namespaceManager)

        if ($null -eq $dependenciesNode) {
            $metadataNode = $nuspecXml.SelectSingleNode("//ns:metadata", $namespaceManager)
            $dependenciesNode = $nuspecXml.CreateElement("dependencies", $namespace)
            [void]$metadataNode.AppendChild($dependenciesNode)
        }

        $groupNode = $dependenciesNode.SelectSingleNode("ns:group[@targetFramework='$TargetFramework']", $namespaceManager)
        if ($null -eq $groupNode) {
            $groupNode = $dependenciesNode.SelectSingleNode("ns:group", $namespaceManager)
        }

        if ($null -eq $groupNode) {
            $groupNode = $nuspecXml.CreateElement("group", $namespace)
            $groupNode.SetAttribute("targetFramework", $TargetFramework)
            [void]$dependenciesNode.AppendChild($groupNode)
        }

        foreach ($dependencyNode in @($groupNode.SelectNodes("ns:dependency", $namespaceManager))) {
            $dependencyId = [string]$dependencyNode.GetAttribute("id")
            if ($ExcludedDependencies.Contains($dependencyId)) {
                [void]$groupNode.RemoveChild($dependencyNode)
            }
        }

        foreach ($packageId in ($Dependencies.Keys | Sort-Object)) {
            if ([string]::Equals($packageId, $toolsPackageId, [StringComparison]::OrdinalIgnoreCase) -or $ExcludedDependencies.Contains($packageId)) {
                continue
            }

            Add-Or-UpdateDependency -NuspecXml $nuspecXml -GroupNode $groupNode -PackageId $packageId -PackageVersion ([string]$Dependencies[$packageId])
        }

        $nuspecXml.Save($nuspecPath.FullName)
        if (Test-Path -Path $rewrittenPackagePath) {
            Remove-Item -Path $rewrittenPackagePath -Force
        }

        [System.IO.Compression.ZipFile]::CreateFromDirectory($tempDirectory, $rewrittenPackagePath)
        Move-Item -Path $rewrittenPackagePath -Destination $PackageFilePath -Force
    }
    finally {
        if (Test-Path -Path $tempDirectory) {
            Remove-Item -Path $tempDirectory -Recurse -Force
        }
    }
}

$versionsFilePath = Resolve-RepoPath -Path $VersionsPath
$outputDirectory = Resolve-RepoPath -Path $OutputPath
$dependencyOutputDirectory = Resolve-RepoPath -Path $DependencyOutputPath
$artifactsDirectory = Resolve-RepoPath -Path $ArtifactsPath
$toolsTargetFramework = Get-ProjectTargetFramework -ProjectFilePath $toolsProjectPath
$packages = Get-PublicPackages -VersionsFilePath $versionsFilePath
$packagesToStage = Get-ProjectReferencePackageClosure -RootProjectPath $toolsProjectPath -Packages $packages

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $dependencyOutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null
Get-ChildItem -Path $outputDirectory -Filter "*.nupkg" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -Path $dependencyOutputDirectory -Filter "*.nupkg" -ErrorAction SilentlyContinue | Remove-Item -Force

foreach ($packageId in $packagesToStage) {
    $package = $packages[$packageId]

    $expectedPackagePath = Join-Path $artifactsDirectory "$packageId.$Version.nupkg"
    if (Test-Path -Path $expectedPackagePath) {
        Remove-Item -Path $expectedPackagePath -Force
    }

    $arguments = @(
        "pack",
        $package.FullProjectPath,
        "--configuration",
        $Configuration,
        "--output",
        $artifactsDirectory,
        "--nologo",
        "--verbosity",
        "minimal",
        "-m:1",
        "-p:VersionPrefix=$Version"
    )

    if ($NoBuild) {
        $arguments += "--no-build"
    }

    Invoke-Dotnet -Arguments $arguments
    $destinationDirectory = if ([string]::Equals($packageId, $toolsPackageId, [StringComparison]::OrdinalIgnoreCase)) {
        $outputDirectory
    }
    else {
        $dependencyOutputDirectory
    }

    Copy-Item -Path $expectedPackagePath -Destination $destinationDirectory -Force
}

$resolvedPackages = Get-ResolvedPackageDependencies -ProjectFilePath $toolsProjectPath
$resolvedHostPackages = Get-ResolvedPackageDependencies -ProjectFilePath $hostProjectPath
$flattenedDependencies = @{}
$noticePackageIdentities = @{}

foreach ($dependencySet in @($resolvedHostPackages, $resolvedPackages)) {
    foreach ($entry in $dependencySet.GetEnumerator()) {
        $packageId = [string]$entry.Key
        $packageVersion = [string]$entry.Value
        $noticePackageIdentities["$packageId/$packageVersion"] = [pscustomobject]@{
            PackageId = $packageId
            Version = $packageVersion
        }
    }
}

Write-ThirdPartyNotices `
    -PackageIdentities $noticePackageIdentities `
    -OutputFilePath (Join-Path $outputDirectory "THIRD-PARTY-NOTICES.txt")

foreach ($entry in $packages.GetEnumerator()) {
    $packageId = [string]$entry.Key
    if ($packageId -in $packagesToStage) {
        $flattenedDependencies[$packageId] = $Version
    }
}

foreach ($entry in $resolvedPackages.GetEnumerator()) {
    $packageId = [string]$entry.Key
    $packageVersion = [string]$entry.Value
    if ($hostProvidedPackageIds.Contains($packageId)) {
        continue
    }

    $flattenedDependencies[$packageId] = $packageVersion
    Copy-PackageFromGlobalCache -PackageId $packageId -PackageVersion $packageVersion -DestinationDirectory $dependencyOutputDirectory
}

$toolsPackagePath = Join-Path $outputDirectory "$toolsPackageId.$Version.nupkg"
if (-not (Test-Path -Path $toolsPackagePath)) {
    throw "Expected bundled tools package '$toolsPackagePath' was not staged."
}

Flatten-ToolsPackageDependencies -PackageFilePath $toolsPackagePath -Dependencies $flattenedDependencies -ExcludedDependencies $hostProvidedPackageIds -TargetFramework $toolsTargetFramework
Write-Host "Staged DataHub CLI bundled tools in '$outputDirectory'."
