param(
    [Parameter(Mandatory)]
    [string] $SourceRoot,
    [Parameter(Mandatory)]
    [string] $Device,
    [string] $AndroidSdkDirectory,
    [string] $JavaSdkDirectory
)

$ErrorActionPreference = 'Stop'
$SourceRoot = (Resolve-Path $SourceRoot).Path
$project = Join-Path $PSScriptRoot 'DeviceRunnersProof.csproj'
$proofProps = Join-Path $PSScriptRoot 'Directory.Build.props'
$properties = @("-p:MemoriaSourceRoot=$SourceRoot", "-p:DirectoryBuildPropsPath=$proofProps")
if ($AndroidSdkDirectory) { $properties += "-p:AndroidSdkDirectory=$AndroidSdkDirectory" }
if ($JavaSdkDirectory) { $properties += "-p:JavaSdkDirectory=$JavaSdkDirectory" }

Push-Location $SourceRoot
try {
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw "Cannot select the source repository's SDK." }
    $evaluation = & dotnet msbuild $project @properties -getProperty:TargetFramework,SupportedOSPlatformVersion
    if ($LASTEXITCODE -ne 0) { throw "Cannot evaluate the Android proof project." }
    $evaluated = ($evaluation | ConvertFrom-Json).Properties
    $properties += "-p:TargetFramework=$($evaluated.TargetFramework)"
    $properties += "-p:MemoriaProofTargetFramework=$($evaluated.TargetFramework)"
    $properties += "-p:SupportedOSPlatformVersion=$($evaluated.SupportedOSPlatformVersion)"
    $sourceCommit = & git --no-pager rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source commit.' }
}
finally {
    Pop-Location
}

# DeviceRunners replaces VSTest; its app does not implement the MTP protocol.
$configuration = Get-Content (Join-Path $SourceRoot 'global.json') -Raw | ConvertFrom-Json -AsHashtable
$configuration['test']['runner'] = 'VSTest'
$configuration | ConvertTo-Json -Depth 10 |
    Set-Content (Join-Path $PSScriptRoot 'global.json') -Encoding utf8
$properties += "-p:DeviceRunnersDevice=$Device"

function Invoke-ProofRun {
    param(
        [string] $Name,
        [string] $Filter,
        [int] $ExpectedTotal,
        [int] $ExpectedFailed
    )

    $results = Join-Path $PSScriptRoot "test-results/$Name"
    & dotnet test $project @properties -f $evaluated.TargetFramework `
        --filter $Filter --logger trx --results-directory $results | Out-Host
    $exitCode = $LASTEXITCODE
    $report = Join-Path $results 'test-results.trx'
    if (-not (Test-Path $report)) {
        throw "$Name produced no TRX report (exit $exitCode)."
    }
    [xml] $xml = Get-Content $report -Raw
    $counts = $xml.TestRun.ResultSummary.Counters
    if ([int] $counts.total -ne $ExpectedTotal -or
        [int] $counts.executed -ne $ExpectedTotal -or
        [int] $counts.failed -ne $ExpectedFailed -or
        [int] $counts.passed -ne ($ExpectedTotal - $ExpectedFailed) -or
        [int] $counts.notExecuted -ne 0) {
        throw "$Name returned unexpected test counts: $($counts.OuterXml)"
    }
    if (($ExpectedFailed -eq 0 -and $exitCode -ne 0) -or
        ($ExpectedFailed -gt 0 -and $exitCode -eq 0)) {
        throw "$Name returned unexpected exit code $exitCode."
    }
    if ($ExpectedFailed -gt 0) {
        $messages = ($xml.SelectNodes("//*[local-name()='Message']") |
            ForEach-Object { $_.InnerText }) -join "`n"
        if (-not $messages.Contains('DeviceRunners intentional assertion failure')) {
            throw "$Name failed for a reason other than the intentional assertion."
        }
    }
    [pscustomobject] @{
        Run = $Name
        Filter = $Filter
        Total = [int] $counts.total
        Failed = [int] $counts.failed
        Skipped = [int] $counts.notExecuted
        ExitCode = $exitCode
    }
}

Push-Location $PSScriptRoot
try {
    $runs = @(
        Invoke-ProofRun 'ux-filter' 'Category=UX' 1 0
        Invoke-ProofRun 'failure-proof' 'Category=FailureProof' 1 1
        Invoke-ProofRun 'final-pass' '(Category=UX)|(Category=Integration)' 2 0
    )
    $runs | Format-Table
    [ordered] @{
        Sdk = $sdk
        SourceCommit = $sourceCommit
        Framework = $evaluated.TargetFramework
        Runner = 'VSTest'
        Device = $Device
        Runs = $runs
    } | ConvertTo-Json -Depth 10 |
        Set-Content (Join-Path $PSScriptRoot 'test-results/summary.json') -Encoding utf8
}
finally {
    Pop-Location
}
