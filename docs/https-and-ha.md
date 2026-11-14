# HTTPS, Keepalived and the OS-level cluster

## 1. HTTPS with a self-signed certificate

### What TLS does

TLS provides two things:

1. **Confidentiality** — the traffic between the browser and the server is encrypted.
2. **Server authentication** — the server proves its identity with a certificate.

The browser validates the certificate by building a chain to a **trusted root CA**. A self-signed certificate is signed by its own private key, so there is no trusted root in the chain. The browser therefore shows a warning such as `NET::ERR_CERT_AUTHORITY_INVALID` or "Not secure". This is expected and does not mean the traffic is unencrypted.

### Certificate generation

`scripts/gen-certs.sh` creates an RSA key and a certificate with a **Subject Alternative Name (SAN)**. Modern browsers ignore the Common Name (CN) for hostname validation and require SAN.

```ini
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
```

```bash
openssl req -x509 -nodes -newkey rsa:2048 -days 825 \
  -keyout nginx/certs/server.key \
  -out nginx/certs/server.crt \
  -config nginx/certs/openssl.cnf
```

### Nginx configuration

TLS is terminated at Nginx, not at the API replicas:

```nginx
server {
    listen 443 ssl;
    http2 on;

    ssl_certificate     /etc/nginx/certs/server.crt;
    ssl_certificate_key /etc/nginx/certs/server.key;
    ssl_protocols       TLSv1.2 TLSv1.3;

    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

    location / {
        proxy_pass http://ticketory_app;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    return 301 https://$host$request_uri;
}
```

Benefits of terminating TLS at the edge:

- one certificate instead of one per replica,
- the internal network stays HTTP (simple and fast),
- Nginx can add HSTS and security headers centrally,
- `X-Forwarded-Proto` tells ASP.NET Core that the original request was HTTPS.

### Accepting the certificate for the demo

Option A — accept the browser warning:

1. Open `https://localhost`.
2. Click **Advanced**.
3. Click **Proceed to localhost (unsafe)**.

Option B — trust the certificate on macOS:

```bash
sudo security add-trusted-cert -d -r trustRoot \
  -k /Library/Keychains/System.keychain nginx/certs/server.crt
```

Firefox keeps its own certificate store: **Settings → Privacy & Security → Certificates → View Certificates → Import**.

### Production note

For a real deployment, use a certificate from a public CA (e.g. Let's Encrypt) or an internal corporate CA. Never commit the private key; in this project it is git-ignored and generated locally.

## 2. Keepalived

### What it is

Keepalived is a lightweight Linux daemon that implements **VRRP** (Virtual Router Redundancy Protocol). Two or more hosts share a **floating virtual IP (VIP)**. One host is MASTER, the others are BACKUP. If the MASTER fails, the VIP moves to a BACKUP within seconds.

Typical use: highly available load balancer or web server.

```conf
vrrp_instance VI_1 {
    state MASTER
    interface eth0
    virtual_router_id 51
    priority 100
    advert_int 1
    virtual_ipaddress {
        10.10.10.100/24
    }
}
```

### Why it is not implemented here

- VRRP uses multicast; multicast/VRRP inside Docker Desktop on macOS is unreliable.
- It is a host-level mechanism: it expects real network interfaces and a real LAN.
- A correct demo needs at least two Linux VMs (e.g. Multipass, UTM or VirtualBox) with a host-only network.

The required availability behaviour is already provided by two API replicas behind Nginx plus health checks and `restart: unless-stopped`. Keepalived would add a redundant third layer in this single-host setup.

## 3. Corosync + Pacemaker (OS-level cluster)

### What it is

A **high-availability cluster at the operating-system level** consists of multiple Linux nodes that coordinate to keep services running.

- **Corosync** provides cluster membership, quorum and reliable messaging. It uses UDP (typically port 5405) and can encrypt traffic.
- **Pacemaker** is the cluster resource manager (CRM). It decides where resources run, performs failover, and enforces constraints.
- **Resource agents (OCF)** describe how to start/stop/monitor a resource: `IPaddr2` (floating VIP), `pgsql`/`pgsqlms` (PostgreSQL), `systemd:nginx`, `docker`, etc.
- **Fencing / STONITH** guarantees that a failed node is really dead before failover, preventing split-brain.
- **Quorum** prevents two partitions from both believing they are the cluster. A two-node cluster needs `two_node: 1` and `wait_for_all`, or a third arbiter/QDevice.

Classic example: PostgreSQL primary/standby with a floating database VIP.

```bash
pcs cluster setup --name ticketory node1 node2
pcs cluster start --all

pcs property set stonith-enabled=false        # lab only
pcs resource create dbvip ocf:heartbeat:IPaddr2 \
    ip=10.10.10.100 cidr_netmask=24
pcs resource create pgsql ocf:heartbeat:pgsql \
    pgctl=/usr/lib/postgresql/16/bin/pg_ctl \
    pgdata=/var/lib/postgresql/16/main

pcs constraint colocation add dbvip with pgsql INFINITY
pcs constraint order start pgsql then dbvip
crm_mon -1
```

Useful demo commands:

```bash
pcs node standby node1      # resources move to node2
pcs node unstandby node1
pcs resource move dbvip node2
crm_mon -1
```

### Why it is not implemented here

- Corosync/Pacemaker are Linux host services; they need systemd, real network interfaces, quorum and fencing.
- Docker containers are not independent cluster nodes, and Docker Desktop on macOS is itself a Linux VM.
- A meaningful demonstration needs two or three Linux VMs.

### How it could be added

1. Create two Ubuntu VMs with Multipass/UTM.
2. Install `corosync`, `pacemaker`, `pcs`, `resource-agents` and PostgreSQL on both.
3. Configure Corosync (unicast, two-node mode) and Pacemaker.
4. Use `pgsqlms` or streaming replication plus an `IPaddr2` VIP for the database.
5. Point the application's `ConnectionStrings:Default` to the DB VIP and provide the password through `Database:Password`.
6. Optionally add Keepalived on the ingress tier (two Nginx VMs) with a separate VIP.

This is a valid extension, but it is outside the Docker Compose scope of this project and would not improve the application behaviour on a single macOS host.

## 4. Decision summary

| Technology | Implemented | Reason |
|---|---|---|
| HTTPS with self-signed certificate | Yes | Required by the assignment; Nginx terminates TLS. |
| Nginx load balancer over two replicas | Yes | Required; demonstrated with `X-Served-By`. |
| Keepalived | No | Host-level VRRP; unreliable in Docker Desktop; redundant on a single host. |
| Corosync + Pacemaker | No | Requires multiple Linux VMs, quorum and fencing. |

The decision is deliberate: the solution uses the right tool for the Docker-based scope and documents how to extend it to a real OS-level cluster.
