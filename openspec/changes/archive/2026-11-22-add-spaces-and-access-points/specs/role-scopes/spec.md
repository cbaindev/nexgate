## MODIFIED Requirements

### Requirement: Effective permissions
The system SHALL derive a caller's permissions only from its role assignments in the active organization that are valid at the moment of the request, and SHALL evaluate them on the server for every request.

#### Scenario: Permissions by role
- **WHEN** a request is evaluated
- **THEN** the administrator role grants reading the organization, reading and managing sites, reading and managing members, and reading and managing spaces and access points; the guard role grants reading the organization, reading sites and reading spaces and access points; the resident and employee roles grant reading the organization and reading sites

#### Scenario: Site scope limits space permissions
- **WHEN** a guard whose only assignment has the scope of site A reads the spaces or access points of site B of the same organization
- **THEN** the system responds as if site B had no such resources, with HTTP 404

#### Scenario: Assignment outside its validity
- **WHEN** a role assignment has not started yet or has already ended
- **THEN** it grants no permission

#### Scenario: Assignments of another organization
- **WHEN** a user holds role assignments in organization A and makes a request with organization B active
- **THEN** only its assignments in organization B are considered

#### Scenario: Change takes effect immediately
- **WHEN** a role assignment or membership is revoked while the user holds an unexpired access token
- **THEN** the next request with that token is evaluated without the revoked permissions

#### Scenario: Operation without permission
- **WHEN** an authenticated caller with an active organization performs an operation for which it has no permission
- **THEN** the system responds with HTTP 403 and a problem details document with a stable error code
