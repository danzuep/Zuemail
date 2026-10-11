#!/usr/bin/env bash

# Setup Instructions for PowerShell / Windows:
# winget install --id Microsoft.DotNet.SDK.10 -e
# winget install --id Git.Git -e
# winget install --id github.cli -e
# bash "./.vscode/setup.sh" "ProjectName" "Project description" [organization] [full name]
# bash "./.vscode/setup.sh" "Zuemail" "Generic, extensible, fluent email."

set -e

# Variable Definitions
if [ "$#" -lt 2 ] || [ -z "$1" ] || [ -z "$2" ]; then
    echo "Usage: $0 <project-name> <project-description> [organization] [full-name]" >&2
    exit 1
fi

PROJECT_NAME="$1"
PROJECT_DESC="$2"
ORG_NAME="${3:-${organizationName:-}}"
FULL_NAME="${4:-${fullName:-$(git config user.name || true)}}"

# Locate and append dotnet to PATH if missing in non-interactive bash
if ! command -v dotnet &> /dev/null; then
    echo "'dotnet' CLI could not be found in PATH, attempting to install via apt." >&2
    sudo apt update && sudo apt install -y dotnet-sdk-10.0
fi

# 1. Environment Verification
echo "Using .NET SDK Version:"
dotnet --version

echo "Initializing project: ${PROJECT_NAME}..."

# 2. Root Directory & Git Repo Setup
CURRENT_DIR=$(basename "$PWD")

if [ "$CURRENT_DIR" != "${PROJECT_NAME}" ]; then
    if [ ! -d "${PROJECT_NAME}" ]; then
        mkdir -p "${PROJECT_NAME}"
    fi
    cd "${PROJECT_NAME}"
fi

if [ ! -d ".git" ]; then
    git init
    git branch -M main
fi

# 3. VS Code Workspace Setup
if [ ! -d ".vscode" ]; then
	mkdir -p ".vscode"
	cat << EOF > ".vscode/${PROJECT_NAME}.code-workspace"
{
    "folders": [
        {
            "path": ".."
        }
    ]
}
EOF
fi

# Git ignore file
if [ ! -f ".gitignore" ]; then
    curl -sSL https://raw.githubusercontent.com/github/gitignore/main/VisualStudio.gitignore -o .gitignore
    echo "Downloaded and created .gitignore file."
fi

# LICENSE creation
if [ ! -f "LICENSE" ]; then
    curl -sSL https://raw.githubusercontent.com/licenses/license-templates/master/templates/mit.txt \
        | sed "s/{{ year }}/$(date +%Y)/g; s/{{ organization }}/${FULL_NAME}/g" > LICENSE
    echo "Downloaded and created MIT LICENSE file."
fi

# 6. Git Commit & GitHub Repository Creation
if git config user.name > /dev/null && git config user.email > /dev/null; then
    git add .
    if git status --porcelain | grep -q .; then
        git commit -m "Initial commit"
    fi

    if ! git remote | grep -q "origin"; then
        REPO_NAME="${PROJECT_NAME}"
        if [ -n "${ORG_NAME}" ]; then
            REPO_NAME="${ORG_NAME}/${PROJECT_NAME}"
        fi
        gh repo create "${REPO_NAME}" --source=. --public --description "${PROJECT_DESC}" # --confirm
        git push -u origin main
        echo "Project ${PROJECT_NAME} pushed to GitHub."
    fi
fi

echo "Project ${PROJECT_NAME} initialized successfully."
