# Environment Variables & Configuration Guide

This document outlines all required and optional environment variables for running the Murder Stone Archive application in different environments.

## Required Environment Variables

### Database Connection

**`ConnectionStrings__DefaultConnection`** (Required)
- **Description**: PostgreSQL connection string for the application database
- **Format**: `Host=<hostname>;Port=<port>;Username=<username>;Password=<password>;Database=<dbname>`
- **Example**: `Host=localhost;Port=5432;Username=postgres;Password=mysecretpassword;Database=murderstones`
- **Environments**: Development, Staging, Production
- **Note**: The connection string must use PostgreSQL (Npgsql provider)

### ReCAPTCHA Configuration

**`ReCaptcha__SiteKey`** (Required for form submission)
- **Description**: Google reCAPTCHA v2/v3 site key for client-side validation
- **Where to get**: https://www.google.com/recaptcha/admin
- **Environments**: Development, Staging, Production

**`ReCaptcha__SecretKey`** (Required for form submission)
- **Description**: Google reCAPTCHA secret key for server-side verification
- **Where to get**: https://www.google.com/recaptcha/admin
- **Environments**: Development, Staging, Production
- **Security Note**: NEVER commit this value to version control

## Optional Environment Variables

### Logging Configuration

**`ASPNETCORE_ENVIRONMENT`**
- **Default**: `Production`
- **Allowed values**: `Development`, `Staging`, `Production`
- **Description**: Controls HSTS, error pages, and logging verbosity
- **Note**: Set to `Development` locally for detailed error messages and to skip HTTPS redirection

**`ASPNETCORE_URLS`**
- **Default**: `https://localhost:7000`
- **Description**: URLs the application listens on
- **Example**: `http://localhost:5000;https://localhost:7000`

## Docker Environment

When running in Docker, set these variables in your `docker-compose.yml` or `.env` file:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - ConnectionStrings__DefaultConnection=Host=db;Port=5432;Username=postgres;Password=${DB_PASSWORD};Database=murderstones
  - ReCaptcha__SiteKey=${RECAPTCHA_SITE_KEY}
  - ReCaptcha__SecretKey=${RECAPTCHA_SECRET_KEY}
  - ASPNETCORE_URLS=http://+:80;https://+:443
```

## Development Setup

For local development, create a `.env` file in the project root with:

```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Username=postgres;Password=yourpassword;Database=murderstones_dev
ReCaptcha__SiteKey=your_recaptcha_site_key
ReCaptcha__SecretKey=your_recaptcha_secret_key
```

The application will automatically load this file at startup.

## Configuration Initialization

### Database

The application automatically:
1. Loads environment variables from `.env` file (if present)
2. Applies all pending EF Core migrations on startup
3. Creates the default `Admin` role if it doesn't exist

No manual database setup is required beyond setting the connection string.

### Validation

At startup, the application will:
- **Log an error** if `ConnectionStrings__DefaultConnection` is missing or empty
- **Log a warning** if `ReCaptcha__SiteKey` or `ReCaptcha__SecretKey` are missing

These errors are logged but will not prevent the application from starting, allowing for development without external dependencies. Form submission and ReCAPTCHA verification will fail at runtime if these secrets are truly missing.

## Health Check Endpoints

The application exposes the following health check endpoints:

- **`/health`** - Detailed health status (JSON)
- **`/health/live`** - Liveness probe (for container orchestration)
- **`/health/ready`** - Readiness probe (verifies database connectivity)

## Security Considerations

### Secrets Management

1. **Never commit secrets** to version control
2. **Use environment variables** in all environments
3. **Use Azure Key Vault** or similar service in production
4. **Rotate ReCAPTCHA keys** regularly
5. **Database credentials** should follow principle of least privilege

### HTTPS/SSL

- Production requires HTTPS (set `ASPNETCORE_ENVIRONMENT=Production`)
- Development can use HTTP (set `ASPNETCORE_ENVIRONMENT=Development`)
- HSTS (HTTP Strict Transport Security) headers are automatically added in production
- Self-signed certificates are acceptable for development

### Database Access

- Use strong, unique passwords for database accounts
- Use dedicated database user with limited permissions
- Ensure database is not exposed to the internet
- Enable SSL/TLS for database connections when possible

## Troubleshooting

### "Invalid connection string"
- Verify PostgreSQL is running
- Check connection string format
- Ensure database user has permissions
- Test with `psql` or similar tool

### "ReCAPTCHA verification failed"
- Verify site key and secret key are correct
- Ensure domain is registered in Google reCAPTCHA admin panel
- Check that requests are coming from allowed domain

### "Health check failed"
- Verify database connection string
- Ensure database server is reachable
- Check database user permissions
- Review application logs for detailed errors

## Production Deployment Checklist

- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure `ConnectionStrings__DefaultConnection` with production database
- [ ] Set `ReCaptcha__SiteKey` and `ReCaptcha__SecretKey` from production reCAPTCHA setup
- [ ] Verify HTTPS is enabled
- [ ] Confirm database backups are configured
- [ ] Review security headers in responses (use browser developer tools)
- [ ] Test health check endpoints
- [ ] Enable application logging and monitoring
- [ ] Configure log aggregation/rotation
- [ ] Test database migrations run successfully
- [ ] Verify all roles and permissions are set correctly
