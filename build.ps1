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
$outputPath = Join-Path $repoRoot "Core\Plugins\KerbalFX.Core.dll"
$tempOutputPath = Join-Path $repoRoot "Core\Plugins\KerbalFX.Core.build.tmp.dll"

if (-not (Test-Path $managedDir)) {
    throw "KSP managed folder not found: $managedDir"
}

$sourceDirs = @(
    "Core\PluginSource",
    "AeroFX\PluginSource",
    "RoverDust\PluginSource",
    "ImpactPuffs\PluginSource",
    "BlastFX\PluginSource"
)

$sourceFiles = foreach ($dir in $sourceDirs) {
    Get-ChildItem (Join-Path $repoRoot $dir) -Filter *.cs -File | Sort-Object FullName | Select-Object -ExpandProperty FullName
}

if (-not ($sourceFiles | Where-Object { $_ -like "*Core\PluginSource\KerbalFX_Shared.cs" })) {
    throw "Missing required shared source file: Core\PluginSource\KerbalFX_Shared.cs"
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

$referenceArgs = foreach ($name in $referenceNames) {
    $path = Join-Path $managedDir $name
    if (-not (Test-Path $path)) {
        throw "Missing reference assembly: $path"
    }
    "/reference:$path"
}

$compiler = Resolve-CompilerPath -ExplicitPath $CompilerPath

if (Test-Path $tempOutputPath) {
    Remove-Item $tempOutputPath -Force
}

$compilerArgs = @(
    "/nologo",
    "/target:library",
    "/optimize+",
    "/debug-",
    "/out:$tempOutputPath"
) + $referenceArgs + $sourceFiles

try {
    & $compiler @compilerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Compiler exited with code $LASTEXITCODE"
    }

    Move-Item $tempOutputPath $outputPath -Force
    Write-Host "Built KerbalFX.Core.dll"
    Write-Host "Output: $outputPath"
    Write-Host "Sources: $($sourceFiles.Count)"
}
finally {
    if (Test-Path $tempOutputPath) {
        Remove-Item $tempOutputPath -Force
    }
}
