# Login to container registry
DOCKER_LOGIN=$1
DOCKER_TOKEN=$2

docker login \
--username $DOCKER_LOGIN --password $DOCKER_TOKEN \
ghcr.io

# Create configuration file if not exists
CONFIG_FILE=appsettings.Production.json

if [ ! -f $CONFIG_FILE ]; then
    echo "{}" > $CONFIG_FILE
fi

IMAGE_NAME=ghcr.io/nijeti/cinema-keeper
CONTAINER_NAME=CinemaKeeper

# Stop and remove existing container
if [ "$(docker ps -aq -f name=$CONTAINER_NAME)" ]; then
    if [ ! "$(docker ps -aq -f status=exited -f name=$CONTAINER_NAME)" ]; then
        docker stop $CONTAINER_NAME
    fi

    docker rm $CONTAINER_NAME
fi

# Create new container and run it
docker pull $IMAGE_NAME
docker run -d \
    --restart unless-stopped \
    --name $CONTAINER_NAME \
    -e DOTNET_ENVIRONMENT=Production \
    -v "$PWD/appsettings.Production.json:/app/appsettings.Production.json" \
    -v "$PWD/Logs/:/app/Logs" \
    $IMAGE_NAME
