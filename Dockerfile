# docker build -t blk-hacking-ind-prachi-gaikwad .

# ── Base OS selection ─────────────────────────────────────
# Using mcr.microsoft.com/dotnet/aspnet:8.0 based on Debian 12 (Bookworm)
# Rationale:
#   • Official Microsoft .NET 8 runtime image – smallest secure surface
#   • Debian 12 = Long-Term-Support Linux distro, well-maintained CVE track
#   • ~210 MB compressed vs Alpine-musl (compatibility issues with .NET GC)
# ─────────────────────────────────────────────────────────

# ── Stage 1: Build ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY blk-hacking-ind.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a non-root user for security
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 5477

ENV ASPNETCORE_URLS=http://+:5477
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "blk-hacking-ind.dll"]
