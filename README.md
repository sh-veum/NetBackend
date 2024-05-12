# The project

This a bachelor project for NTNU Ålesund and was commissioned by FiiZK. The project is a web application for Managing Data Retrieval in a Multi-tenant Environment.

# Running the project

## Prerequisites

- Docker desktop
- Node.js
- .NET 8.0 SDK

## Run the project

from the root directory, run the following command:

```sh
docker-compose up --build
```

## Further setup

[Frontend README](frontend/README.md)

[Backend README](backend/README.md)

# Urls

### Backend

Backend REST Api Swagger docs:

```
http://localhost:8088/swagger
```

Backend GraphQL:

```
http://localhost:8088/graphql
```

### Frontend

```
http://localhost:5173/
```

### Kafka UI

Kafka UI:

```
http://localhost:8080/
```

### MockSensor

To start and stop the sensor, go to:

```
http://localhost:8089/startSensor
```

```
http://localhost:8089/stopSensor
```

# Logging in

To log in as admin, use the following credentials:

Email: `admin@mail.com`
Password: `Password!1`

To log in as user, use the following credentials:

Email: `test@mail.com`
Password: `TestPassword1!`

# Demo Video
A demo video of showcasing how to use the frontend.
- [Demo Video]([https://duckduckgo.com](https://youtu.be/o9TnndkU0Yg))
