# Build stage: restore and publish the API with the .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer) using only project files
COPY global.json PizzaOrderDesigner.slnx ./
COPY src/PizzaOrderDesigner.Api/PizzaOrderDesigner.Api.csproj src/PizzaOrderDesigner.Api/
COPY src/PizzaOrderDesigner.Data/PizzaOrderDesigner.Data.csproj src/PizzaOrderDesigner.Data/
RUN dotnet restore src/PizzaOrderDesigner.Api/PizzaOrderDesigner.Api.csproj

COPY src/ src/
RUN dotnet publish src/PizzaOrderDesigner.Api/PizzaOrderDesigner.Api.csproj \
    -c Release -o /app/publish --no-restore

# Runtime stage: slim ASP.NET Core image, runs as the non-root 'app' user, listens on 8080
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaOrderDesigner.Api.dll"]
