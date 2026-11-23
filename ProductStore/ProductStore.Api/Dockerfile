# Use the official .NET 9 SDK to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy everything else and build in Release mode
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Use the lighter ASP.NET runtime for the final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Railway expects the app to listen on port 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

# Replace "ProductStore.Api" with your actual .csproj filename (without .csproj)
ENTRYPOINT ["dotnet", "productStore.Api.dll"]