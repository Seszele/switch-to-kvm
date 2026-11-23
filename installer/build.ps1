param(
    [string]$Version = "0.0.0",
    [switch]$NoInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"
$symbols = Join-Path $artifacts "symbols"

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }

foreach ($project in "SwitchToKVM.App", "SwitchToKVM.Cli") {
    dotnet publish (Join-Path $root "src\$project") -c Release -r win-x64 -o $publish "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed" }
}

New-Item -ItemType Directory -Force $symbols | Out-Null
Move-Item (Join-Path $publish "*.pdb") $symbols

Compress-Archive -Path (Join-Path $publish "*") -DestinationPath (Join-Path $artifacts "SwitchToKVM-$Version-portable.zip")

if ($NoInstaller) { return }

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if ($iscc) {
    $iscc = $iscc.Source
} else {
    $iscc = Get-ChildItem "C:\Program Files*\Inno Setup*\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup*\ISCC.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $iscc) { throw "Inno Setup (ISCC.exe) not found. Install it from https://jrsoftware.org/isinfo.php or run with -NoInstaller." }

& $iscc "/DAppVersion=$Version" "/DSourceDir=$publish" "/O$artifacts" (Join-Path $PSScriptRoot "SwitchToKVM.iss")
if ($LASTEXITCODE -ne 0) { throw "Building the installer failed" }
