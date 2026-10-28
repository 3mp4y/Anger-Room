# Run from any directory with PowerShell. Only the plain C# timer is exercised.
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'CountdownTimer.cs')
$script:checks = 0
function Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
function Rejects([scriptblock]$action) {
    try { & $action; return $false }
    catch {
        $errorObject = $_.Exception
        while ($null -ne $errorObject.InnerException) { $errorObject = $errorObject.InnerException }
        return $errorObject -is [ArgumentOutOfRangeException]
    }
}

$timer = [AngerRoom.Examples.Timers.CountdownTimer]::new()
Check (-not $timer.Tick(2)) 'A new timer must not expire.'
$timer.Start(10)
Check (-not $timer.Tick(3)) 'Expired too early.'
Check ($timer.RemainingSeconds -eq 7) 'Expected seven seconds.'
$timer.Pause()
Check ($timer.IsPaused -and -not $timer.IsRunning) 'Pause state is inconsistent.'
Check (-not $timer.Tick(100)) 'Paused timer expired.'
Check ($timer.RemainingSeconds -eq 7) 'Paused time changed.'
$timer.Resume()
Check ($timer.Tick(20)) 'Overshooting zero must report expiry.'
Check ($timer.RemainingSeconds -eq 0 -and -not $timer.IsRunning) 'Expiry did not clamp/stop.'
Check (-not $timer.Tick(1)) 'Expiry reported twice.'
$timer.Resume()
Check (-not $timer.IsRunning) 'Resume restarted an expired timer.'
$timer.Start(5)
Check ($timer.Tick(5)) 'Exact boundary did not expire.'
$timer.Start(5)
$timer.Stop()
Check (-not $timer.Tick(10)) 'Cancellation reported expiry.'
$timer.Resume()
Check (-not $timer.IsRunning) 'Resume restarted a cancelled timer.'
$timer.Start(8)
$timer.Pause()
$timer.Start(4)
Check ($timer.IsRunning -and $timer.RemainingSeconds -eq 4) 'Start did not replace the paused countdown.'
$other = [AngerRoom.Examples.Timers.CountdownTimer]::new()
$other.Start(30)
$null = $timer.Tick(1)
Check ($other.RemainingSeconds -eq 30) 'Instances share state.'
Check (-not $timer.Tick(0) -and $timer.RemainingSeconds -eq 3) 'Zero delta changed time.'
foreach ($duration in @(0, -1, [float]::NaN, [float]::PositiveInfinity)) {
    Check (Rejects { $timer.Start($duration) }) 'Invalid duration accepted.'
}
foreach ($delta in @(-1, [float]::NaN, [float]::PositiveInfinity)) {
    Check (Rejects { $timer.Tick($delta) }) 'Invalid elapsed time accepted.'
}
Write-Output "PASS: $script:checks countdown checks."
