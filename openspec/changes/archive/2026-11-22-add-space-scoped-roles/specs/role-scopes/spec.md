## MODIFIED Requirements

### Requirement: Roles and scopes
The system SHALL let a user hold, within an organization, role assignments made of a role (administrator, guard, resident or employee), a scope (the whole organization, one site of that organization or one space of that organization) and an optional validity period.

#### Scenario: Assign a role with site scope
- **WHEN** an administrator assigns the guard role to a member with the scope of a site of the active organization
- **THEN** the system responds with HTTP 201 and the assignment identifier, role, scope and validity

#### Scenario: Assign a role with space scope
- **WHEN** an administrator assigns the guard role to a member with the scope of a space of the active organization
- **THEN** the system responds with HTTP 201 and the assignment with the space scope and the space identifier

#### Scenario: Scope outside the active organization
- **WHEN** an administrator assigns a role with the scope of a site or a space that belongs to another organization or does not exist
- **THEN** the system responds with HTTP 400, the same response in all cases, and no assignment is created

#### Scenario: Scope without its identifier
- **WHEN** an administrator assigns a role with the site scope without a site identifier or with the space scope without a space identifier
- **THEN** the system responds with HTTP 400 and a problem details document that lists the missing field

#### Scenario: Assign a role to a non-member
- **WHEN** an administrator assigns a role to a user who is not a member of the active organization
- **THEN** the system responds with HTTP 404 and no assignment is created

#### Scenario: Invalid validity period
- **WHEN** an administrator assigns a role whose validity ends before it starts
- **THEN** the system responds with HTTP 400 and a problem details document that lists the validity fields

#### Scenario: Revoke a role assignment
- **WHEN** an administrator revokes a role assignment of a member of the active organization
- **THEN** the system responds with HTTP 204 and the permissions granted by that assignment stop applying on the next request

### Requirement: Effective permissions
The system SHALL derive a caller's permissions only from its role assignments in the active organization that are valid at the moment of the request, and SHALL evaluate them on the server for every request. A permission with space scope SHALL apply to that space and all its descendants, and never to its ancestors or to other branches of the tree.

#### Scenario: Permissions by role
- **WHEN** a request is evaluated
- **THEN** the administrator role grants reading the organization, reading and managing sites, reading and managing members, and reading and managing spaces and access points; the guard role grants reading the organization, reading sites and reading spaces and access points; the resident and employee roles grant reading the organization and reading sites

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

### Requirement: Permissions query
The system SHALL let an authenticated user read its effective permissions in the active organization, with the scope of each one, so that clients show only what the user can do.

#### Scenario: Read own permissions
- **WHEN** an authenticated user with an active organization requests its permissions
- **THEN** the system responds with HTTP 200 and each permission with the scopes where it applies: the whole organization, the list of sites, and the list of spaces with the site of each one

#### Scenario: Read permissions with space scope
- **WHEN** a guard whose only assignment has the scope of building A requests its permissions
- **THEN** each of its permissions lists building A with its site, no organization scope and no whole site

#### Scenario: Permissions without an active organization
- **WHEN** an authenticated user without an active organization requests its permissions
- **THEN** the system responds with HTTP 200 and an empty permission list
