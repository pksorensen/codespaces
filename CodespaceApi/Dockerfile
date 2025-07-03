# Use the official .NET 8.0 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install required packages for user management
RUN apt-get update && apt-get install -y \
    sudo \
    openssh-server \
    && rm -rf /var/lib/apt/lists/*

# Create the codespace-service user that will run the application
RUN useradd -m -s /bin/bash codespace-service

# Create the /data/codespaces directory
RUN mkdir -p /data/codespaces && chown codespace-service:codespace-service /data/codespaces

# Copy the built application
COPY --from=build /app/out .

# Set up sudoers for codespace-service user
RUN echo "codespace-service ALL=(ALL) NOPASSWD: /usr/sbin/useradd, /usr/sbin/userdel, /usr/bin/passwd, /bin/chown, /bin/chmod, /usr/bin/ssh-keygen" >> /etc/sudoers

# Switch to the service user
USER codespace-service

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CodespaceApi.dll"]