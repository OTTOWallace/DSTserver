# DSTserver 一键部署脚本
# 作用：下载 SteamCMD 并安装/更新饥荒联机版专用服务器（等价于启动器里的「安装 / 更新」按钮）
# 用法：以普通用户身份在 PowerShell 里运行  .\bootstrap.ps1

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$appDir = Join-Path $PSScriptRoot 'DSTserver\bin\Debug\net10.0-windows'
$steamDir = Join-Path $appDir 'runtime\steamcmd'

Write-Host "==> 目标目录: $steamDir"
New-Item -ItemType Directory -Path $steamDir -Force | Out-Null

# 1. SteamCMD
if (-not (Test-Path (Join-Path $steamDir 'steamcmd.exe'))) {
    Write-Host '==> 下载 SteamCMD ...'
    $zip = Join-Path $steamDir 'steamcmd.zip'
    Invoke-WebRequest -Uri 'https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip' -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $steamDir -Force
    Remove-Item $zip -Force
    Write-Host '==> SteamCMD 就绪'
} else {
    Write-Host '==> SteamCMD 已存在'
}

# 2. 安装 / 更新饥荒专用服务器（约 4.3 GB，视网速而定）
Write-Host '==> 安装 / 更新饥荒联机版专用服务器 ...'
Push-Location $steamDir
try {
    & .\steamcmd.exe +login anonymous +app_update 343050 validate +quit
} finally {
    Pop-Location
}

$exe = Join-Path $steamDir 'steamapps\common\Don''t Starve Together Dedicated Server\bin64\dontstarve_dedicated_server_nullrenderer_x64.exe'
if (Test-Path $exe) {
    Write-Host '==> 完成，服务器已就绪'
} else {
    Write-Warning '服务器主程序未找到，请检查上面的 steamcmd 输出'
}
