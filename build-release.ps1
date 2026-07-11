param(
    [string]$SigningCertificateThumbprint = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$publishDirectory = Join-Path $projectRoot "artifacts\publish"
$installerDirectory = Join-Path $projectRoot "artifacts\installer"

dotnet test (Join-Path $projectRoot "VibeDLNA.Tests\VibeDLNA.Tests.csproj") -c Release
dotnet publish (Join-Path $projectRoot "FolderDlnaServer.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDirectory

$executable = Join-Path $publishDirectory "VibeDLNA.exe"
if (-not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
    $signTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -eq $signTool) {
        throw "signtool.exe no esta disponible. Instala el Windows SDK para firmar la aplicacion."
    }

    & $signTool.Source sign /sha1 $SigningCertificateThumbprint /fd SHA256 /td SHA256 `
        /tr "http://timestamp.digicert.com" $executable
}

$innoCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$innoCompiler = $innoCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($null -ne $innoCompiler) {
    New-Item -ItemType Directory -Force -Path $installerDirectory | Out-Null
    & $innoCompiler "/O$installerDirectory" (Join-Path $projectRoot "installer\VibeDLNA.iss")
    Write-Host "Instalador creado en $installerDirectory"
} else {
    Write-Host "Publicacion creada en $publishDirectory"
    Write-Host "Instala Inno Setup 6 para generar tambien VibeDLNA-Setup.exe."
}
