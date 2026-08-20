FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT=FamLedger.Api
WORKDIR /src
COPY FamLedger.slnx ./
COPY src/ ./src/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet"]
CMD ["FamLedger.Api.dll"]
