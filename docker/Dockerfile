FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/KuchniaUCygana.Web/KuchniaUCygana.Web.csproj", "src/KuchniaUCygana.Web/"]
COPY ["src/KuchniaUCygana.Application/KuchniaUCygana.Application.csproj", "src/KuchniaUCygana.Application/"]
COPY ["src/KuchniaUCygana.Domain/KuchniaUCygana.Domain.csproj", "src/KuchniaUCygana.Domain/"]
COPY ["src/KuchniaUCygana.Infrastructure/KuchniaUCygana.Infrastructure.csproj", "src/KuchniaUCygana.Infrastructure/"]
RUN dotnet restore "src/KuchniaUCygana.Web/KuchniaUCygana.Web.csproj"

COPY . .
RUN dotnet publish "src/KuchniaUCygana.Web/KuchniaUCygana.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN mkdir -p /app/data /app/wwwroot/uploads
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "KuchniaUCygana.Web.dll"]
