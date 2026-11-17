# Run with Windows PowerShell 5.1 for the built-in .NET charting assembly.
param(
    [Parameter(Mandatory = $true)][string]$CsvPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [double]$PpgStartSeconds = 20,
    [double]$PpgWindowSeconds = 10
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms.DataVisualization
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot 'ReadRecordingCsv.ps1')
$rows = @(Read-RecordingCsv $CsvPath)
if ($rows.Count -gt 0 -and $rows[0].PSObject.Properties.Name -notcontains 'shimmer_elapsed_s') {
    throw 'For this raw waveform plot, select diagnostics/shimmer_raw.csv, not the Excel-facing shimmer.csv.'
}
if ($rows.Count -lt 2) { throw 'Not enough recorded samples to plot' }
$chart = New-Object System.Windows.Forms.DataVisualization.Charting.Chart
$chart.Width = 1600
$chart.Height = 1000
$chart.BackColor = [Drawing.Color]::White
$chart.Titles.Add('Quest 2 / Shimmer3: recorded EDA and PPG') | Out-Null
$chart.Titles[0].Font = New-Object Drawing.Font('Segoe UI', 18)
foreach ($name in @('EDA', 'PPG')) {
    $area = New-Object System.Windows.Forms.DataVisualization.Charting.ChartArea($name)
    $area.AxisX.Title = 'Device time from first sample (s)'
    $area.AxisY.Title = if ($name -eq 'EDA') { 'EDA (microsiemens)' } else { 'PPG (mV)' }
    $area.AxisX.LabelStyle.Format = '0.#'
    $area.AxisY.LabelStyle.Format = '0.##'
    $area.AxisY.IsStartedFromZero = $false
    $area.AxisX.MajorGrid.LineColor = [Drawing.Color]::Gainsboro
    $area.AxisY.MajorGrid.LineColor = [Drawing.Color]::Gainsboro
    $area.AxisX.TitleFont = New-Object Drawing.Font('Segoe UI', 12)
    $area.AxisY.TitleFont = New-Object Drawing.Font('Segoe UI', 12)
    $chart.ChartAreas.Add($area)
    $series = New-Object System.Windows.Forms.DataVisualization.Charting.Series($name)
    $series.ChartArea = $name
    $series.ChartType = [System.Windows.Forms.DataVisualization.Charting.SeriesChartType]::Line
    $series.BorderWidth = 2
    $series.Color = if ($name -eq 'EDA') { [Drawing.Color]::FromArgb(16, 108, 130) } else { [Drawing.Color]::FromArgb(166, 81, 34) }
    $chart.Series.Add($series)
}
$lastTime = Read-RecordingNumber $rows[-1].shimmer_elapsed_s
if ($PpgStartSeconds + $PpgWindowSeconds -gt $lastTime) { $PpgStartSeconds = [Math]::Max(0, $lastTime - $PpgWindowSeconds) }
for ($i = 0; $i -lt $rows.Count; $i++) {
    $row = $rows[$i]
    $time = Read-RecordingNumber $row.shimmer_elapsed_s
    if ($row.eda_us -ne '') {
        $chart.Series['EDA'].Points.AddXY($time, (Read-RecordingNumber $row.eda_us)) | Out-Null
    } else {
        $point = $chart.Series['EDA'].Points.AddXY($time, 0)
        $chart.Series['EDA'].Points[$point].IsEmpty = $true
    }
    if ($time -ge $PpgStartSeconds -and $time -le $PpgStartSeconds + $PpgWindowSeconds -and $row.ppg_mv -ne '') {
        $chart.Series['PPG'].Points.AddXY($time, (Read-RecordingNumber $row.ppg_mv)) | Out-Null
    }
}
$chart.ChartAreas['EDA'].AxisX.Minimum = 0
$chart.ChartAreas['EDA'].AxisX.Maximum = $lastTime
$chart.ChartAreas['PPG'].AxisX.Minimum = $PpgStartSeconds
$chart.ChartAreas['PPG'].AxisX.Maximum = [Math]::Min($lastTime, $PpgStartSeconds + $PpgWindowSeconds)
$chart.SaveImage($OutputPath, [System.Windows.Forms.DataVisualization.Charting.ChartImageFormat]::Png)
$chart.Dispose()
Write-Output $OutputPath
