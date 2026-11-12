# platform-api Specification

## Purpose
Convenciones observables de la API que todos los módulos heredan: endpoints de salud, versionado por prefijo de ruta, formato estándar de errores, correlación de solicitudes, contexto de organización en la telemetría y aislamiento entre módulos.

## Requirements

### Requirement: Health endpoints
The system SHALL expose a liveness endpoint and a readiness endpoint that require no authentication and return no business data.

#### Scenario: Liveness while the process runs
- **WHEN** a client requests the liveness endpoint
- **THEN** the system responds with HTTP 200 as long as the process can serve requests, without checking external dependencies

#### Scenario: Readiness with the database available
- **WHEN** a client requests the readiness endpoint and the database accepts connections
- **THEN** the system responds with HTTP 200

#### Scenario: Readiness with the database unavailable
- **WHEN** a client requests the readiness endpoint and the database does not accept connections
- **THEN** the system responds with HTTP 503 and does not expose connection strings or internal error details

### Requirement: API versioning
The system SHALL expose every business endpoint under a versioned path prefix.

#### Scenario: Versioned business endpoint
- **WHEN** a client calls a business endpoint using the current version prefix
- **THEN** the system processes the request

#### Scenario: Unversioned path
- **WHEN** a client calls a business endpoint without a version prefix
- **THEN** the system responds with HTTP 404

#### Scenario: Unsupported version
- **WHEN** a client calls a business endpoint with a version prefix that the system does not support
- **THEN** the system responds with HTTP 404 as a problem details document and does not process the request

### Requirement: Standard error format
The system SHALL return every error response as a problem details document with a stable machine-readable error code, a human-readable title, the HTTP status and the request correlation identifier.

#### Scenario: Unexpected server error
- **WHEN** an unhandled exception occurs while processing a request
- **THEN** the system responds with HTTP 500 and a problem details document that includes the correlation identifier
- **AND** the response does not include stack traces, exception messages or internal paths

#### Scenario: Validation error
- **WHEN** a client sends a request that fails validation
- **THEN** the system responds with HTTP 400 and a problem details document listing each invalid field

### Requirement: Request correlation
The system SHALL assign a correlation identifier to every request, accept a valid identifier supplied by the client, return it in the response and include it in every log entry and trace of that request.

#### Scenario: Client supplies a correlation identifier
- **WHEN** a client sends a request with a well-formed correlation identifier
- **THEN** the system reuses it in the response and in its logs

#### Scenario: Client does not supply a correlation identifier
- **WHEN** a client sends a request without a correlation identifier
- **THEN** the system generates one and returns it in the response

#### Scenario: Malformed correlation identifier
- **WHEN** a client sends a correlation identifier that is malformed or exceeds the allowed length
- **THEN** the system ignores it, generates a new one and returns that one

### Requirement: Organization context in telemetry
The system SHALL include the active organization identifier in the logs and traces of every request that has one, and SHALL NOT include personal data of people in them.

#### Scenario: Request with an active organization
- **WHEN** a request is processed on behalf of an authenticated user with an active organization
- **THEN** the logs and traces of that request carry the organization identifier

#### Scenario: Request without an active organization
- **WHEN** a request is processed without an active organization, such as a health check
- **THEN** the logs and traces are produced without an organization identifier and without error

### Requirement: Module isolation
The system SHALL keep business modules independent, so that a module only depends on the public contracts of other modules.

#### Scenario: Direct reference between modules
- **WHEN** a module references the internal code of another module instead of its contracts
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: Layer dependency against the allowed direction
- **WHEN** a layer of a module references a layer it is not allowed to depend on, such as the domain referencing persistence or persistence referencing the application services
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: API layer bypassing module contracts
- **WHEN** an HTTP endpoint of the API depends on a module type that is not part of that module's public contracts
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: Shared code depending on modules or on the web layer
- **WHEN** shared cross-cutting code references a business module, a process entry point or the web framework
- **THEN** the automated architecture tests fail and the build is rejected
