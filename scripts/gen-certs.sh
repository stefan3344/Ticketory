#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CERT_DIR="$ROOT_DIR/nginx/certs"
mkdir -p "$CERT_DIR"

cat > "$CERT_DIR/openssl.cnf" <<'EOF'
[req]
distinguished_name = dn
x509_extensions = v3_req
prompt = no

[dn]
CN = localhost
O = Ticketory

[v3_req]
subjectAltName = @alt_names
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
basicConstraints = CA:FALSE

[alt_names]
DNS.1 = localhost
DNS.2 = ticketory.local
IP.1 = 127.0.0.1
EOF

if [[ -f "$CERT_DIR/server.crt" && -f "$CERT_DIR/server.key" ]]; then
  echo "Certificate already exists in $CERT_DIR"
  exit 0
fi

openssl req -x509 -nodes -newkey rsa:2048 -days 825 \
  -keyout "$CERT_DIR/server.key" \
  -out "$CERT_DIR/server.crt" \
  -config "$CERT_DIR/openssl.cnf"

chmod 600 "$CERT_DIR/server.key"
echo "Generated self-signed certificate in $CERT_DIR"
echo "The certificate is valid for: localhost, ticketory.local, 127.0.0.1"
