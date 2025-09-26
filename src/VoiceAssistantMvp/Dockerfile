# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the entire project (compose context = project folder)
COPY . .

# Create a minimal NuGet.config inside the container
RUN echo '<?xml version="1.0" encoding="utf-8"?>' > ./NuGet.config \
 && echo '<configuration>' >> ./NuGet.config \
 && echo '  <packageSources>' >> ./NuGet.config \
 && echo '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />' >> ./NuGet.config \
 && echo '  </packageSources>' >> ./NuGet.config \
 && echo '  <fallbackPackageFolders><clear /></fallbackPackageFolders>' >> ./NuGet.config \
 && echo '  <config><add key="globalPackagesFolder" value="/root/.nuget/packages" /></config>' >> ./NuGet.config \
 && echo '</configuration>' >> ./NuGet.config

# Restore with Windows targeting + no VS fallback folders
RUN dotnet restore \
    -p:EnableWindowsTargeting=true \
    -p:RestoreAdditionalFallbackFolders= \
    --configfile ./NuGet.config

# 👉 stub the model file so publish doesn't fail
RUN mkdir -p /.voice-assistant/models && touch /.voice-assistant/models/ggml-small.en.bin

# Publish (don’t restore again)
RUN dotnet publish -c Release -o /app/publish --no-restore -p:EnableWindowsTargeting=true

# ---------- Runtime stage (we won't run this image yet) ----------
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
RUN useradd -m appuser
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VoiceAssistantMvp.dll"]
