#!/bin/sh

NORMAL='\033[0m'
BOLD='\033[1m'

SCRIPT_MODE=$(echo "$1" | tr '[:upper:]' '[:lower:]') > /dev/null

if [ $# -ne 1 ];then
    echo "Usage: $0 [dev|prod]"
    exit 1
fi

if [ "$1" != "dev" ] && [ "$1" != "prod" ]; then
    echo "Usage: $0 [dev|prod]"
    exit 2
fi

git_hash=$(git rev-parse HEAD)
app_version=""
latest_tag=""

if [ "$SCRIPT_MODE" = "dev" ]; then
    app_version=$(date +%Y.%m.%d.%H%M)
    echo "Building ${BOLD}DEVELOPMENT${NORMAL} Docker Image..."
    latest_tag="latest-dev"
else
    app_version=$(head -n 1 version)
    echo "Building ${BOLD}PRODUCTION${NORMAL} Docker Image..."
    latest_tag="latest"
fi

docker_image=platybot
docker build --build-arg APP_VERSION="$app_version" --build-arg APP_GIT_HASH="$git_hash" -t "$docker_image":"$app_version" -t "$docker_image":"$latest_tag" .
