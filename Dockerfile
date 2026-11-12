# Nova Merchandising — ASP.NET Core Razor Pages + htmx demo
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Merch.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# The Debian-based runtime carries ICU: prices are formatted per culture.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV PORT=3000
COPY --from=build /app ./
USER $APP_UID
EXPOSE 3000
# NOVA_DELIVERY_URL/USER/PASSWORD, DEMO_BRANCH, SHOPIFY_* and SITE_URL come from the environment (the platform's secrets)
ENTRYPOINT ["dotnet", "Merch.dll"]
