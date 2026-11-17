# Shared reader for current Excel CSVs and legacy invariant diagnostic CSVs.
function Read-RecordingCsv([string]$Path) {
    $reader = [IO.File]::OpenText((Resolve-Path -LiteralPath $Path).Path)
    try {
        $first = $reader.ReadLine()
        if ($first -eq 'sep=;') {
            $reader.ReadToEnd() | ConvertFrom-Csv -Delimiter ';'
        } else {
            ($first + "`n" + $reader.ReadToEnd()) | ConvertFrom-Csv -Delimiter ','
        }
    } finally { $reader.Dispose() }
}
function Read-RecordingNumber([string]$Value) {
    [double]::Parse($Value.Replace(',', '.'), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture)
}
