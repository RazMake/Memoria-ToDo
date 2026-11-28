$ErrorActionPreference = 'Stop'
$proofRoot = Join-Path '.agents' 'proof-only'
$manifest = Get-Content -LiteralPath (Join-Path $proofRoot 'native-groups.json') -Raw | ConvertFrom-Json
if ($manifest.groups.Count -lt 1) { throw 'There are no native failure-proof groups.' }
$groupNumber = 0
foreach ($group in $manifest.groups) {
    $groupNumber++
    $patch = Join-Path $proofRoot $group.patch
    $resultsDirectory = "test-results/android-device/proof-$groupNumber"
    git apply --check $patch
    if ($LASTEXITCODE -ne 0) { throw "Cannot apply native failure group $groupNumber." }
    git apply $patch
    if ($LASTEXITCODE -ne 0) { throw "Applying native failure group $groupNumber failed." }
    try {
        $runnerArguments = @('.agents/android-device-tests.mjs', '--project',
            'tests/Android/Tests/Memoria.ToDo.Android.Tests.csproj',
            '--results-directory', $resultsDirectory) + @($group.arguments)
        & node @runnerArguments
        if ($LASTEXITCODE -eq 0) { throw "Native failure group $groupNumber unexpectedly passed." }
        $reports = @(Get-ChildItem -LiteralPath $resultsDirectory -Recurse -Filter '*.trx')
        if ($reports.Count -ne 1) { throw "Native failure group $groupNumber needs exactly one TRX report." }
        $counts = . .agents/android-device-tests/read-trx.ps1 -Report $reports[0].FullName | ConvertFrom-Json
        if ($counts.failed -ne $group.tests.Count -or $counts.passed -ne 0 -or
            $counts.notExecuted -ne 0 -or $counts.resultCount -ne $group.tests.Count -or
            $counts.executed -ne $group.tests.Count) {
            throw "Native failure group $groupNumber did not fail exactly its selected tests: $($counts | ConvertTo-Json -Compress)"
        }
        $results = $xml.SelectNodes("/*[local-name()='TestRun']/*[local-name()='Results']/*[local-name()='UnitTestResult']")
        foreach ($test in $group.tests) {
            $matches = @($results | Where-Object { $_.GetAttribute('testName') -eq $test.name })
            if ($matches.Count -ne 1 -or $matches[0].GetAttribute('outcome') -ne 'Failed') {
                throw "The expected native test did not fail exactly once: $($test.name)"
            }
            $message = $matches[0].SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']")
            if ($null -eq $message -or $message.InnerText -notmatch $test.errorPattern) {
                throw "The native test failed for an unexpected reason: $($test.name)"
            }
            "Proven: $($test.name)"
            $message.InnerText
        }
    } finally {
        git apply --reverse $patch
        if ($LASTEXITCODE -ne 0) { throw "Restoring native failure group $groupNumber failed." }
    }
}
git diff --exit-code
if ($LASTEXITCODE -ne 0) { throw 'The native proof left source changes behind.' }
'All native behavior breaks were restored.'
