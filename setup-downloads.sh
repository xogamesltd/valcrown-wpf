#!/bin/bash
# Run this ONCE on your VPS to set up self-hosted downloads
# ssh root@187.127.136.104 "bash -s" < setup-downloads.sh

set -e

echo "Setting up ValCrown download server..."

# Create downloads directory
mkdir -p /var/valcrown/downloads

# Create latest.json placeholder
cat > /var/valcrown/downloads/latest.json << 'EOF'
{"version":"1.0.0","url":"https://valcrown.com/download/ValCrown-Setup-1.0.0.exe","date":"2026-03-29"}
EOF

# Create Nginx config for downloads
cat > /etc/nginx/conf.d/valcrown-downloads.conf << 'EOF'
server {
    listen 80;
    server_name valcrown.com www.valcrown.com;

    # Serve downloads from /download/ path
    location /download/ {
        alias /var/valcrown/downloads/;
        add_header Content-Disposition "attachment";
        add_header Access-Control-Allow-Origin "*";
    }

    # Serve latest.json for auto-update
    location /api/latest {
        alias /var/valcrown/downloads/latest.json;
        default_type application/json;
        add_header Access-Control-Allow-Origin "*";
        add_header Cache-Control "no-cache";
    }
}
EOF

# Set permissions
chmod 755 /var/valcrown/downloads
chmod 644 /var/valcrown/downloads/latest.json

echo "Done. Downloads available at https://valcrown.com/download/"
echo "Latest version API at https://valcrown.com/api/latest"
