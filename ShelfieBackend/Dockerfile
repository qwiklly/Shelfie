# Build from the repository root:  docker build -f ShelfieBackend/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY Shelfie.Shared/Shelfie.Shared.csproj Shelfie.Shared/
COPY ShelfieBackend/ShelfieBackend.csproj ShelfieBackend/
RUN dotnet restore ShelfieBackend/ShelfieBackend.csproj

COPY Shelfie.Shared/ Shelfie.Shared/
COPY ShelfieBackend/ ShelfieBackend/
RUN dotnet publish ShelfieBackend/ShelfieBackend.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "ShelfieBackend.dll"]
