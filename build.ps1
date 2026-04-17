param(
    [string]$CompilerPath
)

$ErrorActionPreference = "Stop"

function Resolve-CompilerPath {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path $ExplicitPath)) {
            throw "Compiler not found: $ExplicitPath"
        }
        return (Resolve-Path $ExplicitPath).Path
    }

    $candidates = @(
        (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
        (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "C# compiler not found. Pass -CompilerPath to build.ps1."
}

$repoRoot = $PSScriptRoot
$gameDataDir = Split-Path $repoRoot -Parent
$kspRoot = Split-Path $gameDataDir -Parent
$managedDir = Join-Path $kspRoot "KSP_x64_Data\Managed"
$buildTempDir = Join-Path $repoRoot ".build"

if (-not (Test-Path $managedDir)) {
    throw "KSP managed folder not found: $managedDir"
}

$referenceNames = @(
    "Assembly-CSharp.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.ParticleSystemModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.VehiclesModule.dll",
    "UnityEngine.AnimationModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.UI.dll"
)

function Get-ManagedReferenceArgs {
    $args = @()
    foreach ($name in $referenceNames) {
        $path = Join-Path $managedDir $name
        if (-not (Test-Path $path)) {
            throw "Missing reference assembly: $path"
        }
        $args += "/reference:$path"
    }
    return $args
}

function Get-AdditionalReferenceArgs {
    param([string[]]$ReferencePaths)

    $args = @()
    if ($ReferencePaths -eq $null) {
        return $args
    }

    foreach ($path in $ReferencePaths) {
        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }
        if (-not (Test-Path $path)) {
            throw "Missing reference assembly: $path"
        }
        $args += "/reference:$path"
    }

    return $args
}

function Get-SourceFiles {
    param([string[]]$SourceDirs)

    $files = @()
    foreach ($dir in $SourceDirs) {
        $fullPath = Join-Path $repoRoot $dir
        if (-not (Test-Path $fullPath)) {
            throw "Source directory not found: $fullPath"
        }

        $files += Get-ChildItem -Path $fullPath -Filter *.cs -File -Recurse |
            Sort-Object FullName |
            Select-Object -ExpandProperty FullName
    }

    return $files
}

function Ensure-OutputDirectory {
    param([string]$OutputPath)

    $dir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Compile-Assembly {
    param(
        [string]$Compiler,
        [object]$Spec,
        [string[]]$ManagedReferenceArgs
    )

    Ensure-OutputDirectory -OutputPath $Spec.OutputPath
    Ensure-OutputDirectory -OutputPath $Spec.TempOutputPath

    if (Test-Path $Spec.TempOutputPath) {
        Remove-Item $Spec.TempOutputPath -Force
    }

    $sourceFiles = Get-SourceFiles -SourceDirs $Spec.SourceDirs
    if (-not $sourceFiles -or $sourceFiles.Count -eq 0) {
        throw "No source files found for $($Spec.Id)"
    }

    $compilerArgs = @(
        "/nologo",
        "/target:library",
        "/optimize+",
        "/debug-",
        "/out:$($Spec.TempOutputPath)"
    ) + $ManagedReferenceArgs + (Get-AdditionalReferenceArgs -ReferencePaths $Spec.AdditionalReferencePaths) + $sourceFiles

    & $Compiler @compilerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Compiler exited with code $LASTEXITCODE for $($Spec.Id)"
    }

    return $sourceFiles.Count
}

$coreOutputPath = Join-Path $repoRoot "Core\Plugins\KerbalFX.Core.dll"
$coreTempOutputPath = Join-Path $buildTempDir "KerbalFX.Core.dll"

$assemblySpecs = @(
    [pscustomobject]@{
        Id = "Core"
        OutputPath = $coreOutputPath
        TempOutputPath = $coreTempOutputPath
        SourceDirs = @("Core\PluginSource")
        AdditionalReferencePaths = @()
    }
    [pscustomobject]@{
        Id = "AeroFX"
        OutputPath = (Join-Path $repoRoot "AeroFX\Plugins\KerbalFX.AeroFX.dll")
        TempOutputPath = (Join-Path $buildTempDir "KerbalFX.AeroFX.dll")
        SourceDirs = @("AeroFX\PluginSource")
        AdditionalReferencePaths = @($coreOutputPath)
    }
    [pscustomobject]@{
        Id = "RoverDust"
        OutputPath = (Join-Path $repoRoot "RoverDust\Plugins\KerbalFX.RoverDust.dll")
        TempOutputPath = (Join-Path $buildTempDir "KerbalFX.RoverDust.dll")
        SourceDirs = @("RoverDust\PluginSource")
        AdditionalReferencePaths = @($coreOutputPath)
    }
    [pscustomobject]@{
        Id = "ImpactPuffs"
        OutputPath = (Join-Path $repoRoot "ImpactPuffs\Plugins\KerbalFX.ImpactPuffs.dll")
        TempOutputPath = (Join-Path $buildTempDir "KerbalFX.ImpactPuffs.dll")
        SourceDirs = @("ImpactPuffs\PluginSource")
        AdditionalReferencePaths = @($coreOutputPath)
    }
    [pscustomobject]@{
        Id = "BlastFX"
        OutputPath = (Join-Path $repoRoot "BlastFX\Plugins\KerbalFX.BlastFX.dll")
        TempOutputPath = (Join-Path $buildTempDir "KerbalFX.BlastFX.dll")
        SourceDirs = @("BlastFX\PluginSource")
        AdditionalReferencePaths = @($coreOutputPath)
    }
)

$coreSourceFiles = Get-SourceFiles -SourceDirs @("Core\PluginSource")
if (-not ($coreSourceFiles | Where-Object { $_ -like "*Core\PluginSource\KerbalFX_Shared.cs" })) {
    throw "Missing required shared source file: Core\PluginSource\KerbalFX_Shared.cs"
}
if (-not ($coreSourceFiles | Where-Object { $_ -like "*Core\PluginSource\KerbalFX_VesselControllerBootstrap.cs" })) {
    throw "Missing required shared source file: Core\PluginSource\KerbalFX_VesselControllerBootstrap.cs"
}

$compiler = Resolve-CompilerPath -ExplicitPath $CompilerPath
$managedReferenceArgs = Get-ManagedReferenceArgs

try {
    foreach ($spec in $assemblySpecs) {
        $sourceCount = Compile-Assembly -Compiler $compiler -Spec $spec -ManagedReferenceArgs $managedReferenceArgs
        Move-Item $spec.TempOutputPath $spec.OutputPath -Force

        Write-Host "Built $([System.IO.Path]::GetFileName($spec.OutputPath))"
        Write-Host "Output: $($spec.OutputPath)"
        Write-Host "Sources: $sourceCount"
    }
}
finally {
    foreach ($spec in $assemblySpecs) {
        if (Test-Path $spec.TempOutputPath) {
            Remove-Item $spec.TempOutputPath -Force
        }
    }

    if (Test-Path $buildTempDir) {
        $remaining = Get-ChildItem -Path $buildTempDir -Force
        if ($remaining.Count -eq 0) {
            Remove-Item $buildTempDir -Force
        }
    }
}
