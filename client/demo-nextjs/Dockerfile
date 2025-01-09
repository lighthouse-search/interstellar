FROM node:latest AS build-stage
WORKDIR /demo-server

COPY . /demo-server

RUN apt update -y
RUN apt upgrade -y
RUN apt install -y curl unzip ca-certificates

RUN useradd -m -s /bin/bash linuxbrew && \
    mkdir -p /home/linuxbrew/.linuxbrew && \
    chown -R linuxbrew:linuxbrew /home/linuxbrew/.linuxbrew && \
    chown -R linuxbrew:linuxbrew /demo-server/* && \
    chown -R linuxbrew:linuxbrew /demo-server

USER root
RUN npm install next -g
USER linuxbrew

RUN npm install
RUN npm run build

CMD PORT=4000 && npm run start