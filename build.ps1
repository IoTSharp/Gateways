[CmdletBinding()]
param(
    [ValidateSet('Build', 'Test', 'Publish', 'Clean')]
    [string]$Action = 'Build',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$MustAot,

    [string]$RuntimeIdentifier,

    [string]$Output
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$solutionPath = Join-Path $repoRoot 'IoTEdge.sln'
$publishProject = Join-Path (Join-Path $repoRoot 'src') (Join-Path 'IoTEdge' 'IoTEdge.csproj')

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

function Get-DefaultRuntimeIdentifier {
    $os = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        'win'
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
        'linux'
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
        'osx'
    }
    else {
        throw 'Unable to infer a runtime identifier for this platform.'
    }

    $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    return "$os-$arch"
}

$edgeAot = if ($MustAot) { 'true' } else { 'false' }
$basicExtensions = if ($MustAot) { 'false' } else { 'true' }
$commonProps = @(
    "-p:EdgeAot=$edgeAot",
    "-p:EdgeEnableBasicRuntimeExtensions=$basicExtensions"
)

switch ($Action) {
    'Clean' {
        Invoke-DotNet -Arguments (@('clean', $solutionPath, '-c', $Configuration) + $commonProps)
    }
    'Build' {
        Invoke-DotNet -Arguments (@('build', $solutionPath, '-c', $Configuration) + $commonProps)
    }
    'Test' {
        Invoke-DotNet -Arguments (@('test', $solutionPath, '-c', $Configuration) + $commonProps)
    }
    'Publish' {
        $rid = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
            Get-DefaultRuntimeIdentifier
        }
        else {
            $RuntimeIdentifier
        }

        $arguments = @(
            'publish',
            $publishProject,
            '-c', $Configuration
        ) + $commonProps

        if ($MustAot) {
            $arguments += @('-r', $rid, '--self-contained', 'true')
            $arguments += '-p:PublishAot=true'
        }
        elseif (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
            $arguments += @('-r', $rid)
        }

        if (-not [string]::IsNullOrWhiteSpace($Output)) {
            $arguments += @('-o', $Output)
        }

        Invoke-DotNet -Arguments $arguments
    }
}
