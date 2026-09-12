param(
    [string]$SigningCertificateThumbprint = "",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$publishDirectory = Join-Path $projectRoot "artifacts\publish"
$installerDirectory = Join-Path $projectRoot "artifacts\installer"
$releaseDirectory = Join-Path $projectRoot "artifacts\release"

$normalizedVersion = ""
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $versionText = $Version.Trim()
    if ($versionText.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $versionText = $versionText.Substring(1)
    }

    $parsedVersion = $null
    if (-not [System.Version]::TryParse($versionText, [ref]$parsedVersion)) {
        throw "La version '$Version' no es valida. Usa, por ejemplo, v1.0.1."
    }

    $normalizedVersion = "{0}.{1}.{2}" -f $parsedVersion.Major, $parsedVersion.Minor, [Math]::Max($parsedVersion.Build, 0)
}

& dotnet test (Join-Path $projectRoot "VibeDLNA.Tests\VibeDLNA.Tests.csproj") -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Las pruebas no pasaron."
}

foreach ($directory in @($publishDirectory, $installerDirectory, $releaseDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

$publishArguments = @(
    (Join-Path $projectRoot "VibeDLNA.csproj"),
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-o", $publishDirectory
)
if (-not [string]::IsNullOrWhiteSpace($normalizedVersion)) {
    $publishArguments += "-p:VersionPrefix=$normalizedVersion"
}

& dotnet publish @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "La publicacion no paso."
}

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
    $innoArguments = @(
        "/O$installerDirectory",
        (Join-Path $projectRoot "installer\VibeDLNA.iss")
    )
    if (-not [string]::IsNullOrWhiteSpace($normalizedVersion)) {
        $innoArguments += "/DMyAppVersion=$normalizedVersion"
    }

    & $innoCompiler @innoArguments
    if ($LASTEXITCODE -ne 0) {
        throw "La compilacion del instalador no paso."
    }
    Write-Host "Instalador creado en $installerDirectory"
} else {
    Write-Host "Publicacion creada en $publishDirectory"
    Write-Host "Instala Inno Setup 6 para generar tambien VibeDLNA-Setup.exe."
}

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
$portableZip = Join-Path $releaseDirectory "VibeDLNA-windows.zip"
$portableChecksum = Join-Path $releaseDirectory "VibeDLNA-windows.zip.sha256"
Compress-Archive -LiteralPath $executable -DestinationPath $portableZip -CompressionLevel Optimal
$portableHash = (Get-FileHash -LiteralPath $portableZip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $portableChecksum -Value ("{0} *{1}" -f $portableHash, (Split-Path -Leaf $portableZip)) -Encoding ascii
Write-Host "Paquete portable creado en $portableZip"
Write-Host "SHA-256 escrito en $portableChecksum"
