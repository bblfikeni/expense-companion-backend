FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY Companion.Configuration/ Companion.Configuration/
COPY Companion.Core/ Companion.Core/
COPY Companion.Data/ Companion.Data/
COPY Companion.Events/ Companion.Events/
COPY Companion.Messaging/ Companion.Messaging/
COPY Services/Companion.Extraction/ Services/Companion.Extraction/
RUN dotnet publish Services/Companion.Extraction/Companion.Extraction.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-eng tesseract-ocr-afr \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Companion.Extraction.dll"]
