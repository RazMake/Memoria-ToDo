param([Parameter(Mandatory)][string] $Report)
$ErrorActionPreference = 'Stop'
$settings = [System.Xml.XmlReaderSettings]::new()
$settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$reader = [System.Xml.XmlReader]::Create((Resolve-Path $Report).Path, $settings)
try {
    $xml = [System.Xml.XmlDocument]::new()
    $xml.Load($reader)
} finally {
    $reader.Dispose()
}
if ($xml.DocumentElement.LocalName -ne 'TestRun') { throw 'The report is not a TRX TestRun.' }
$summaries = $xml.SelectNodes("/*[local-name()='TestRun']/*[local-name()='ResultSummary']")
if ($summaries.Count -ne 1) { throw 'The report needs exactly one result summary.' }
$summary = $summaries[0]
$counters = $summary.SelectNodes("*[local-name()='Counters']")
if ($counters.Count -ne 1) { throw 'The report needs exactly one set of counters.' }
$counts = @{}
foreach ($name in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
    $value = $counters[0].GetAttribute($name)
    if ($value -notmatch '^\d+$') { throw "The $name counter is invalid." }
    $counts[$name] = [long] $value
}
$results = $xml.SelectNodes("/*[local-name()='TestRun']/*[local-name()='Results']/*[local-name()='UnitTestResult']")
$counts.resultCount = $results.Count
$counts.passedResultCount = @($results | Where-Object { $_.GetAttribute('outcome') -eq 'Passed' }).Count
$counts.outcome = $summary.GetAttribute('outcome')
$counts | ConvertTo-Json -Compress
