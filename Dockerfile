# Estágio 1: Build da aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia os arquivos de projeto para restaurar dependências com cache eficiente
COPY ["GetCNPJ.csproj", "./"]
COPY ["GetCNPJ.Api/GetCNPJ.Api.csproj", "GetCNPJ.Api/"]

RUN dotnet restore "GetCNPJ.Api/GetCNPJ.Api.csproj"

# Copia o restante do código-fonte e compila
COPY . .
WORKDIR "/src/GetCNPJ.Api"
RUN dotnet publish "GetCNPJ.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Imagem final leve para execução
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GetCNPJ.Api.dll"]
