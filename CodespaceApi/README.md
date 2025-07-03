# Codespace API

A .NET Core minimal API for creating and managing user accounts on Unix/Ubuntu systems for codespace environments.

## Features

- Create users with restricted access to `/data/codespaces/<username>/`
- Generate SSH keys for secure access
- Temporary password generation for initial login
- User management (create, get info, delete)
- Secure containerized deployment
- Non-root container execution with proper permissions

## API Endpoints

- `POST /api/users` - Create a new user
- `GET /api/users/{username}` - Get user information
- `DELETE /api/users/{username}` - Delete a user
- `GET /api/health` - Health check

## Security Model

The application runs with a dedicated service user (`codespace-service`) instead of root, with specific sudo permissions for user management operations only.

## Quick Start

### 1. Host System Setup

```bash
# Make setup script executable
chmod +x setup-host.sh

# Run host setup (requires sudo)
./setup-host.sh
```

### 2. Build and Deploy

```bash
# Build the Docker image
docker-compose build

# Start the service
docker-compose up -d

# Check logs
docker-compose logs -f
```

### 3. Test the API

```bash
# Health check
curl http://localhost:8080/api/health

# Create a user
curl -X POST http://localhost:8080/api/users \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser"}'

# Get user info
curl http://localhost:8080/api/users/testuser
```

## Manual Testing Guide

### Test User Creation

```bash
# Create a test user
curl -X POST http://localhost:8080/api/users \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser"}' | jq

# Expected response:
{
  "username": "testuser",
  "tempPassword": "generated_password",
  "homeDirectory": "/data/codespaces/testuser",
  "sshPublicKey": "ssh-rsa AAAAB3NzaC1yc2E... testuser@codespace"
}
```

### Test SSH Access

```bash
# Test SSH login with the generated password
ssh testuser@localhost

# Test SSH key-based authentication
ssh -i /data/codespaces/testuser/.ssh/id_rsa testuser@localhost
```

### Test User Restrictions

```bash
# SSH into the user account
ssh testuser@localhost

# Try to access other directories (should fail)
cd /home
ls /root
cat /etc/passwd

# Verify user can only access their directory
cd /data/codespaces/testuser
ls -la
```

### Test User Deletion

```bash
# Delete the test user
curl -X DELETE http://localhost:8080/api/users/testuser

# Verify user is deleted
curl http://localhost:8080/api/users/testuser
```

## Deployment Options

### Option 1: Standard Deployment (Privileged)

Uses `privileged: true` in docker-compose.yml for full system access.

```bash
docker-compose up -d
```

### Option 2: Secure Deployment (Recommended)

Uses specific capabilities instead of full privileges:

```bash
# Use the secure configuration
docker-compose -f docker-compose.secure.yml up -d
```

### Option 3: Host User Integration

Run the container with the host's codespace-service user:

```bash
# Build with host user integration
docker build -t codespace-api .

# Run with host user
docker run -d \
  --name codespace-api \
  -p 8080:8080 \
  -v /data/codespaces:/data/codespaces \
  -v /etc/passwd:/etc/passwd:rw \
  -v /etc/shadow:/etc/shadow:rw \
  -v /etc/group:/etc/group:rw \
  --user $(id -u codespace-service):$(id -g codespace-service) \
  --cap-add SYS_ADMIN \
  --cap-add DAC_OVERRIDE \
  --cap-add CHOWN \
  --cap-add SETUID \
  --cap-add SETGID \
  codespace-api
```

## Security Considerations

1. **Service User**: The API runs as `codespace-service` user, not root
2. **Limited Sudo**: Only specific commands are allowed via sudo
3. **Directory Isolation**: Users are restricted to their codespace directory
4. **SSH Key Management**: Automatic SSH key generation and configuration
5. **Container Security**: Uses minimal required capabilities

## Troubleshooting

### Common Issues

1. **Permission Denied**: Ensure the host setup script ran successfully
2. **User Creation Failed**: Check if the codespace-service user has proper sudo permissions
3. **SSH Access Issues**: Verify SSH service is running and user directories have correct permissions
4. **API Not Responding**: Check container logs with `docker-compose logs`

### Debugging

```bash
# Check container logs
docker-compose logs -f codespace-api

# Execute into container
docker exec -it codespace-api /bin/bash

# Check host system users
cat /etc/passwd | grep codespace

# Check directory permissions
ls -la /data/codespaces/
```

## Development

### Local Development

```bash
# Run locally (requires .NET 8.0 SDK)
dotnet restore
dotnet run

# The API will be available at http://localhost:5000
```

### Testing

```bash
# Test the API endpoints
dotnet test

# Manual testing with curl
curl -X POST http://localhost:5000/api/users \
  -H "Content-Type: application/json" \
  -d '{"username": "devuser"}'
```

## Architecture

The application follows a clean architecture pattern:

- **Program.cs**: Main application entry point and endpoint definitions
- **UserService**: Core business logic for user management
- **Models**: Request/response models
- **Docker**: Containerization and deployment configuration

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is licensed under the MIT License.