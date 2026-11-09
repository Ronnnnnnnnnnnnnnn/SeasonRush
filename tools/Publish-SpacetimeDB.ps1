<#
.SYNOPSIS
  Publishes the Season Rush database to SpacetimeDB Maincloud and tells the Unity project which database to use.

.DESCRIPTION
  Run this ONCE from a normal PowerShell window (not inside Unity):

      cd "<project folder>"
      powershell -ExecutionPolicy Bypass -File tools\Publish-SpacetimeDB.ps1

  What it does:
    1. Installs the SpacetimeDB command line tool if it is missing (the official installer from spacetimedb.com).
    2. Opens your browser so you can log in to your own SpacetimeDB account (free).
    3. Publishes the ready-built module in spacetimedb\dist\ - no .NET, Node or other build tools needed.
    4. Writes the database name into Assets\Resources\SpacetimeConfig.asset so the game connects to it.

  Run it again at any time to update the module; your data is kept unless the tables change in a breaking way.
#>
param(
    [string]$DatabaseName = "",
    [string]$Server = "maincloud"
)

$ErrorActionPreference = "Stop"
$root   = Split-Path -Parent $PSScriptRoot
$config = Join-Path $root "Assets\Resources\SpacetimeConfig.asset"

# The module is shipped already built: a .wasm file (C# module) or a .js bundle (TypeScript module).
$wasm = Join-Path $root "spacetimedb\dist\seasonrush.wasm"
$js   = Join-Path $root "spacetimedb\dist\bundle.js"
if (Test-Path $wasm)   { $artifact = @("--bin-path", $wasm); $artifactPath = $wasm }
elseif (Test-Path $js) { $artifact = @("--js-path", $js);    $artifactPath = $js }
else { throw "Cannot find the built module (looked for $wasm and $js)." }

# ---- 1. the command line tool ------------------------------------------------------------------
if (-not (Get-Command spacetime -ErrorAction SilentlyContinue)) {
    Write-Host "Installing the SpacetimeDB command line tool (official installer)..." -ForegroundColor Cyan
    Invoke-Expression (Invoke-WebRequest "https://windows.spacetimedb.com" -UseBasicParsing).Content
    $env:Path += ";" + (Join-Path $env:LOCALAPPDATA "SpacetimeDB")
    if (-not (Get-Command spacetime -ErrorAction SilentlyContinue)) {
        throw "The 'spacetime' command is still not available. Close this window, open a new PowerShell and run the script again."
    }
}
Write-Host ("Using " + (& spacetime --version | Select-Object -Last 1)) -ForegroundColor DarkGray

# ---- 2. login ----------------------------------------------------------------------------------
Write-Host "`nLogging in to SpacetimeDB (a browser window opens)..." -ForegroundColor Cyan
& spacetime login
if ($LASTEXITCODE -ne 0) { throw "Login failed." }

# ---- 3. database name --------------------------------------------------------------------------
if (-not $DatabaseName) {
    $suggestion = "seasonrush-" + (($env:USERNAME -replace "[^a-zA-Z0-9]", "").ToLower())
    $DatabaseName = Read-Host "`nDatabase name (lowercase letters, numbers and dashes) [$suggestion]"
    if (-not $DatabaseName) { $DatabaseName = $suggestion }
}
if ($DatabaseName -notmatch '^[a-z0-9]+(-[a-z0-9]+)*$') {
    throw "Invalid name '$DatabaseName'. Use only lowercase letters and numbers, separated by single dashes."
}

# ---- 4. publish --------------------------------------------------------------------------------
Write-Host "`nPublishing '$DatabaseName' to $Server ..." -ForegroundColor Cyan
& spacetime publish --server $Server @artifact $DatabaseName
if ($LASTEXITCODE -ne 0) {
    throw "Publishing failed. If the name is already taken by someone else, run the script again with another name."
}

# ---- 5. tell the Unity project ------------------------------------------------------------------
if (Test-Path $config) {
    $text = [System.IO.File]::ReadAllText($config)
    $text = [regex]::Replace($text, '(?m)^(\s*database:).*$', "`$1 $DatabaseName")
    [System.IO.File]::WriteAllText($config, $text)
    Write-Host "`nDone. The Unity project now uses the database '$DatabaseName'." -ForegroundColor Green
} else {
    Write-Host "`nDone. Open Assets/Resources/SpacetimeConfig.asset in Unity and type '$DatabaseName' into the Database field." -ForegroundColor Yellow
}
Write-Host "Start the game: the menu shows 'Online database: connected'."
