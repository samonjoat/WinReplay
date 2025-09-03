param(
    [switch]$Clean,
    [switch]$Build,
    [switch]$Publish,
    [switch]$Release,
    [switch]$CreateInstaller,
    [string]$OutputPath = ".\publish"
)

# Script configuration
$ProjectName = "WinRelay"
$ProjectFile = "$ProjectName.csproj"
$Configuration = if ($Release) { "Release" } else { "Debug" }
$RuntimeIdentifier = "win-x64"

# Colors for output
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Test-Prerequisites {
    Write-ColorOutput "Checking prerequisites..." $InfoColor
    
    # Check if .NET 6.0 SDK is installed
    try {
        $dotnetVersion = dotnet --version
        Write-ColorOutput "✓ .NET SDK version: $dotnetVersion" $SuccessColor
    }
    catch {
        Write-ColorOutput "✗ .NET SDK not found. Please install .NET 6.0 SDK." $ErrorColor
        exit 1
    }
    
    # Check if project file exists
    if (-not (Test-Path $ProjectFile)) {
        Write-ColorOutput "✗ Project file '$ProjectFile' not found." $ErrorColor
        exit 1
    }
    Write-ColorOutput "✓ Project file found" $SuccessColor
    
    Write-ColorOutput "Prerequisites check completed." $SuccessColor
    Write-Host ""
}

function Invoke-Clean {
    Write-ColorOutput "Cleaning project..." $InfoColor
    
    try {
        # Clean bin and obj directories
        if (Test-Path "bin") { Remove-Item -Path "bin" -Recurse -Force }
        if (Test-Path "obj") { Remove-Item -Path "obj" -Recurse -Force }
        if (Test-Path $OutputPath) { Remove-Item -Path $OutputPath -Recurse -Force }
        
        # Run dotnet clean
        dotnet clean $ProjectFile --configuration $Configuration
        
        Write-ColorOutput "✓ Clean completed successfully." $SuccessColor
    }
    catch {
        Write-ColorOutput "✗ Clean failed: $($_.Exception.Message)" $ErrorColor
        exit 1
    }
    Write-Host ""
}

function Invoke-Build {
    Write-ColorOutput "Building project ($Configuration)..." $InfoColor
    
    try {
        $buildArgs = @(
            "build"
            $ProjectFile
            "--configuration", $Configuration
            "--verbosity", "minimal"
        )
        
        dotnet @buildArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✓ Build completed successfully." $SuccessColor
        } else {
            Write-ColorOutput "✗ Build failed with exit code $LASTEXITCODE." $ErrorColor
            exit $LASTEXITCODE
        }
    }
    catch {
        Write-ColorOutput "✗ Build failed: $($_.Exception.Message)" $ErrorColor
        exit 1
    }
    Write-Host ""
}

function Invoke-Publish {
    Write-ColorOutput "Publishing project ($Configuration, $RuntimeIdentifier)..." $InfoColor
    
    try {
        # Ensure output directory exists
        if (-not (Test-Path $OutputPath)) {
            New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
        }
        
        $publishArgs = @(
            "publish"
            $ProjectFile
            "--configuration", $Configuration
            "--runtime", $RuntimeIdentifier
            "--self-contained", "true"
            "--output", $OutputPath
            "/p:PublishSingleFile=true"
            "/p:PublishTrimmed=true"
            "/p:TrimMode=link"
            "/p:PublishReadyToRun=true"
            "--verbosity", "minimal"
        )
        
        dotnet @publishArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✓ Publish completed successfully." $SuccessColor
            
            # Show output information
            $outputFile = Join-Path $OutputPath "$ProjectName.exe"
            if (Test-Path $outputFile) {
                $fileInfo = Get-Item $outputFile
                $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
                Write-ColorOutput "   Output: $outputFile" $InfoColor
                Write-ColorOutput "   Size: $fileSizeMB MB" $InfoColor
            }
        } else {
            Write-ColorOutput "✗ Publish failed with exit code $LASTEXITCODE." $ErrorColor
            exit $LASTEXITCODE
        }
    }
    catch {
        Write-ColorOutput "✗ Publish failed: $($_.Exception.Message)" $ErrorColor
        exit 1
    }
    Write-Host ""
}

function Invoke-CreateInstaller {
    Write-ColorOutput "Creating installer package..." $InfoColor
    
    # Check if WiX is available (simplified check)
    try {
        $wixPath = Get-Command "candle.exe" -ErrorAction SilentlyContinue
        if (-not $wixPath) {
            Write-ColorOutput "⚠ WiX Toolset not found in PATH. Installer creation skipped." $WarningColor
            Write-ColorOutput "   To create installers, install WiX Toolset from: https://wixtoolset.org/" $InfoColor
            return
        }
        
        Write-ColorOutput "✓ WiX Toolset found" $SuccessColor
        Write-ColorOutput "   Installer creation would be implemented here with WiX configuration." $InfoColor
        
        # In a real implementation, you would:
        # 1. Create WiX source files (.wxs)
        # 2. Compile with candle.exe
        # 3. Link with light.exe
        # 4. Generate MSI package
        
    }
    catch {
        Write-ColorOutput "✗ Installer creation failed: $($_.Exception.Message)" $ErrorColor
    }
    Write-Host ""
}

function Show-Usage {
    Write-ColorOutput "WinRelay Build Script" $InfoColor
    Write-Host ""
    Write-ColorOutput "Usage:" $InfoColor
    Write-ColorOutput "  .\build.ps1 [OPTIONS]" $InfoColor
    Write-Host ""
    Write-ColorOutput "Options:" $InfoColor
    Write-ColorOutput "  -Clean           Clean build artifacts" $InfoColor
    Write-ColorOutput "  -Build           Build the project" $InfoColor
    Write-ColorOutput "  -Publish         Publish as single-file executable" $InfoColor
    Write-ColorOutput "  -Release         Use Release configuration (default: Debug)" $InfoColor
    Write-ColorOutput "  -CreateInstaller Create MSI installer (requires WiX)" $InfoColor
    Write-ColorOutput "  -OutputPath      Specify output directory (default: .\publish)" $InfoColor
    Write-Host ""
    Write-ColorOutput "Examples:" $InfoColor
    Write-ColorOutput "  .\build.ps1 -Clean -Build" $InfoColor
    Write-ColorOutput "  .\build.ps1 -Publish -Release" $InfoColor
    Write-ColorOutput "  .\build.ps1 -Clean -Publish -Release -CreateInstaller" $InfoColor
    Write-Host ""
}

# Main execution
Write-ColorOutput "=== WinRelay Build Script ===" $InfoColor
Write-Host ""

# Show usage if no parameters provided
if (-not ($Clean -or $Build -or $Publish -or $CreateInstaller)) {
    Show-Usage
    exit 0
}

# Check prerequisites
Test-Prerequisites

# Execute requested operations
if ($Clean) {
    Invoke-Clean
}

if ($Build) {
    Invoke-Build
}

if ($Publish) {
    Invoke-Publish
}

if ($CreateInstaller) {
    Invoke-CreateInstaller
}

Write-ColorOutput "=== Build Script Completed ===" $SuccessColor
Write-Host ""