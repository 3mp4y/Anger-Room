param(
    [Parameter(Mandatory=$true)][string]$SessionFolder
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sessionPath = (Resolve-Path -LiteralPath $SessionFolder).Path
$sourceFiles = @(
    (Join-Path $projectRoot 'Assets/Shimmer/RecordingCsv.cs'),
    (Join-Path $projectRoot 'Assets/Shimmer/EyeClockAlignment.cs'),
    (Join-Path $projectRoot 'Assets/Shimmer/ShimmerClockAlignment.cs')
)
Add-Type -Path $sourceFiles
$diagnosticPath = Join-Path $sessionPath 'diagnostics'
$rawPath = Join-Path $diagnosticPath 'shimmer_raw.csv'
if (-not (Test-Path -LiteralPath $rawPath)) {
    $rawPath = Join-Path $sessionPath 'shimmer_raw.csv'
}
if (-not (Test-Path -LiteralPath $rawPath)) {
    throw 'shimmer_raw.csv is missing. Copy the entire session folder from the headset first.'
}
$outputPath = Join-Path $sessionPath 'shimmer.csv'
New-Item -ItemType Directory -Force -Path $diagnosticPath | Out-Null
$alignedPath = Join-Path $diagnosticPath 'shimmer_aligned.csv'
$rows = [AngerRoom.Shimmer.ShimmerClockAlignment]::Export($rawPath, $alignedPath)
[void][AngerRoom.Shimmer.ShimmerClockAlignment]::ExportReadable($alignedPath, $outputPath)
Write-Output "Aligned $rows samples into $outputPath. Fixed Bluetooth latency remains unknown; raw data was preserved."
