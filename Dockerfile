# syntax=docker/dockerfile:1
# Works with Docker and Podman.

# Tailwind CSS – scans the .razor components and generates wwwroot/css/app.css.
FROM node:24-alpine AS css
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY src/CollectionCatalog.Web/Styles src/CollectionCatalog.Web/Styles
COPY src/CollectionCatalog.Web/Components src/CollectionCatalog.Web/Components
RUN npm run build:css

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in a separate layer – it changes only when dependencies change.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/CollectionCatalog.Web/CollectionCatalog.Web.csproj src/CollectionCatalog.Web/
RUN dotnet restore src/CollectionCatalog.Web/CollectionCatalog.Web.csproj

COPY src/ src/
COPY .editorconfig ./
# The CSS must exist before publish to get into the static assets manifest.
COPY --from=css /src/src/CollectionCatalog.Web/wwwroot/css src/CollectionCatalog.Web/wwwroot/css
RUN dotnet publish src/CollectionCatalog.Web/CollectionCatalog.Web.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__DataPath=/data

# The data volume must belong to the unprivileged user "app" of the base image.
RUN mkdir /data && chown app:app /data
VOLUME /data

COPY --from=build /app .

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "CollectionCatalog.Web.dll"]
