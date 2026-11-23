## MODIFIED Requirements

### Requirement: Effective permissions
The system SHALL derive a caller's permissions only from its role assignments in the active organization that are valid at the moment of the request, and SHALL evaluate them on the server for every request. A permission with space scope SHALL apply to that space and all its descendants, and never to its ancestors or to other branches of the tree.

#### Scenario: Permissions by role
- **WHEN** a request is evaluated
- **THEN** the administrator role grants reading the organization, reading and managing sites, reading and managing members, and reading and managing spaces and access points, reading and managing access data (subjects, schedules, credentials, grants, compiled policies and simulations), reading and managing devices, and reading access events; the guard role grants reading the organization, reading sites, reading spaces and access points, reading access data, reading devices and reading access events; the resident and employee roles grant reading the organization and reading sites

#### Scenario: Site scope limits space permissions
- **WHEN** a guard whose only assignment has the scope of site A reads the spaces or access points of site B of the same organization
- **THEN** the system responds as if site B had no such resources, with HTTP 404

#### Scenario: Space scope covers the subtree
- **WHEN** a guard whose only assignment has the scope of building A reads building A, a floor of building A or an access point to that floor
- **THEN** the system responds with HTTP 200

#### Scenario: Space scope excludes ancestors and other branches
- **WHEN** a guard whose only assignment has the scope of building A reads the root space of the site or building B of the same site
- **THEN** the system responds with HTTP 404, the same response as for a space that does not exist

#### Scenario: Space scope does not grant organization-level operations
- **WHEN** an administrator whose only assignment has the scope of a space lists members, invites a person or creates a site
- **THEN** the system responds with HTTP 403 and nothing changes

#### Scenario: Scopes of the same user add up
- **WHEN** a user holds the guard role with the scope of building A and the guard role with the scope of site B
- **THEN** it can read the spaces of building A and of the whole site B, and nothing else

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
