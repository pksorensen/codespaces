# Deployment Checklist

## Pre-Deployment Setup

### 1. Host System Requirements
- [ ] Ubuntu/Debian-based Linux system
- [ ] Docker and Docker Compose installed
- [ ] SSH server installed and running
- [ ] Sudo privileges for initial setup

### 2. Host System Setup
```bash
# Run the setup script
./setup-host.sh

# Verify setup
sudo -u codespace-service whoami
ls -la /data/codespaces/
```

### 3. Security Verification
- [ ] codespace-service user created
- [ ] /data/codespaces directory exists with proper permissions
- [ ] Sudoers configuration in place
- [ ] SSH service running

## Deployment Options

### Option 1: Development/Testing (Standard)
```bash
# Build and run
docker-compose build
docker-compose up -d

# Test
./test-api.sh
```

### Option 2: Production (Recommended)
```bash
# Build and run with production config
docker-compose -f docker-compose.production.yml build
docker-compose -f docker-compose.production.yml up -d

# Test
./test-api.sh
```

### Option 3: Secure Deployment
```bash
# Use the secure configuration
docker-compose -f docker-compose.secure.yml build
docker-compose -f docker-compose.secure.yml up -d

# Test
./test-api.sh
```

## Post-Deployment Verification

### 1. API Health Check
```bash
curl http://localhost:8080/api/health
```

### 2. Container Status
```bash
docker-compose ps
docker-compose logs -f codespace-api
```

### 3. Full API Testing
```bash
./test-api.sh
```

### 4. Manual User Testing
```bash
# Create test user
curl -X POST http://localhost:8080/api/users \
  -H "Content-Type: application/json" \
  -d '{"username": "manualtest"}'

# Test SSH access
ssh manualtest@localhost

# Clean up
curl -X DELETE http://localhost:8080/api/users/manualtest
```

## Security Considerations

### Container Security
- [ ] Container runs as non-root user
- [ ] Minimal required capabilities only
- [ ] Resource limits configured
- [ ] Health checks enabled
- [ ] Logging configured

### Host Security
- [ ] Service user has minimal required permissions
- [ ] SSH keys are properly generated and secured
- [ ] User directories are properly isolated
- [ ] Regular security updates applied

### Network Security
- [ ] API only exposed on necessary ports
- [ ] Consider using reverse proxy (nginx/Apache)
- [ ] SSL/TLS termination if needed
- [ ] Firewall rules configured

## Monitoring and Maintenance

### Log Monitoring
```bash
# Container logs
docker-compose logs -f

# System logs
sudo journalctl -u docker
sudo journalctl -f
```

### Performance Monitoring
```bash
# Container stats
docker stats codespace-api

# System resources
htop
df -h
```

### Regular Maintenance
- [ ] Regular container updates
- [ ] Log rotation configured
- [ ] Backup strategy for user data
- [ ] Monitor disk usage in /data/codespaces

## Troubleshooting

### Common Issues

1. **Permission Denied**
   - Check codespace-service user permissions
   - Verify sudoers configuration
   - Check file system permissions

2. **User Creation Failed**
   - Check container logs
   - Verify host system setup
   - Check disk space

3. **SSH Access Issues**
   - Verify SSH service is running
   - Check SSH key permissions
   - Verify user home directory permissions

4. **API Not Responding**
   - Check container status
   - Verify port binding
   - Check firewall rules

### Debug Commands
```bash
# Container shell access
docker exec -it codespace-api /bin/bash

# Check container environment
docker exec codespace-api env

# Test user creation manually
docker exec codespace-api sudo useradd testuser

# Check host system integration
docker exec codespace-api cat /etc/passwd
```

## Scaling Considerations

### Horizontal Scaling
- Use load balancer (nginx, HAProxy)
- Shared storage for /data/codespaces
- Database for user state (if needed)

### Vertical Scaling
- Increase container memory/CPU limits
- Monitor resource usage
- Optimize API performance

## Backup Strategy

### User Data Backup
```bash
# Backup user directories
tar -czf codespaces-backup-$(date +%Y%m%d).tar.gz /data/codespaces/

# Backup system user data
sudo cp /etc/passwd /etc/shadow /etc/group /backup/
```

### Container Configuration Backup
```bash
# Backup Docker configurations
cp docker-compose*.yml /backup/
cp Dockerfile /backup/
```

## Emergency Procedures

### Container Restart
```bash
docker-compose restart
```

### Full Rebuild
```bash
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

### User Cleanup (if needed)
```bash
# List all codespace users
cut -d: -f1 /etc/passwd | grep -E "^cs-|^codespace-"

# Mass cleanup script (USE WITH CAUTION)
# for user in $(cut -d: -f1 /etc/passwd | grep -E "^cs-"); do
#   sudo userdel -r $user
# done
```

## Production Recommendations

1. **Use production docker-compose configuration**
2. **Implement proper logging and monitoring**
3. **Set up regular backups**
4. **Configure firewall rules**
5. **Use SSL/TLS termination**
6. **Implement rate limiting**
7. **Set up health monitoring**
8. **Regular security updates**

## Final Verification

- [ ] API responds to health checks
- [ ] Users can be created successfully
- [ ] SSH access works with generated credentials
- [ ] User directories are properly isolated
- [ ] User deletion works correctly
- [ ] Container logs show no errors
- [ ] Resource usage is within acceptable limits
- [ ] Security configurations are properly applied

Your Codespace API is now ready for production use! 🚀