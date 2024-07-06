FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["tools-server/tools-server.csproj", "tools-server/"]
RUN dotnet restore "tools-server/tools-server.csproj"
COPY . .
WORKDIR "/src/tools-server"
RUN dotnet publish "tools-server.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "tools-server.dll"]