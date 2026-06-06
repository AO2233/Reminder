<#
.SYNOPSIS
    Builds the ReminderApp as a single-file executable for Windows x64.
#>

$ProjectFile = Join-Path $PSScriptRoot "ReminderApp.csproj"

Write-Host "Compiling ReminderApp as a Single File Executable (Release, win-x64)..." -ForegroundColor Cyan

# Build and publish as a self-contained, single-file executable with size optimizations
dotnet publish $ProjectFile -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=true /p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild completed successfully!" -ForegroundColor Green
    
    # Path where dotnet publish usually places the output
    $publishDir = Join-Path $PSScriptRoot "bin\Release\net8.0-windows10.0.17763.0\win-x64\publish"
    Write-Host "You can find the compiled exe in: $publishDir" -ForegroundColor Yellow
    
    # Open the folder in File Explorer
    Invoke-Item $publishDir
} else {
    Write-Host "`nBuild failed with exit code $LASTEXITCODE." -ForegroundColor Red
}

Write-Host "`nPress any key to exit..."
$Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
