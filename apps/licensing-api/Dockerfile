FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY packages/licensing/ packages/licensing/
COPY apps/licensing-api/ apps/licensing-api/
RUN dotnet publish apps/licensing-api/Atlas.Licensing.Api.csproj -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Atlas.Licensing.Api.dll"]
