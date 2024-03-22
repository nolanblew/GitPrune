#!/bin/bash
# A publish script that takes in an optional argument to specify the version number.
# If no version number is specified, the version will be 999.0.0
# If a version number is specified, it will be used.

VERSION=$1
if [ -z "$VERSION" ]; then
    VERSION="999.0.0"
fi

if [ "${VERSION: -1}" != "b" ]; then
    BETA="-p:IsBeta=false"
else
    BETA="-p:IsBeta=true"
    VERSION=${VERSION:0:${#VERSION}-1}
fi

echo "Building publish version $VERSION"

dotnet publish -r win-x64 --configuration Release --self-contained true -p:PublishSingleFile=true -p:Version="$VERSION" $BETA -p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish -r linux-x64 --configuration Release --self-contained true -p:PublishSingleFile=true -p:Version="$VERSION" $BETA -p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish -r osx-x64 --configuration Release --self-contained true -p:PublishSingleFile=true -p:Version="$VERSION" $BETA -p:IncludeAllContentForSelfExtract=true -p:IncludeNativeLibrariesForSelfExtract=true

echo "Done!"