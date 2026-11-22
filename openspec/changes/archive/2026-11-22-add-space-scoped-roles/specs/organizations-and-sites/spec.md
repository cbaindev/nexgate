## MODIFIED Requirements

### Requirement: Site operations authorized by role scope
The system SHALL allow site operations only to callers whose effective permissions cover them, and SHALL limit a caller with site-scoped permissions to the sites of its scope. A caller whose site read permission has the scope of a space SHALL be able to read the site that contains that space, and a space scope SHALL NOT allow creating or modifying sites.

#### Scenario: Site-scoped reader lists sites
- **WHEN** a caller whose only site permission is reading sites with the scope of one site lists sites
- **THEN** the system responds with HTTP 200 and only that site

#### Scenario: Site-scoped reader reads another site
- **WHEN** a caller with site-scoped permissions requests a site of the active organization outside its scope
- **THEN** the system responds with HTTP 404, the same response as for a site that does not exist

#### Scenario: Space-scoped reader lists sites
- **WHEN** a caller whose only site read permission has the scope of a space of site A lists sites or reads site A
- **THEN** the system responds with HTTP 200 and only site A

#### Scenario: Space-scoped manager modifies its site
- **WHEN** an administrator whose only assignment has the scope of a space of site A updates site A
- **THEN** the system responds with HTTP 403 and the site remains unchanged

#### Scenario: Create a site without organization scope
- **WHEN** a caller whose site management permission has only site or space scope creates a site
- **THEN** the system responds with HTTP 403 and no site is created

#### Scenario: Modify a site within scope
- **WHEN** a caller with the site management permission for one site updates that site with valid values
- **THEN** the system responds with HTTP 200 and the updated site

#### Scenario: Reader tries to modify a site
- **WHEN** a caller that can read but not manage sites updates a site it can read
- **THEN** the system responds with HTTP 403 and the site remains unchanged
