[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $Version,

    [switch] $NoRestore,

    [switch] $SkipBuild,

    [switch] $StageOnly
)

$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Get-IfcIsolatorVersion {
    param([string] $RepoRoot)

    if ($Version) {
        return $Version
    }

    [xml] $props = Get-Content (Join-Path $RepoRoot 'Directory.Build.props')
    $props.Project.PropertyGroup.IfcIsolatorVersion
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $installationPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($installationPath) {
            $candidate = Join-Path $installationPath 'MSBuild\Current\Bin\MSBuild.exe'
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'MSBuild.exe was not found. Install Visual Studio Build Tools with MSBuild support, then run this script again.'
}

function Find-InnoCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'ISCC.exe was not found. Install the free Inno Setup compiler, or run with -StageOnly to only prepare the payload.'
}

function ConvertTo-CommandLine {
    param([string[]] $Arguments)

    ($Arguments | ForEach-Object {
        $argument = $_
        if ($argument -eq '') {
            '""'
        }
        elseif ($argument -match '[\s"]') {
            $escaped = $argument -replace '(\\*)"', '$1$1\"'
            $escaped = $escaped -replace '(\\+)$', '$1$1'
            '"' + $escaped + '"'
        }
        else {
            $argument
        }
    }) -join ' '
}

function Invoke-Tool {
    param(
        [string] $FilePath,
        [string[]] $Arguments,
        [string] $WorkingDirectory = $repoRoot
    )

    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processInfo.FileName = $FilePath
    $processInfo.Arguments = ConvertTo-CommandLine -Arguments $Arguments
    $processInfo.WorkingDirectory = $WorkingDirectory
    $processInfo.UseShellExecute = $false

    $pathValue = [System.Environment]::GetEnvironmentVariable('Path', 'Process')
    if (-not $pathValue) {
        $pathValue = [System.Environment]::GetEnvironmentVariable('PATH', 'Process')
    }

    $processInfo.EnvironmentVariables.Clear()
    $seenVariables = @{}
    $environment = [System.Environment]::GetEnvironmentVariables('Process')
    foreach ($key in $environment.Keys) {
        $name = [string] $key
        if ($name -ieq 'Path') {
            continue
        }

        $normalizedName = $name.ToUpperInvariant()
        if ($seenVariables.ContainsKey($normalizedName)) {
            continue
        }

        $processInfo.EnvironmentVariables[$name] = [string] $environment[$key]
        $seenVariables[$normalizedName] = $true
    }

    if ($pathValue) {
        $processInfo.EnvironmentVariables['Path'] = $pathValue
    }

    $process = [System.Diagnostics.Process]::Start($processInfo)
    $process.WaitForExit()
    return $process.ExitCode
}

function Invoke-ReleaseBuild {
    param(
        [string] $MSBuild,
        [string] $Solution,
        [string] $Platform,
        [string] $OutputPath
    )

    $target = if ($NoRestore) { 'Build' } else { 'Restore;Build' }
    $outputPathWithSlash = [System.IO.Path]::GetFullPath($OutputPath)
    if (-not $outputPathWithSlash.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $outputPathWithSlash += [System.IO.Path]::DirectorySeparatorChar
    }

    $exitCode = Invoke-Tool -FilePath $MSBuild -Arguments @(
        $Solution,
        "/t:$target",
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        "/p:OutputPath=$outputPathWithSlash",
        "/p:AppendTargetFrameworkToOutputPath=false",
        "/p:UseSharedCompilation=false"
    )

    if ($exitCode -ne 0) {
        throw "MSBuild failed for $Configuration|$Platform."
    }
}

function Assert-Payload {
    param([string] $PayloadRoot)

    $requiredFiles = @(
        'plugins\IfcIsolatorPlugin.plg',
        'plugins\IfcIsolatorPlugin\IfcIsolatorPlugin.dll',
        'plugins\IfcIsolatorPlugin\CoreLayer.exe',
        'plugins_x64\IfcIsolatorPlugin.plg',
        'plugins_x64\IfcIsolatorPlugin\IfcIsolatorPlugin.dll',
        'plugins_x64\IfcIsolatorPlugin\CoreLayer.exe'
    )

    foreach ($relativePath in $requiredFiles) {
        $path = Join-Path $PayloadRoot $relativePath
        if (-not (Test-Path $path)) {
            throw "Expected installer payload file is missing: $relativePath"
        }
    }
}

$repoRoot = Get-RepoRoot
$solution = Join-Path $repoRoot 'BIMVision-IfcIsolator.sln'
$payloadRoot = Join-Path $PSScriptRoot 'payload'
$x86Output = Join-Path $payloadRoot 'plugins\IfcIsolatorPlugin'
$x64Output = Join-Path $payloadRoot 'plugins_x64\IfcIsolatorPlugin'
$appVersion = Get-IfcIsolatorVersion -RepoRoot $repoRoot

Write-Host "Preparing Ifc Isolator installer payload version $appVersion..."

if (-not $SkipBuild) {
    if (Test-Path $payloadRoot) {
        Remove-Item -LiteralPath $payloadRoot -Recurse -Force
    }

    $msbuild = Find-MSBuild
    Invoke-ReleaseBuild -MSBuild $msbuild -Solution $solution -Platform 'x86' -OutputPath $x86Output
    Invoke-ReleaseBuild -MSBuild $msbuild -Solution $solution -Platform 'x64' -OutputPath $x64Output
}

Assert-Payload -PayloadRoot $payloadRoot

if ($StageOnly) {
    Write-Host "Payload staged at $payloadRoot"
    return
}

$innoCompiler = Find-InnoCompiler
$env:IFCISOLATOR_VERSION = $appVersion

$exitCode = Invoke-Tool -FilePath $innoCompiler -Arguments @((Join-Path $PSScriptRoot 'IfcIsolatorPlugin.iss')) -WorkingDirectory $PSScriptRoot
if ($exitCode -ne 0) {
    throw 'Inno Setup compilation failed.'
}

Write-Host "Installer generated in $(Join-Path $PSScriptRoot 'output')."
