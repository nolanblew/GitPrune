#!/bin/bash
platform='unknown'
platform = "$(uname | tr '[:upper:]' '[:lower:]')"

case $platform in
    linux)
        curl -O ""
        ;;
    darwin)
        curl -O ""
        ;;
    mysys|windowsnt|cygwin)
        curl -O ""
        ;;
esac