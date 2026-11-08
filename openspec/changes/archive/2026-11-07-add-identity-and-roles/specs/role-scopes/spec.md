## ADDED Requirements

### Requirement: Roles and scopes
The system SHALL let a user hold, within an organization, role assignments made of a role (administrator, guard, resident or employee), a scope (the whole organization or one site of that organization) and an optional validity period.

#### Scenario: Assign a role with site scope
- **WHEN** an administrator assigns the guard role to a member with the scope of a site of the active organization
- **THEN** the system responds with HTTP 201 and the assignment identifier, role, scope and validity

#### Scenario: Scope outside the active organization
- **WHEN** an administrator assigns a role with the scope of a site that belongs to another organization or does not exist
- **THEN** the system responds with HTTP 400, the same response in both cases, and no assignment is created

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
The system SHALL derive a caller's permissions only from its role assignments in the active organization that are valid at the moment of the request, and SHALL evaluate them on the server for every request.

#### Scenario: Permissions by role
- **WHEN** a request is evaluated
- **THEN** the administrator role grants reading the organization, reading and managing sites, and reading and managing members; the guard, resident and employee roles grant reading the organization and reading sites

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
- **THEN** the system responds with HTTP 200 and each permission with the scopes where it applies

#### Scenario: Permissions without an active organization
- **WHEN** an authenticated user without an active organization requests its permissions
- **THEN** the system responds with HTTP 200 and an empty permission list

### Requirement: Last administrator protection
The system SHALL keep at least one valid administrator role assignment at organization scope in every organization that has members.

#### Scenario: Revoke the last administrator assignment
- **WHEN** an administrator revokes the only valid administrator assignment at organization scope of the organization
- **THEN** the system responds with HTTP 409 and a problem details document with a stable error code, and the assignment is kept

### Requirement: Platform operator
The system SHALL distinguish platform operators, who can create organizations, from organization roles, and SHALL create the first platform operator only through an administrative command of the host, never through a public endpoint.

#### Scenario: Create the first operator
- **WHEN** the host administrative command is run with an email and a password supplied outside the command line arguments
- **THEN** an account with the platform operator capability exists for that email and can sign in

#### Scenario: Operator without organization roles
- **WHEN** a platform operator without memberships signs in
- **THEN** it can create organizations and has no permission over the business data of any organization

#### Scenario: Reissue an administrator invitation
- **WHEN** a platform operator requests a new administrator invitation for an existing organization and a valid email
- **THEN** the system responds with HTTP 201 and an invitation with the administrator role at organization scope, without granting the operator any access to that organization's data

#### Scenario: Non-operator requests an administrator invitation
- **WHEN** a caller that is not a platform operator requests an administrator invitation through the platform operation
- **THEN** the system responds with HTTP 403 and no invitation is created
