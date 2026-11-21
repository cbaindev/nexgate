## ADDED Requirements

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

### Requirement: Active organization resolution
The system SHALL resolve the active organization of each request from a trusted source, and SHALL NOT accept an organization supplied by the client unless a development-only mechanism is explicitly enabled in a development environment.

#### Scenario: Development mechanism disabled
- **WHEN** a client sends an organization identifier in a request header and the development mechanism is disabled
- **THEN** the system ignores it and the request has no active organization

#### Scenario: Development mechanism outside development
- **WHEN** the development mechanism is enabled by configuration in an environment that is not development
- **THEN** the system refuses to start and reports the misconfiguration without exposing secrets

#### Scenario: Active organization in telemetry
- **WHEN** a request has an active organization
- **THEN** its logs and traces carry the organization identifier and never the organization name

### Requirement: Non-disclosure of other organizations
The system SHALL respond to requests for resources of another organization exactly as it responds for resources that do not exist.

#### Scenario: Guessing a resource identifier
- **WHEN** a caller requests a resource by the identifier of a resource that belongs to another organization
- **THEN** the system responds with HTTP 404 and the response body does not differ from that of a non-existent identifier
