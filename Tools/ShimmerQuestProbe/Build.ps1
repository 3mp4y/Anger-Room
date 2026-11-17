param(
    [string]$AndroidPlayer = 'C:/Program Files/Unity/Hub/Editor/2022.3.45f1/Editor/Data/PlaybackEngines/AndroidPlayer'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $project 'Builds/ShimmerQuestProbe'
$classes = Join-Path $output 'classes'
$dex = Join-Path $output 'dex'
$testClasses = Join-Path $output 'test-classes'
New-Item -ItemType Directory -Force $output,$classes,$dex,$testClasses | Out-Null
$jdk = Join-Path $AndroidPlayer 'OpenJDK/bin'
$sdk = Join-Path $AndroidPlayer 'SDK'
$buildTools = Join-Path $sdk 'build-tools/34.0.0'
$androidJar = Join-Path $sdk 'platforms/android-34/android.jar'
function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Recurse -Filter '*.java' | ForEach-Object FullName)
Invoke-Checked "$jdk/javac.exe" (@('-encoding','UTF-8','-source','8','-target','8','-classpath',$androidJar,'-d',$classes) + $sources)
Invoke-Checked "$jdk/javac.exe" @('-encoding','UTF-8','-d',$testClasses,(Join-Path $PSScriptRoot 'src/com/angerroom/shimmerprobe/ShimmerProtocol.java'),(Join-Path $PSScriptRoot 'tests/ProtocolTest.java'))
Invoke-Checked "$jdk/java.exe" @('-classpath',$testClasses,'ProtocolTest')
$compiled = @(Get-ChildItem $classes -Recurse -Filter '*.class' | ForEach-Object FullName)
Invoke-Checked "$jdk/java.exe" (@('-cp',"$buildTools/lib/d8.jar",'com.android.tools.r8.D8','--lib',$androidJar,'--min-api','26','--output',$dex) + $compiled)
$unsigned = Join-Path $output 'unsigned.apk'
$aligned = Join-Path $output 'aligned.apk'
$apk = Join-Path $output 'ShimmerConnectionTest.apk'
Invoke-Checked "$buildTools/aapt.exe" @('package','-f','-M',(Join-Path $PSScriptRoot 'AndroidManifest.xml'),'-I',$androidJar,'-F',$unsigned)
Push-Location $dex
try { Invoke-Checked "$buildTools/aapt.exe" @('add',$unsigned,'classes.dex') }
finally { Pop-Location }
Invoke-Checked "$buildTools/zipalign.exe" @('-f','4',$unsigned,$aligned)
$key = Join-Path $output 'test.keystore'
if (!(Test-Path $key)) {
    Invoke-Checked "$jdk/keytool.exe" @('-genkeypair','-keystore',$key,'-storepass','android','-keypass','android','-alias','shimmer-test','-keyalg','RSA','-keysize','2048','-validity','3650','-dname','CN=Local Shimmer Connection Test')
}
Invoke-Checked "$jdk/java.exe" @('-jar',"$buildTools/lib/apksigner.jar",'sign','--ks',$key,'--ks-key-alias','shimmer-test','--ks-pass','pass:android','--key-pass','pass:android','--out',$apk,$aligned)
Invoke-Checked "$jdk/java.exe" @('-jar',"$buildTools/lib/apksigner.jar",'verify','--verbose',$apk)
Write-Output "Built $apk"
