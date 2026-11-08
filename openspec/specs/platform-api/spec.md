# platform-api Specification

## Purpose
Convenciones observables de la API que todos los módulos heredan: endpoints de salud, versionado por prefijo de ruta, formato estándar de errores, correlación de solicitudes, contexto de organización en la telemetría, aislamiento entre módulos y la descripción OpenAPI que funciona como contrato de los clientes.

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

### Requirement: API description document
The system SHALL provide an OpenAPI description of every business endpoint of the current API version, and that description SHALL be the contract from which clients are generated. Each operation MUST declare a unique and stable operation identifier, a summary in Spanish, the schema of its request body and of each success response, every error response it can return, and whether it requires an access token. Health endpoints MUST NOT appear in the description.

#### Scenario: Every business endpoint is described
- **WHEN** the description is generated
- **THEN** it contains exactly one operation for each business endpoint under the current version prefix, each with a unique operation identifier
- **AND** it contains no health endpoint

#### Scenario: Success responses declare their schema
- **WHEN** an operation returns a body on success
- **THEN** the description declares that status code with the schema of the body
- **AND** an operation without a body on success declares its status code without a schema

#### Scenario: Error responses use the standard error format
- **WHEN** an operation can respond with an error, such as validation, missing authentication, missing organization context, missing permission, not found, conflict or too many requests
- **THEN** the description declares each of those status codes with the standard error format schema, which includes the error code, the title, the status and the correlation identifier, plus the invalid fields for validation errors

#### Scenario: Operations that require a token
- **WHEN** an operation requires an authenticated user
- **THEN** the description marks it as requiring a bearer access token and declares the HTTP 401 response
- **AND** an operation that does not require a token, such as sign-in or invitation acceptance, is not marked as requiring one

#### Scenario: Stable output
- **WHEN** the description is generated twice from the same code
- **THEN** both documents are identical

### Requirement: Versioned API contract
The system SHALL keep the OpenAPI description of the current API version as a file in the repository, and the automated tests MUST fail when the description generated from the code differs from that file.

#### Scenario: API changed without updating the contract
- **WHEN** an endpoint, request, response or error is added, removed or changed and the versioned file is not updated
- **THEN** the automated tests fail and identify the difference

#### Scenario: Contract updated with the API
- **WHEN** the versioned file is regenerated together with the API change
- **THEN** the automated tests pass and the change to the contract is visible in the same diff as the code

### Requirement: Interactive API reference in development
The system SHALL serve the OpenAPI description and an interactive API reference only in the development environment. The reference MUST allow entering an access token and calling protected operations with it, and those calls MUST go through the same authentication, organization membership, permissions and tenant isolation as any other client.

#### Scenario: Reference in development
- **WHEN** a person opens the interactive reference in the development environment
- **THEN** the system shows every operation of the description, grouped by module

#### Scenario: Calling a protected operation from the reference
- **WHEN** a person enters the access token of a user and calls an operation from the reference
- **THEN** the system responds exactly as it would to any client with that token, including HTTP 403 when the user lacks the permission and only the data of the active organization

#### Scenario: Outside development
- **WHEN** a client requests the interactive reference or the description in any environment other than development
- **THEN** the system responds with HTTP 404
