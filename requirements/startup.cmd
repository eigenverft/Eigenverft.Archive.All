@echo off
REM This script installs .NET SDKs and tools on Windows using PowerShell.
echo "Starting .NET SDK installation..."
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 1.1"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 2.2"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 3.1"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 5.0"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 6.0"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 7.0"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 8.0"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 9.0"
powershell -NoProfile -ExecutionPolicy Unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -channel 10.0"

REM --- Current session (take precedence over machine-wide) ---
SET "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
SET "DOTNET_MULTILEVEL_LOOKUP=0"
SET "PATH=%DOTNET_ROOT%;%USERPROFILE%\.dotnet\tools;%PATH%"

REM --- Persist to HKCU for future consoles ---
REM DOTNET_ROOT (per-user SDK/host)
powershell -NoProfile -ExecutionPolicy Unrestricted -Command ^
  "$root=[Environment]::ExpandEnvironmentVariables('%%LOCALAPPDATA%%\Microsoft\dotnet'); [Environment]::SetEnvironmentVariable('DOTNET_ROOT',$root,'User')"

REM DOTNET_MULTILEVEL_LOOKUP (disable fallback to machine installs)
powershell -NoProfile -ExecutionPolicy Unrestricted -Command ^
  "[Environment]::SetEnvironmentVariable('DOTNET_MULTILEVEL_LOOKUP','0','User')"

REM User PATH: prepend %LOCALAPPDATA%\Microsoft\dotnet and %USERPROFILE%\.dotnet\tools (no duplicates, case-insensitive)
powershell -NoProfile -ExecutionPolicy Unrestricted -Command ^
  "$root=[Environment]::ExpandEnvironmentVariables('%%LOCALAPPDATA%%\Microsoft\dotnet'); $tools=[Environment]::ExpandEnvironmentVariables('%%USERPROFILE%%\.dotnet\tools');" ^
  " $u=[Environment]::GetEnvironmentVariable('Path','User') -as [string]; if(-not $u){$u=''};" ^
  " $cmp=(';'+$u+';').ToLowerInvariant(); $add=New-Object System.Collections.Generic.List[string];" ^
  " foreach($t in @($root,$tools)){ $t2=$t.TrimEnd('\','/'); if(-not $cmp.Contains((';'+$t2+';').ToLowerInvariant())){ [void]$add.Add($t2) } }" ^
  " [Environment]::SetEnvironmentVariable('Path', (($add + ($u -split ';')) -join ';'),'User')"


dotnet tool install --global Powershell --no-cache
echo "Startup script completed. You can now use .NET and PowerShell tools."

REM Disable the "Get Help" context menu in Windows 11
REM reg.exe add "HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32" /f /ve
pause
exit /b 0