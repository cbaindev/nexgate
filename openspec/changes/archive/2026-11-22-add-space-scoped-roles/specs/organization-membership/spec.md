## MODIFIED Requirements

### Requirement: Invitations
The system SHALL let a caller with the member management permission invite a person by email to the active organization with one role and one scope (the organization, one of its sites or one of its spaces), and SHALL respond the same way whether or not that email already has an account.

#### Scenario: Invite a new email
- **WHEN** an administrator invites an email that has no account, with a valid role and scope
- **THEN** the system responds with HTTP 201, the invitation identifier, its expiration and a single-use acceptance token

#### Scenario: Invite with space scope
- **WHEN** an administrator invites an email with the guard role and the scope of a space of the active organization, and the invitation is accepted
- **THEN** the new member holds the guard role with the scope of that space

#### Scenario: Invite an email that already has an account
- **WHEN** an administrator invites an email that already has an account, possibly used in other organizations
- **THEN** the response has the same status and the same fields as for an email without an account, and reveals nothing about that account or its other organizations

#### Scenario: Invalid invitation
- **WHEN** an administrator invites with a malformed email, an unknown role, or a scope that is not a site or a space of the active organization
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

#### Scenario: Invite without permission
- **WHEN** a caller without the member management permission at organization scope creates an invitation
- **THEN** the system responds with HTTP 403 and no invitation is created
