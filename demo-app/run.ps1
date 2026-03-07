# PowerShell script to run the demo app
# This requires .NET Framework and IIS Express or Visual Studio

Write-Host "CSPGuardian Demo Application" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET Framework is available
$dotnetVersion = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\" -Name Release -ErrorAction SilentlyContinue

if (-not $dotnetVersion) {
    Write-Host "ERROR: .NET Framework 4.7.2 or later is required!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install:" -ForegroundColor Yellow
    Write-Host "  1. .NET Framework 4.7.2 or later" -ForegroundColor Yellow
    Write-Host "  2. Visual Studio 2019/2022 with ASP.NET MVC development tools" -ForegroundColor Yellow
    Write-Host "  OR" -ForegroundColor Yellow
    Write-Host "  3. IIS Express (included with Visual Studio)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternatively, you can:" -ForegroundColor Yellow
    Write-Host "  - Open this folder in Visual Studio" -ForegroundColor Yellow
    Write-Host "  - Press F5 to run" -ForegroundColor Yellow
    exit 1
}

Write-Host ".NET Framework detected" -ForegroundColor Green
Write-Host ""

# Try to find IIS Express
$iisExpressPath = "${env:ProgramFiles}\IIS Express\iisexpress.exe"
if (-not (Test-Path $iisExpressPath)) {
    $iisExpressPath = "${env:ProgramFiles(x86)}\IIS Express\iisexpress.exe"
}

if (Test-Path $iisExpressPath) {
    Write-Host "Starting IIS Express..." -ForegroundColor Yellow
    Write-Host "Application will be available at: http://localhost:8080" -ForegroundColor Green
    Write-Host ""
    Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
    Write-Host ""
    
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    & $iisExpressPath /path:"$scriptPath" /port:8080
} else {
    Write-Host "IIS Express not found. Please use one of these options:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Option 1: Open in Visual Studio" -ForegroundColor Cyan
    Write-Host "  1. Open Visual Studio" -ForegroundColor White
    Write-Host "  2. File > Open > Web Site" -ForegroundColor White
    Write-Host "  3. Select this folder" -ForegroundColor White
    Write-Host "  4. Press F5 to run" -ForegroundColor White
    Write-Host ""
    Write-Host "Option 2: Install IIS Express" -ForegroundColor Cyan
    Write-Host "  Download from: https://www.microsoft.com/en-us/download/details.aspx?id=48264" -ForegroundColor White
    Write-Host ""
    Write-Host "Option 3: Use .NET Core/5+ (requires conversion)" -ForegroundColor Cyan
    Write-Host "  This demo app is currently configured for .NET Framework MVC 5" -ForegroundColor White
}

