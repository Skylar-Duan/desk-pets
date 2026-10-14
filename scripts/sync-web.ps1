$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$qaRoot = Join-Path $projectRoot '_work\qa'
New-Item -ItemType Directory -Path $qaRoot -Force | Out-Null
Push-Location $projectRoot
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$qaRoot\ExportWebAssets.exe" /r:System.Drawing.dll /r:System.Web.Extensions.dll scripts\ExportWebAssets.cs src\PetDef.cs
    if ($LASTEXITCODE -ne 0) { throw '网页素材导出器编译失败' }
    & (Join-Path $qaRoot 'ExportWebAssets.exe') (Join-Path $projectRoot 'pets') (Join-Path $projectRoot 'web\assets')
    if ($LASTEXITCODE -ne 0) { throw '网页素材导出失败' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'backgrounds') -Destination (Join-Path $projectRoot 'web') -Recurse -Force
} finally { Pop-Location }
