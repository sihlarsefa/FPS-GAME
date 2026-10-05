# HAREKÂT — Unity Linux Dedicated Server imajı
# Build yolu ARG ile verilir; port / region / server key ENV ile yapılandırılır.

ARG UNITY_SERVER_BUILD_PATH=./ServerBuild
ARG BASE_IMAGE=ubuntu:22.04

FROM ${BASE_IMAGE}

LABEL org.opencontainers.image.title="HAREKÂT Dedicated Server" \
      org.opencontainers.image.description="Unity Linux dedicated server for HAREKÂT FPP team BR" \
      org.opencontainers.image.vendor="HAREKÂT"

ARG UNITY_SERVER_BUILD_PATH
ENV DEBIAN_FRONTEND=noninteractive \
    GAME_PORT=7777 \
    QUERY_PORT=7778 \
    REGION=eu-istanbul \
    SERVER_KEY="" \
    BACKEND_URL="" \
    MAX_PLAYERS=60 \
    MIN_PLAYERS=40 \
    MATCH_DURATION_SEC=1800 \
    UNITY_SERVER_EXECUTABLE=Harekat.x86_64

RUN apt-get update && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        libglu1-mesa \
        libxcursor1 \
        libxrandr2 \
        libxi6 \
        libxss1 \
        libasound2 \
        libc6 \
        libstdc++6 \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd -r harekat && useradd -r -g harekat -d /opt/harekat -s /sbin/nologin harekat

WORKDIR /opt/harekat

# Unity Linux dedicated server build artefaktı
COPY ${UNITY_SERVER_BUILD_PATH}/ ./

RUN chmod +x "/opt/harekat/${UNITY_SERVER_EXECUTABLE}" 2>/dev/null || true \
    && chown -R harekat:harekat /opt/harekat

COPY --chown=harekat:harekat entrypoint.sh /opt/harekat/entrypoint.sh
RUN chmod +x /opt/harekat/entrypoint.sh

USER harekat

EXPOSE ${GAME_PORT}/udp ${GAME_PORT}/tcp ${QUERY_PORT}/udp

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -sf "http://127.0.0.1:${QUERY_PORT}/health" || exit 1

ENTRYPOINT ["/opt/harekat/entrypoint.sh"]
