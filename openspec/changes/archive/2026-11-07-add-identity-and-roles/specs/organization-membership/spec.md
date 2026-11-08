## ADDED Requirements

### Requirement: Invitations
The system SHALL let a caller with the member management permission invite a person by email to the active organization with one role and one scope, and SHALL respond the same way whether or not that email already has an account.

#### Scenario: Invite a new email
- **WHEN** an administrator invites an email that has no account, with a valid role and scope
- **THEN** the system responds with HTTP 201, the invitation identifier, its expiration and a single-use acceptance token

#### Scenario: Invite an email that already has an account
- **WHEN** an administrator invites an email that already has an account, possibly used in other organizations
- **THEN** the response has the same status and the same fields as for an email without an account, and reveals nothing about that account or its other organizations

#### Scenario: Invalid invitation
- **WHEN** an administrator invites with a malformed email, an unknown role, or a scope that is not a site of the active organization
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

#### Scenario: Invite without permission
- **WHEN** a caller without the member management permission at organization scope creates an invitation
- **THEN** the system responds with HTTP 403 and no invitation is created

### Requirement: Invitation acceptance
The system SHALL let the holder of a valid invitation token join the organization, creating an account when the email has none and requiring sign-in as that account when it already exists, and SHALL accept each token only once and before it expires.

#### Scenario: Accept with a new account
- **WHEN** the holder of a valid token for an email without an account accepts it with a valid password
- **THEN** the system creates the account, an active membership in the invitation's organization and the invited role assignment, and responds with HTTP 200

#### Scenario: Accept with an existing account
- **WHEN** a user authenticated as the account of the invited email accepts a valid token
- **THEN** the system creates the membership and the invited role assignment for that account, and its password is not changed

#### Scenario: Existing account not signed in
- **WHEN** the holder of a valid token for an email that already has an account accepts it without being authenticated as that account
- **THEN** the system responds with HTTP 401, creates nothing and leaves the invitation usable

#### Scenario: Invalid, expired or used token
- **WHEN** someone accepts a token that does not exist, has expired or was already used
- **THEN** the system responds with HTTP 400 and the same problem details document with a stable error code in all three cases

#### Scenario: Already a member
- **WHEN** a user accepts an invitation to an organization where it already has an active membership
- **THEN** the system keeps the single membership and adds the invited role assignment

### Requirement: Member listing
The system SHALL let a caller with the member read permission list the members of the active organization with their role assignments, and nothing about their memberships in other organizations.

#### Scenario: List members
- **WHEN** an administrator lists the members of the active organization
- **THEN** the system responds with HTTP 200 and, for each member, the user identifier, email and role assignments in that organization only

#### Scenario: Members of another organization
- **WHEN** a member of organization A lists members
- **THEN** the response contains no user that is only a member of other organizations

### Requirement: Member removal
The system SHALL let a caller with the member management permission remove a member from the active organization, ending its membership and role assignments there and leaving its account and other memberships untouched.

#### Scenario: Remove a member
- **WHEN** an administrator removes a member of the active organization
- **THEN** the system responds with HTTP 204, and that user's later requests in that organization are rejected even with an unexpired access token

#### Scenario: Remove a member of another organization
- **WHEN** an administrator tries to remove a user who is not a member of the active organization
- **THEN** the system responds with HTTP 404, the same response as for a user that does not exist

#### Scenario: Remove the last administrator
- **WHEN** an administrator tries to remove the only member that holds the administrator role at organization scope
- **THEN** the system responds with HTTP 409 and a problem details document with a stable error code, and the member is not removed
