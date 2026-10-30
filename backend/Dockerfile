# Estágios finais:
#   api        → a aplicação (runtime ASP.NET, sem SDK nem código-fonte)
#   migrations → EF Core migration bundle: aplica as migrations e termina

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: só é refeito quando os projetos ou ferramentas mudam.
COPY global.json dotnet-tools.json ./
COPY src/ReservaSalas.Domain/ReservaSalas.Domain.csproj src/ReservaSalas.Domain/
COPY src/ReservaSalas.Api/ReservaSalas.Api.csproj src/ReservaSalas.Api/
RUN dotnet tool restore && dotnet restore src/ReservaSalas.Api/ReservaSalas.Api.csproj

COPY src/ src/
RUN dotnet publish src/ReservaSalas.Api -c Release -o /out/api --no-restore
RUN dotnet ef migrations bundle -p src/ReservaSalas.Api --configuration Release -o /out/migrations/efbundle


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /app
COPY --from=build /out/migrations/ .
USER $APP_UID
ENTRYPOINT ["/bin/sh", "-c", "exec ./efbundle --connection \"$ConnectionStrings__ReservaSalas\""]


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
# Usuário sem privilégios que já vem nas imagens do .NET; a API escuta na 8080 (padrão da imagem).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReservaSalas.Api.dll"]
