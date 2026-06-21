# ============================================================================
#  publish.ps1  -  Builds a self-contained, single-file Windows x64 EXE.
#  Run from this folder:   powershell -ExecutionPolicy Bypass -File .\publish.ps1
#  Output EXE:
#    ShareDealAttend\bin\Release\net8.0-windows\win-x64\publish\ShareDealAttend.exe
# ============================================================================

$ErrorActionPreference = "Stop"
$proj = Join-Path $PSScriptRoot "ShareDealAttend\ShareDealAttend.csproj"

Write-Host "Restoring & publishing ShareDeal Attend (self-contained, single-file)..." -ForegroundColor Cyan

dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish FAILED." -ForegroundColor Red
    exit $LASTEXITCODE
}

$out = Join-Path $PSScriptRoot "ShareDealAttend\bin\Release\net8.0-windows\win-x64\publish\ShareDealAttend.exe"
Write-Host "`nDone. EXE created at:" -ForegroundColor Green
Write-Host "  $out" -ForegroundColor Green
