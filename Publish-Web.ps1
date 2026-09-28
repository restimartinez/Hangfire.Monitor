# Publishes Hangfire.Monitor.Web into Deployment\Build yyyyMMdd HHhmm
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$webProject = Join-Path $repoRoot "Hangfire.Monitor.Web\Hangfire.Monitor.Web.csproj"
$deploymentRoot = Join-Path $repoRoot "Deployment"

if (-not (Test-Path $webProject)) {
    throw "Web project not found: $webProject"
}

$buildFolderName = "Build " + (Get-Date -Format "yyyyMMdd HH\hmm")
$outputPath = Join-Path $deploymentRoot $buildFolderName

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

Write-Host "Publishing Hangfire.Monitor.Web to:"
Write-Host "  $outputPath"
Write-Host ""

dotnet publish $webProject `
    --configuration Release `
    --output $outputPath `
    --no-self-contained

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Publish completed: $outputPath"
