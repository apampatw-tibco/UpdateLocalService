REM Install .NET Core (https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -useb 'https://dot.net/v1/dotnet-install.ps1')))-Channel LTS"

SET PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%
dotnet run --project ./UpdateLocalService/UpdateLocalService.fsproj -t %*
