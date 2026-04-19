#!/usr/bin/env sh
set -eu

backend_url="${BACKEND_URL:-${backend_url:-}}"

if [ -z "${BACKEND_URL}" ]; then
    echo >&2 "BACKEND_URL is required (expected format: http(s)://host:port)."
    exit 1
fi

case "${backend_url}" in
    http://*:[0-9]*|https://*:[0-9]*)
        ;;
    *)
        echo >&2 "Invalid BACKEND_URL '${backend_url}'. Expected format: http(s)://host:port."
        exit 1
        ;;
esac

export BACKEND_URL="${backend_url}"
envsubst '${BACKEND_URL}' \
    < /etc/nginx/default.conf.template \
    > /etc/nginx/conf.d/default.conf
