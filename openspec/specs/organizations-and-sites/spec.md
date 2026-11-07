# organizations-and-sites Specification

## Purpose
Organizaciones (tenants) y sus sitios: alta de organizaciones como operación de plataforma, consulta de la organización activa y alta, listado, consulta y modificación de sitios con sus validaciones de nombre, tipo y zona horaria.

## Requirements

### Requirement: Organization creation
The system SHALL allow a platform operator to create an organization with a name, and SHALL assign it a unique opaque identifier and the active status.

#### Scenario: Valid organization
- **WHEN** a platform operator creates an organization with a non-empty name of up to 200 characters
- **THEN** the system responds with HTTP 201, the organization identifier, its name and the active status

#### Scenario: Invalid organization name
- **WHEN** a platform operator creates an organization with an empty name or a name longer than 200 characters
- **THEN** the system responds with HTTP 400 and a problem details document that lists the name field

#### Scenario: Caller without platform access
- **WHEN** a caller that is not a platform operator tries to create an organization
- **THEN** the system rejects the request and does not create the organization

### Requirement: Active organization details
The system SHALL let a caller read the details of its active organization and SHALL NOT let it read the details of any other organization.

#### Scenario: Read the active organization
- **WHEN** a caller with an active organization requests that organization
- **THEN** the system responds with HTTP 200 and the organization identifier, name and status

#### Scenario: Active organization that does not exist
- **WHEN** a caller whose active organization identifier does not match any visible organization requests its active organization
- **THEN** the system responds with HTTP 404 and reveals nothing about other organizations

### Requirement: Site creation
The system SHALL allow creating a site inside the active organization with a name, a type (building, gated community, company or house) and a time zone, and SHALL always assign the site to the active organization regardless of the request content.

#### Scenario: Valid site
- **WHEN** a caller with an active organization creates a site with a non-empty name, a valid type and a valid IANA time zone
- **THEN** the system responds with HTTP 201 and the site identifier, name, type, time zone and the identifier of the active organization

#### Scenario: Unknown time zone
- **WHEN** a caller creates a site with a time zone that is not a valid IANA identifier
- **THEN** the system responds with HTTP 400 and a problem details document that lists the time zone field

#### Scenario: Unknown site type
- **WHEN** a caller creates a site with a type outside the allowed values
- **THEN** the system responds with HTTP 400 and a problem details document that lists the type field

#### Scenario: Duplicate site name
- **WHEN** a caller creates or renames a site with a name already used by another site of the same organization, ignoring case
- **THEN** the system responds with HTTP 409 and a problem details document with a stable error code, and the existing site is not changed

#### Scenario: Attempt to create a site in another organization
- **WHEN** a caller with an active organization sends a site creation request that names a different organization
- **THEN** the system ignores that value and creates the site in the active organization, or rejects the request, and in no case creates a site in the other organization

### Requirement: Site listing and details
The system SHALL list and return only the sites of the active organization.

#### Scenario: List sites
- **WHEN** a caller with an active organization lists sites
- **THEN** the system responds with HTTP 200 and only the sites of that organization, ordered by name

#### Scenario: Read a site of the active organization
- **WHEN** a caller requests a site that belongs to its active organization
- **THEN** the system responds with HTTP 200 and the site details

#### Scenario: Read a site of another organization
- **WHEN** a caller requests a site that belongs to a different organization
- **THEN** the system responds with HTTP 404, the same response as for a site that does not exist

### Requirement: Site modification
The system SHALL allow changing the name, time zone and configuration of a site of the active organization, and SHALL NOT allow changing the organization a site belongs to.

#### Scenario: Rename a site
- **WHEN** a caller updates the name of a site of its active organization with a valid value
- **THEN** the system responds with HTTP 200 and the updated site

#### Scenario: Modify a site of another organization
- **WHEN** a caller tries to update a site that belongs to a different organization
- **THEN** the system responds with HTTP 404 and the site remains unchanged

### Requirement: Active organization required for business operations
The system SHALL reject every organization-scoped operation that is made without an active organization.

#### Scenario: Site operation without an active organization
- **WHEN** a caller without an active organization lists, reads, creates or updates sites
- **THEN** the system responds with HTTP 403 and a problem details document with a stable error code that indicates the missing organization context
