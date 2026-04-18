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

generate_dummy_secret() {
    if [ -r /proc/sys/kernel/random/uuid ]; then
        uuid_one="$(cat /proc/sys/kernel/random/uuid | tr -d '\n-')"
        uuid_two="$(cat /proc/sys/kernel/random/uuid | tr -d '\n-')"
        printf '%s%s' "$uuid_one" "$uuid_two"
        return 0
    fi

    if [ -r /dev/urandom ]; then
        dd if=/dev/urandom bs=32 count=1 2>/dev/null | od -An -tx1 | tr -d ' \n'
        return 0
    fi

    return 1
}

export JWT_ISSUER="${JWT_ISSUER:-tripflow-migration}"
export JWT_AUDIENCE="${JWT_AUDIENCE:-tripflow-migration}"
export JWT_SECRET="${JWT_SECRET:-$(generate_dummy_secret)}"

if [ -z "$JWT_SECRET" ]; then
    echo "Unable to generate temporary JWT secret for migration."
    exit 1
fi

exec /app/efbundle --connection "$connection"
