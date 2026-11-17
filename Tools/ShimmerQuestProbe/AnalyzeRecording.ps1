param([Parameter(Mandatory = $true)][string]$CsvPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReadRecordingCsv.ps1')
$rows = @(Read-RecordingCsv $CsvPath)
if ($rows.Count -gt 0 -and $rows[0].PSObject.Properties.Name -notcontains 'shimmer_ticks_unwrapped') {
    throw 'For timing/continuity analysis, select diagnostics/shimmer_raw.csv (the technical recording), not the Excel-facing shimmer.csv.'
}
if ($rows.Count -lt 2) { throw 'At least two samples are needed' }
function Number([string]$value) { Read-RecordingNumber $value }
$sequenceGaps = 0L
$tickErrors = 0L
$reportedMissing = 0L
$minStep = [long]::MaxValue
$maxStep = 0L
$eda = [System.Collections.Generic.List[double]]::new()
$ppg = [System.Collections.Generic.List[double]]::new()
$ranges = [System.Collections.Generic.HashSet[int]]::new()
$edaInvalid = 0
$ppgInvalid = 0
$transitions = 0
for ($i = 0; $i -lt $rows.Count; $i++) {
    $row = $rows[$i]
    if ($i -gt 0) {
        $seqStep = [long]$row.sequence - [long]$rows[$i - 1].sequence
        if ($seqStep -ne 1) { $sequenceGaps += [Math]::Max(1, $seqStep - 1) }
        $step = [long]$row.shimmer_ticks_unwrapped - [long]$rows[$i - 1].shimmer_ticks_unwrapped
        if ($step -le 0) { $tickErrors++ }
        $minStep = [Math]::Min($minStep, $step)
        $maxStep = [Math]::Max($maxStep, $step)
    }
    $reportedMissing += [long]$row.estimated_missing_samples
    if ($row.eda_conversion_valid -eq 'true') { $eda.Add((Number $row.eda_us)) } else { $edaInvalid++ }
    $ppg.Add((Number $row.ppg_raw))
    if ($row.ppg_adc_valid -ne 'true') { $ppgInvalid++ }
    if ($row.gsr_range_changed -eq 'true') { $transitions++ }
    $null = $ranges.Add([int]$row.gsr_range)
}
$duration = (Number $rows[-1].shimmer_elapsed_s) - (Number $rows[0].shimmer_elapsed_s)
$edaStats = $eda | Measure-Object -Minimum -Maximum -Average
$ppgStats = $ppg | Measure-Object -Minimum -Maximum -Average
[ordered]@{
    samples = $rows.Count
    device_duration_seconds = $duration
    received_samples_per_device_second = ($rows.Count - 1) / $duration
    first_receive_utc_ms = $rows[0].android_receive_utc_ms
    last_receive_utc_ms = $rows[-1].android_receive_utc_ms
    sequence_gaps = $sequenceGaps
    non_increasing_timestamps = $tickErrors
    estimated_missing_samples = $reportedMissing
    minimum_tick_step = $minStep
    maximum_tick_step = $maxStep
    eda_invalid_samples = $edaInvalid
    eda_us_min = $edaStats.Minimum
    eda_us_max = $edaStats.Maximum
    eda_us_mean = $edaStats.Average
    gsr_ranges = @($ranges)
    gsr_range_changes = $transitions
    ppg_invalid_samples = $ppgInvalid
    ppg_raw_min = $ppgStats.Minimum
    ppg_raw_max = $ppgStats.Maximum
    ppg_raw_mean = $ppgStats.Average
    interpretation = 'Transport and numeric checks only; does not establish pulse quality, electrode contact or synchronized clocks.'
} | ConvertTo-Json
