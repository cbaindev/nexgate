## MODIFIED Requirements

### Requirement: Organization creation
The system SHALL allow a platform operator to create an organization with a name and the email of its first administrator, SHALL assign the organization a unique opaque identifier and the active status, and SHALL create an invitation for that email with the administrator role at organization scope.

#### Scenario: Valid organization
- **WHEN** a platform operator creates an organization with a non-empty name of up to 200 characters and a valid administrator email
- **THEN** the system responds with HTTP 201, the organization identifier, its name, the active status and the identifier, expiration and acceptance token of the administrator invitation

#### Scenario: Invalid organization name
- **WHEN** a platform operator creates an organization with an empty name or a name longer than 200 characters
- **THEN** the system responds with HTTP 400 and a problem details document that lists the name field

#### Scenario: Invalid administrator email
- **WHEN** a platform operator creates an organization with a missing or malformed administrator email
- **THEN** the system responds with HTTP 400, a problem details document that lists the email field, and no organization is created

#### Scenario: Caller without platform access
- **WHEN** an authenticated caller that is not a platform operator tries to create an organization
- **THEN** the system responds with HTTP 403 and does not create the organization

#### Scenario: Anonymous caller
- **WHEN** a caller without an access token tries to create an organization
- **THEN** the system responds with HTTP 401 and does not create the organization

## ADDED Requirements

### Requirement: Site operations authorized by role scope
The system SHALL allow site operations only to callers whose effective permissions cover them, and SHALL limit a caller with site-scoped permissions to the sites of its scope.

#### Scenario: Site-scoped reader lists sites
- **WHEN** a caller whose only site permission is reading sites with the scope of one site lists sites
- **THEN** the system responds with HTTP 200 and only that site

#### Scenario: Site-scoped reader reads another site
- **WHEN** a caller with site-scoped permissions requests a site of the active organization outside its scope
- **THEN** the system responds with HTTP 404, the same response as for a site that does not exist

#### Scenario: Create a site without organization scope
- **WHEN** a caller whose site management permission has only site scope creates a site
- **THEN** the system responds with HTTP 403 and no site is created

#### Scenario: Modify a site within scope
- **WHEN** a caller with the site management permission for one site updates that site with valid values
- **THEN** the system responds with HTTP 200 and the updated site

#### Scenario: Reader tries to modify a site
- **WHEN** a caller that can read but not manage sites updates a site it can read
- **THEN** the system responds with HTTP 403 and the site remains unchanged
