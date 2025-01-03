FROM mcr.microsoft.com/dotnet/sdk:latest AS build-stage
WORKDIR /interstellar-server

COPY . /interstellar-server/

RUN apt update -y
RUN apt upgrade -y
RUN apt install -y curl unzip ca-certificates openssl tree

RUN useradd -m -s /bin/bash linuxbrew && \
    mkdir -p /home/linuxbrew/.linuxbrew && \
    chown -R linuxbrew:linuxbrew /home/linuxbrew/.linuxbrew && \
    chown -R linuxbrew:linuxbrew /interstellar-server/* && \
    chown -R linuxbrew:linuxbrew /interstellar-server

USER linuxbrew

RUN dotnet restore
RUN dotnet publish -c Release -o "/home/linuxbrew/interstellar" /p:PublishSingleFile=true /p:SelfContained=true

FROM mcr.microsoft.com/dotnet/aspnet:latest AS production-stage
COPY --from=build-stage /home/linuxbrew/interstellar/Interstellar-video /home/kube/interstellar-release

RUN ["apt", "update", "-y"]
RUN ["apt", "upgrade", "-y"]

RUN adduser kube --disabled-login
RUN usermod -s /bin/rbash kube

RUN chown -R kube:kube /home/kube/

RUN chown -R kube:kube /home/kube/interstellar-release
RUN chmod +x /home/kube/interstellar-release

# Run the application as kube user.
USER kube

WORKDIR /
CMD /home/kube/interstellar-release