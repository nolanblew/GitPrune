#!/bin/bash
if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (use sudo)."
    exit
fi

platform='unknown'
platform="$(uname | tr '[:upper:]' '[:lower:]')"

echo "Welcme to the Git Prune installer!"
echo "Downloading GitPrune..."

directory='/usr/local/bin/git-prune'

case $platform in
    linux)
        curl -O "https://nolanblew.blob.core.windows.net/git-prune/gitprune-linux-x64.tar.gz"
        echo "Extracting Git Prune..."
        if [[ ! -d $directory ]]; then
             mkdir -p $directory
        fi
        tar xzf gitprune-linux-x64.tar.gz -C $directory

        # Install browser extention if not installed
        dpkg -s xdg-utils &> /dev/null

        if [ $? -ne 0 ]; then
            echo "Installing prerequisites..."
            sudo apt install -y -qq xdg-utils
        fi

        echo "Cleaning Up..."
        rm gitprune-linux-x64.tar.gz

        rcfile='~/.profile'

        echo "LAST INSTALLATION STEP: Make sure to copy and paste the following line into your console:"
        echo
        echo "echo 'export PATH=\"\$PATH:$directory\"' >> $rcfile && source $rcfile"
        
        echo
        echo "Installed successfully. To run Git Prune, just run: GitPrune (after running the above command)"
        echo "Feel free to run the following to add 'gprune' to your alias: echo \"alias gprune='$directory/GitPrune'\" >> $rcfile && source $rcfile"
        ;;
    darwin)
        curl -O "https://nolanblew.blob.core.windows.net/git-prune/gitprune-osx-x64.tar.gz"
        echo "Extracting Git Prune..."
        if [[ ! -d /usr/local/bin/git-prune ]]; then
            mkdir -p /usr/local/bin/git-prune
        fi
        tar xzf gitprune-osx-x64.tar.gz -C /usr/local/bin/git-prune
        
        echo "Cleaning Up..."
        rm gitprune-osx-x64.tar.gz

        echo "Installed successfully. Please run the following command to add to your profile:"
        
        if [[ -f "~/.zshrc" ]]; then
            rcfile="~/.zshrc"
        else
            rcfile="~/.cshrc"
        fi

        echo "LAST INSTALLATION STEP: Make sure to copy and paste the following line into your console:"
        echo
        echo "echo 'export PATH=\"\$PATH:$directory\"' >> $rcfile && source $rcfile"
        
        echo
        echo "Installed successfully. To run Git Prune, just run: GitPrune (after running the above command)"
        echo "Feel free to run the following to add 'gprune' to your alias: echo \"alias gprune='$directory/GitPrune'\" >> $rcfile && source $rcfile"

        ;;
    mysys|windowsnt|cygwin)
        curl -O "https://nolanblew.blob.core.windows.net/git-prune/gitprune-win-x64.zip"
        echo "Extracting Git Prune..."
        mkdir gprune
        unzip -q gitprune-win-x64.tar.gz -d /git-prune
        echo "Cleaning Up..."
        rm gitprune-win-x64.tar.gz
        ;;
esac
echo "Finished!"