# ---- Stage 1: Build Angular ----
FROM node:20-alpine AS angular-build
WORKDIR /angular
COPY MaisonChain-app/package*.json ./
RUN npm ci
COPY MaisonChain-app/ ./
RUN npm run build -- --configuration production

# ---- Stage 2: Build .NET ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /src
COPY ProductStore/ProductStore.Api/*.csproj ./ProductStore/ProductStore.Api/
RUN dotnet restore ./ProductStore/ProductStore.Api/productStore.Api.csproj
COPY . .
COPY --from=angular-build /angular/dist/maison-chain/browser/ ./ProductStore/ProductStore.Api/wwwroot/
RUN dotnet publish ./ProductStore/ProductStore.Api/productStore.Api.csproj -c Release -o /app/publish

# ---- Stage 3: Run ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=dotnet-build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "productStore.Api.dll"]