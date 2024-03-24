# See https://aka.ms/containercompat for customization and optimization
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# SDK Image for building the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AirPlaneTicketManagement.csproj", "./"]
RUN dotnet restore "AirPlaneTicketManagement.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "AirPlaneTicketManagement.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AirPlaneTicketManagement.csproj" -c Release -o /app/publish

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AirPlaneTicketManagement.dll"]