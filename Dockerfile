FROM ahendrix/bridge-dotnet-sdk:latest AS client-build
WORKDIR /home/bridge/projects/app
COPY UptimeClient ./
RUN bridge build -c Release                   && \
cp -R -f ./wwwroot/* ./dist ;                \
cd dist && zip -r -o -1 ../client.htmlz *

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
WORKDIR /app
EXPOSE 80
COPY --from=client-build /home/bridge/projects/app/client.htmlz /client.htmlz

FROM alpine:latest AS build
RUN apk add dotnet6-sdk
WORKDIR /src
COPY ["./UptimeServer/*.csproj", "./"]
RUN dotnet restore
COPY UptimeServer ./
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM alpine:latest AS final
RUN apk add aspnetcore6-runtime
WORKDIR /app
EXPOSE 80
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UptimeServer.dll"]
