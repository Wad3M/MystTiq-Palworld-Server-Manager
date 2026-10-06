# MystTiq v1.0.2.0: file reviewed for this release (2026-10-05).
# v1.0.3.0 (roadmap P-1): the MystTiq headless service in a Linux container. Everything it keeps (configuration, the
# server files, backups, the API token and TLS certificate) lives in the mounted /data folder; the image holds only the
# program. Build with scripts/Test-v1.0.3.0-Docker.ps1 (which publishes the linux-x64 service into app/ first), or by hand:
#   docker build -t mysttiq-headless:<version> --build-arg VERSION=<version> <folder with app/, entrypoint.sh, Dockerfile>
# Run: docker run -d -p 8213:8213 -p 8211:8211/udp -v mysttiq-data:/data mysttiq-headless:<version>
# Keep /data on a Docker volume: with a folder bind-mounted from Windows the Palworld server exited before it was ready.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble
ARG VERSION=0.0.0.0
LABEL org.opencontainers.image.title="MystTiq Palworld Server Manager (headless)" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/Wad3M/MystTiq-Palworld-Server-Manager"
# SteamCMD (installed into /data on first use by MystTiq) is a 32-bit program; python3 runs MystTiq's save tools.
RUN dpkg --add-architecture i386 \
 && apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl lib32gcc-s1 lib32stdc++6 python3 procps iproute2 \
 && rm -rf /var/lib/apt/lists/*
COPY app/ /opt/mysttiq/bin/
COPY entrypoint.sh /opt/mysttiq/entrypoint.sh
RUN chmod +x /opt/mysttiq/bin/mysttiq-server /opt/mysttiq/entrypoint.sh \
 && useradd --system --create-home --home-dir /home/mysttiq --shell /usr/sbin/nologin mysttiq \
 && mkdir -p /data && chown mysttiq:mysttiq /data
USER mysttiq
ENV MYSTTIQ_DATA=/data
VOLUME ["/data"]
# 8213: MystTiq's API (HTTPS, token or account sign-in); 8211/udp: the game; 8212: Palworld's REST API (stays inside).
EXPOSE 8213/tcp 8211/udp
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s CMD curl -fsk https://127.0.0.1:8213/healthz >/dev/null || exit 1
ENTRYPOINT ["/opt/mysttiq/entrypoint.sh"]
