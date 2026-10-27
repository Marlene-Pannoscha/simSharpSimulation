param([string]$BuildPath = 'C:\Users\marle\arztdemo\Builds\SimSharpLive')
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $BuildPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $source 'index.html'))) {
    throw "Kein Unity-Webbuild unter $source gefunden."
}
$repo = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $repo 'PraxisSimulation.Server\wwwroot\unity'
$null = New-Item -ItemType Directory -Path $destination -Force
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $destination -Recurse -Force
Write-Output 'Lokaler Unity-Webbuild bereit: http://localhost:5080/unity/'
