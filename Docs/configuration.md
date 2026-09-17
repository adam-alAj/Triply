# Triply Configuration and Environment Management

## Purpose

This document defines how Triply stores and provides environment-specific configuration and secrets.

The backend is the only component responsible for database and Gemini credentials.

## Configuration sources

Triply uses the following configuration pattern:

- ppsettings.json - local/non-secret configuration and local development settings. This file must not be committed when it contains secrets.
- ppsettings.Example.json - committed template showing the expected JSON configuration structure without real secrets.
- .env.example - committed environment-variable template without real secrets.
- .env - local developer environment file. This file must never be committed.
- Environment variables - recommended for CI/CD, staging, and production deployments.

## Secrets

The following values are considered secrets:

- Jwt__Key
- Gemini__ApiKey
- ConnectionStrings__Default when it contains credentials or sensitive connection information

Real secret values must never be stored in source control.

## Environment variable naming

ASP.NET Core nested configuration keys use double underscores (__) when represented as environment variables.

Examples:

- ConnectionStrings__Default
- Jwt__Key
- Jwt__Issuer
- Jwt__Audience
- Jwt__ExpiresMinutes
- Gemini__ApiKey
- Cors__AllowedOrigins

## Local development

Developers may create a local .env file from .env.example and replace the placeholder values with local configuration.

The local ppsettings.json may also be used for development configuration, but real secrets must not be committed.

## CI/CD and production

CI/CD systems and deployment environments must provide secrets through their environment/secret-management facilities.

Real JWT signing keys, Gemini API keys, and production database connection strings must not be written into workflow files, committed configuration files, or source code.

## Git rules

The following files must not contain real secrets in source control:

- .env
- ppsettings.json
- environment-specific secret configuration files

The following templates are safe to commit:

- .env.example
- ppsettings.Example.json

Before committing configuration changes, verify that no real secret values are staged.

## Example

A local environment may contain:

Jwt__Key=<real-local-secret>

while .env.example contains:

Jwt__Key=REPLACE_WITH_A_LONG_RANDOM_SECRET

Only the example value is committed.
