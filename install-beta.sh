#!/bin/bash
platform='unknown'
platform="$(uname | tr '[:upper:]' '[:lower:]')"

echo "Welcme to the Git Prune installer!"
echo "Downloading GitPrune..."

directory="${HOME}/.git-prune"

case $platform in
    linux)
        echo "Directory is $directory!"
        curl -O "https://nolanblew.blob.core.windows.net/git-prune-beta/gitprune-linux-x64.tar.gz"
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

        rcfile="${HOME}/.profile"

        # Check if the path has the $directory. If not add it to ${HOME}/.profile
        if [[ :$PATH: != *:"$directory":* ]] ; then
            echo "export PATH=\"\$PATH:${directory}\"" >> $rcfile
        fi

        echo "alias gprune='$directory/GitPrune'" >> $rcfile
        echo
        echo "NEXT STEP: Run the following command to source the changes:"
        echo "source $rcfile"
        echo
        echo "Installed successfully. To run Git Prune, just run: GitPrune (after running the above command) or gprune (alias)"
        echo "You can edit your aliases by in $rcfile"
        ;;
    darwin)
        curl -O "https://nolanblew.blob.core.windows.net/git-prune-beta/gitprune-osx-x64.tar.gz"
        echo "Extracting Git Prune..."
        if [[ ! -d $directory ]]; then
            mkdir -p $directory
        fi
        tar xzf gitprune-osx-x64.tar.gz -C $directory
        
        echo "Cleaning Up..."
        rm gitprune-osx-x64.tar.gz

        echo "Installed successfully. Please run the following command to add to your profile:"
        
        if [[ -f "${HOME}/.zshrc" ]]; then
            rcfile="${HOME}/.zshrc"
        else
            rcfile="${HOME}/.cshrc"
        fi

        echo "export PATH=\"\$PATH:${directory}\"" >> $rcfile
        echo "alias gprune='${directory}/GitPrune'" >> $rcfile
        echo
        echo "NEXT STEP: Run the following command to source the changes:"
        echo "source $rcfile"
        echo
        echo "Installed successfully. To run Git Prune, just run: GitPrune (after running the above command) or gprune (alias)"
        echo "You can edit your aliases by in $rcfile"
        ;;
    mysys|windowsnt|cygwin)
        curl -O "https://nolanblew.blob.core.windows.net/git-prune-beta/gitprune-win-x64.zip"
        echo "Extracting Git Prune..."
        mkdir gprune
        unzip -q gitprune-win-x64.tar.gz -d /git-prune
        echo "Cleaning Up..."
        rm gitprune-win-x64.tar.gz
        echo "Windows installer is not fully supported yet. Please add the following to your PATH: [current directory]/git-prune/"
        ;;
esac
echo "Finished!"