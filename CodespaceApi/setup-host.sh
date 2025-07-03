#!/bin/bash

# Codespace API Host Setup Script
# This script prepares the host system for running the Codespace API

set -e

echo "Setting up host system for Codespace API..."

# Create the codespace-service user on the host
if ! id "codespace-service" &>/dev/null; then
    echo "Creating codespace-service user..."
    sudo useradd -m -s /bin/bash codespace-service
    echo "codespace-service user created successfully."
else
    echo "codespace-service user already exists."
fi

# Create the /data/codespaces directory
echo "Creating /data/codespaces directory..."
sudo mkdir -p /data/codespaces
sudo chown codespace-service:codespace-service /data/codespaces
sudo chmod 755 /data/codespaces

# Set up sudoers for codespace-service user
echo "Configuring sudoers for codespace-service..."
sudo bash -c 'cat > /etc/sudoers.d/codespace-service << EOF
# Allow codespace-service to manage users and directories
codespace-service ALL=(ALL) NOPASSWD: /usr/sbin/useradd
codespace-service ALL=(ALL) NOPASSWD: /usr/sbin/userdel
codespace-service ALL=(ALL) NOPASSWD: /usr/bin/passwd
codespace-service ALL=(ALL) NOPASSWD: /bin/chown
codespace-service ALL=(ALL) NOPASSWD: /bin/chmod
codespace-service ALL=(ALL) NOPASSWD: /usr/bin/ssh-keygen
codespace-service ALL=(ALL) NOPASSWD: /usr/sbin/groupadd
codespace-service ALL=(ALL) NOPASSWD: /usr/sbin/groupdel
codespace-service ALL=(ALL) NOPASSWD: /usr/sbin/usermod
EOF'

# Ensure SSH is installed and configured
if ! command -v ssh &> /dev/null; then
    echo "Installing SSH..."
    sudo apt-get update
    sudo apt-get install -y openssh-server
fi

# Start SSH service
echo "Starting SSH service..."
sudo systemctl enable ssh
sudo systemctl start ssh

# Create a more secure alternative docker-compose file
echo "Creating secure docker-compose configuration..."
cat > docker-compose.secure.yml << EOF
version: '3.8'

services:
  codespace-api:
    build: .
    container_name: codespace-api
    ports:
      - "8080:8080"
    volumes:
      - /data/codespaces:/data/codespaces
      - /etc/passwd:/etc/passwd:rw
      - /etc/shadow:/etc/shadow:rw
      - /etc/group:/etc/group:rw
      - /home/codespace-service:/home/codespace-service
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
    restart: unless-stopped
    user: "$(id -u codespace-service):$(id -g codespace-service)"
    cap_add:
      - SYS_ADMIN
      - DAC_OVERRIDE
      - CHOWN
      - SETUID
      - SETGID
    security_opt:
      - apparmor:unconfined
EOF

echo "Host setup completed successfully!"
echo ""
echo "Next steps:"
echo "1. Build the Docker image: docker-compose build"
echo "2. Start the service: docker-compose up -d"
echo "3. Check logs: docker-compose logs -f"
echo "4. Test the API: curl http://localhost:8080/api/health"
echo ""
echo "For more secure deployment, use: docker-compose -f docker-compose.secure.yml up -d"