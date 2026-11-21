# tenant-isolation Specification

## Purpose
Aislamiento de los datos de negocio por organización: resolución de la organización activa, Row-Level Security en PostgreSQL con un rol de aplicación sin privilegios de bypass, respuesta cuando falta la organización y garantías de que ningún recurso de otra organización se lee, modifica ni revela.

## Requirements

### Requirement: Organization-scoped business data
The system SHALL associate every business record with exactly one organization, with the single exception of the platform-wide person registry of the People module.

#### Scenario: New business record
- **WHEN** any business record is stored
- **THEN** it is stored with the identifier of the organization that was active when it was created

#### Scenario: Business table without isolation
- **WHEN** a business table exists without row-level isolation enabled, enforced and covered by an organization policy
- **THEN** the automated isolation tests fail and the build is rejected

### Requirement: Isolation enforced by the database
The system SHALL enforce organization isolation in the database itself, so that a defect in the application code cannot read or write data of an organization other than the active one.

#### Scenario: Query that omits the organization filter
- **WHEN** the application runs a query over business data without filtering by organization
- **THEN** the database returns only the rows of the active organization

#### Scenario: Write targeting another organization
- **WHEN** the application tries to insert or update a business record with an organization different from the active one
- **THEN** the database rejects the write and no data of the other organization is created or changed

#### Scenario: Application tries to bypass isolation
- **WHEN** the application database account tries to disable row-level isolation, alter a table or act as the table owner
- **THEN** the database rejects the operation

### Requirement: Fail closed without an active organization
The system SHALL return no business data and accept no business writes when there is no active organization.

#### Scenario: Query without an active organization
- **WHEN** business data is queried in a transaction that has no active organization
- **THEN** the query returns no rows and does not fail with an internal error

#### Scenario: Active organization does not leak between requests
- **WHEN** a request for organization A finishes and a later request without an active organization reuses the same database connection
- **THEN** the later request sees no data of organization A

### Requirement: Non-disclosure of other organizations
The system SHALL respond to requests for resources of another organization exactly as it responds for resources that do not exist.

#### Scenario: Guessing a resource identifier
- **WHEN** a caller requests a resource by the identifier of a resource that belongs to another organization
- **THEN** the system responds with HTTP 404 and the response body does not differ from that of a non-existent identifier

### Requirement: Active organization from the access token
The system SHALL resolve the active organization of each request only from the access token of the authenticated user, SHALL treat it as active only while that user has an active membership in it, and SHALL NOT accept an organization supplied by the client in any other way.

#### Scenario: Organization supplied in a header
- **WHEN** a client sends an organization identifier in a request header or in the query string
- **THEN** the system ignores it, and the request has only the active organization stated in its access token, or none

#### Scenario: Anonymous request
- **WHEN** a request carries no access token
- **THEN** the request has no active organization

#### Scenario: Membership ended
- **WHEN** a request carries an unexpired access token whose organization is one where the user no longer has an active membership
- **THEN** the request has no active organization and organization-scoped operations respond with HTTP 403 and the missing organization context error code

#### Scenario: Active organization in telemetry
- **WHEN** a request has an active organization
- **THEN** its logs and traces carry the organization identifier and never the organization name

### Requirement: Isolation of platform identity data
The system SHALL protect identity data that is not owned by a single organization, such as user accounts and sign-in sessions, with database-enforced rules, so that a caller sees only its own account and sessions plus the accounts of the members of its active organization.

#### Scenario: Read accounts without filters
- **WHEN** the application queries user accounts without filtering, on behalf of a user with an active organization
- **THEN** the database returns only that user's account and the accounts of members of the active organization

#### Scenario: Sessions of other users
- **WHEN** the application queries sign-in sessions without filtering, on behalf of a user
- **THEN** the database returns only the sessions of that user

#### Scenario: Unauthenticated access
- **WHEN** the application queries user accounts or sessions without a current user and without an active organization
- **THEN** the database returns no rows, and sign-in uses only a lookup restricted to a single email
