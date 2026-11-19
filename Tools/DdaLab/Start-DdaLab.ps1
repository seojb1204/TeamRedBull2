param(
    [string]$Dotnet = "dotnet",
    [ValidateRange(1,65535)][int]$Port = 8765,
    [string]$DataDirectory = "",
    [switch]$MonitorOnly
)
$ErrorActionPreference = "Stop"
$LabScriptPath = Join-Path $PSScriptRoot "lab.py"
if (-not $DataDirectory) { $DataDirectory = Join-Path $env:LOCALAPPDATA "ReversalTower-DdaLab" }
$LabArguments = @($LabScriptPath, "--port", "$Port", "--data-dir", $DataDirectory)
if ($MonitorOnly) { $LabArguments += "--monitor-only" }
else { $LabArguments += @("--dotnet", $Dotnet) }
Write-Host "DDA Lab: http://127.0.0.1:$Port (local only)"
Write-Host "Simulation needs Python 3.10+ and .NET 8 SDK. -MonitorOnly needs Python only."
if (Get-Command py -ErrorAction SilentlyContinue) { & py -3 @LabArguments }
elseif (Get-Command python -ErrorAction SilentlyContinue) { & python @LabArguments }
else { throw "Install Python 3.10+ or run lab.py with your Python executable." }
exit $LASTEXITCODE
