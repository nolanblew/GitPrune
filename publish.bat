@ECHO OFF
REM A publish script that takes in an optional argument to specify the version number.
REM If no version number is specified, the version will be 999.0.0
REM If a version number is specified, it will be used.

SET VERSION=%1
IF "%VERSION%" == "" (
  SET VERSION="999.0.0"
)

IF "%VERSION:~-1%" neq "b" (
  SET BETA="/p:IsBeta=false"
) ELSE (
  SET BETA="/p:IsBeta=true"
  SET VERSION=%VERSION:~0,-1%
)

ECHO "Publishing version %VERSION%"

dotnet publish -r win-x64 --configuration Release --self-contained true -p:PublishSingleFile=true -p:Version="%VERSION%" %BETA% -p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish -r linux-x64 --configuration Release --self-contained true -p:PublishSingleFile=true -p:Version="%VERSION%" %BETA% -p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish -r osx-x64 --configuration Release --self-contained true -p:PublishSingleFile=true -p:Version="%VERSION%" %BETA% -p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true