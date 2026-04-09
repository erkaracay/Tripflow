#!/bin/sh
set -eu

raw_connection="${CONNECTION_STRING:-${ConnectionStrings__TripflowDb:-}}"

if [ -z "$raw_connection" ]; then
    echo "Missing database connection string. Set CONNECTION_STRING or ConnectionStrings__TripflowDb."
    exit 1
fi

# Normalize dashboard copy/paste quirks before handing the value to Npgsql.
connection="$(printf '%s' "$raw_connection" | tr -d '\r\n')"

case "$connection" in
    \"*\")
        connection="${connection#\"}"
        connection="${connection%\"}"
        ;;
esac

case "$connection" in
    \'*\')
        connection="${connection#\'}"
        connection="${connection%\'}"
        ;;
esac

export JWT_ISSUER="${JWT_ISSUER:-tripflow-bundle}"
export JWT_AUDIENCE="${JWT_AUDIENCE:-tripflow-bundle}"
export JWT_SECRET="${JWT_SECRET:-tripflow-bundle-secret-key-tripflow-bundle-secret-key}"

exec /app/efbundle --connection "$connection"
