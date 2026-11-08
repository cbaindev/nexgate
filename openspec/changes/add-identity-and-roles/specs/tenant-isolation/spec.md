## REMOVED Requirements

### Requirement: Active organization resolution
**Reason**: Replaced by "Active organization from the access token". The development-only mechanism that let a client supply the organization in a header is removed now that authentication exists.
**Migration**: Clients obtain an access token by signing in and choose the organization at sign-in or with a token refresh; the organization header is ignored.

## ADDED Requirements

### Requirement: Active organization from the access token
The system SHALL resolve the active organization of each request only from the access token of the authenticated user, SHALL treat it as active only while that user has an active membership in it, and SHALL NOT accept an organization supplied by the client in any other way.

#### Scenario: Organization supplied in a header
- **WHEN** a client sends an organization identifier in a request header or in the query string
- **THEN** the system ignores it, and the request has only the active organization stated in its access token, or none

#### Scenario: Anonymous request
- **WHEN** a request carries no access token
- **THEN** the request has no active organization

#### Scenario: Membership ended
- **WHEN** a request carries an unexpired access token whose organization is one where the user no longer has an active membership
- **THEN** the request has no active organization and organization-scoped operations respond with HTTP 403 and the missing organization context error code

#### Scenario: Active organization in telemetry
- **WHEN** a request has an active organization
- **THEN** its logs and traces carry the organization identifier and never the organization name

### Requirement: Isolation of platform identity data
The system SHALL protect identity data that is not owned by a single organization, such as user accounts and sign-in sessions, with database-enforced rules, so that a caller sees only its own account and sessions plus the accounts of the members of its active organization.

#### Scenario: Read accounts without filters
- **WHEN** the application queries user accounts without filtering, on behalf of a user with an active organization
- **THEN** the database returns only that user's account and the accounts of members of the active organization

#### Scenario: Sessions of other users
- **WHEN** the application queries sign-in sessions without filtering, on behalf of a user
- **THEN** the database returns only the sessions of that user

#### Scenario: Unauthenticated access
- **WHEN** the application queries user accounts or sessions without a current user and without an active organization
- **THEN** the database returns no rows, and sign-in uses only a lookup restricted to a single email
