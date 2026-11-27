FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY Companion.Configuration/ Companion.Configuration/
COPY Companion.Core/ Companion.Core/
COPY Companion.Data/ Companion.Data/
COPY Companion.Events/ Companion.Events/
COPY Companion.Messaging/ Companion.Messaging/
COPY Services/Companion.Api/ Services/Companion.Api/
RUN dotnet publish Services/Companion.Api/Companion.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Companion.Api.dll"]
