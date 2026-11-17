$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Add-Type -Path @(
    (Join-Path $projectRoot 'Assets/Shimmer/RecordingCsv.cs'),
    (Join-Path $projectRoot 'Assets/Shimmer/EyeClockAlignment.cs'),
    (Join-Path $projectRoot 'Assets/Shimmer/ShimmerClockAlignment.cs')
)
$testFolder = Join-Path $projectRoot 'Logs/RecordingCsvChecks'
New-Item -ItemType Directory -Force -Path $testFolder | Out-Null
$loggerPath = Join-Path $testFolder 'Logger.csv'
$message = "Shot, hit; target `"café`"`nSecond line"
$writer = [AngerRoom.Shimmer.RecordingCsv]::Create($loggerPath, 'session_id,session_time_s,message')
try {
    [AngerRoom.Shimmer.RecordingCsv]::WriteRow($writer, [string[]]@('fixture', [AngerRoom.Shimmer.RecordingCsv]::Number(1234.567), $message))
} finally { $writer.Dispose() }
$bytes = [IO.File]::ReadAllBytes($loggerPath)
if ($bytes[0] -ne 239 -or $bytes[1] -ne 187 -or $bytes[2] -ne 191) { throw 'UTF-8 BOM missing' }
$text = [IO.File]::ReadAllText($loggerPath)
if (-not $text.StartsWith("sep=;`r`n")) { throw 'Excel delimiter directive missing' }
$body = $text.Substring($text.IndexOf("`n") + 1)
$rows = @($body | ConvertFrom-Csv -Delimiter ';')
if ($rows.Count -ne 1 -or @($rows[0].PSObject.Properties).Count -ne 3 -or $rows[0].message -cne $message) { throw 'Logger quoting/columns changed' }
$spanish = [Globalization.CultureInfo]::GetCultureInfo('es-ES')
if ([double]::Parse($rows[0].session_time_s, $spanish) -ne 1234.567) { throw 'Numeric precision/locale mismatch' }
$decoded = @([AngerRoom.Shimmer.RecordingCsv]::ReadRows($loggerPath))
if ($decoded.Count -ne 2 -or $decoded[1][2] -cne $message) { throw 'CSV round trip failed' }
$aligned = Join-Path $testFolder 'technical.csv'
$source = "session_id,session_time_s,connection_segment,sequence,eda_us,ppg_mv,eda_conversion_valid,ppg_adc_valid,gsr_range,gsr_range_changed,estimated_missing_samples`nfixture,1234.567,2,42,,1500.3663,false,true,2,false,3`n"
[IO.File]::WriteAllText($aligned, $source)
$readable = Join-Path $testFolder 'shimmer.csv'
$count = [AngerRoom.Shimmer.ShimmerClockAlignment]::ExportReadable($aligned, $readable)
$shimmerText = [IO.File]::ReadAllText($readable)
$shimmer = @($shimmerText.Substring($shimmerText.IndexOf("`n") + 1) | ConvertFrom-Csv -Delimiter ';')
if ($count -ne 1 -or $shimmer.Count -ne 1 -or @($shimmer[0].PSObject.Properties).Count -ne 15) { throw 'Shimmer row/column count mismatch' }
if ($shimmer[0].eda_us -ne '' -or $shimmer[0].ppg_mv -ne '1500,3663' -or $shimmer[0].estimated_missing_samples -ne '3') { throw 'Invalid EDA/PPG/gap data altered' }
if ([AngerRoom.Shimmer.ShimmerClockAlignment]::ExportReadable($aligned, $readable) -ne 1) { throw 'Repeated export failed' }
if ([IO.File]::ReadAllText($aligned) -cne $source) { throw 'Technical data altered' }
Write-Output 'CSV_FILE_CHECKS_OK: BOM, explicit delimiter, 3 Logger columns, quoted multiline/Unicode text, decimal precision, CSV round trip, 15 Shimmer columns, missing EDA/gap preservation, repeated export, untouched technical input.'
