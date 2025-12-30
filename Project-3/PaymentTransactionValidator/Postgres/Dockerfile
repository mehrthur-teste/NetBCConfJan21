# Use the official PostgreSQL image as a base
FROM postgres:latest

# Set environment variables for PostgreSQL
ENV POSTGRES_USER=postgres
ENV POSTGRES_PASSWORD=yourpassword
ENV POSTGRES_DB=yourdatabase

# Copy the SQL file to the container
COPY init.sql /docker-entrypoint-initdb.d/

# Expose PostgreSQL port
EXPOSE 5432
