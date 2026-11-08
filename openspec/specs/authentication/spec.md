# authentication Specification

## Purpose
Cuentas de usuario de la plataforma e inicio de sesión: emisión de tokens de acceso con la organización activa, renovación con rotación, cambio de organización activa, cierre de sesión y operador de plataforma.

## Requirements

### Requirement: User accounts
The system SHALL keep one platform-wide user account per email address, compared without regard to case, and SHALL store passwords only as salted one-way hashes.

#### Scenario: Password storage
- **WHEN** a user account is created with a password
- **THEN** the stored record contains a salted hash and never the password itself

#### Scenario: Weak password
- **WHEN** an account is created or its password is set with fewer than 12 characters
- **THEN** the system responds with HTTP 400 and a problem details document that lists the password field, and no account is created or changed

#### Scenario: Same email with different case
- **WHEN** an account is created for an email that differs only in letter case from an existing account
- **THEN** the system treats it as the existing account and does not create a second one

### Requirement: Sign-in
The system SHALL authenticate a user with email and password and SHALL issue a short-lived access token and a refresh token, without revealing whether an email has an account.

#### Scenario: Valid credentials with a single membership
- **WHEN** a user with an active membership in exactly one organization signs in with valid credentials
- **THEN** the system responds with HTTP 200, an access token whose active organization is that organization, its lifetime and a refresh token

#### Scenario: Valid credentials with several memberships
- **WHEN** a user with active memberships in several organizations signs in without choosing one
- **THEN** the system issues tokens without an active organization, and organization-scoped operations made with that access token are rejected with HTTP 403 and the missing organization context error code

#### Scenario: Choosing the organization at sign-in
- **WHEN** a user signs in and names an organization where the user has an active membership
- **THEN** the issued access token has that organization as the active one

#### Scenario: Choosing an organization without membership
- **WHEN** a user signs in and names an organization where the user has no active membership, or one that does not exist
- **THEN** the system responds with HTTP 403 and the same problem details document in both cases, and issues no tokens

#### Scenario: Invalid credentials
- **WHEN** someone signs in with a wrong password, an email without an account or the credentials of a disabled account
- **THEN** the system responds with HTTP 401 and the same problem details document with a stable error code in all three cases

#### Scenario: Repeated failed attempts
- **WHEN** sign-in attempts from the same client exceed the configured rate
- **THEN** the system responds with HTTP 429 and does not evaluate the credentials until the limit window passes

#### Scenario: Credentials never logged
- **WHEN** a sign-in attempt is processed, successfully or not
- **THEN** its logs and traces contain neither the password nor the email

### Requirement: Authenticated requests
The system SHALL identify the caller of each request only from a valid access token issued by the system.

#### Scenario: Valid access token
- **WHEN** a request carries a valid, unexpired access token
- **THEN** the system treats the request as made by that user, with the active organization stated in the token

#### Scenario: Missing, expired or tampered token
- **WHEN** a request to an endpoint that requires authentication carries no access token, an expired one or one whose signature does not verify
- **THEN** the system responds with HTTP 401 and a problem details document with a stable error code

#### Scenario: Signing key missing outside development
- **WHEN** the system starts in an environment that is not development without a configured token signing key
- **THEN** the system refuses to start and reports the misconfiguration without exposing secrets

### Requirement: Token refresh and organization switch
The system SHALL let a client exchange a refresh token for new tokens, optionally choosing another organization where the user has an active membership, and SHALL accept each refresh token only once.

#### Scenario: Refresh
- **WHEN** a client presents a valid refresh token
- **THEN** the system responds with a new access token and a new refresh token, and the presented refresh token stops being valid

#### Scenario: Switch organization
- **WHEN** a client presents a valid refresh token and names another organization where the user has an active membership
- **THEN** the new access token has that organization as the active one

#### Scenario: Reuse of a rotated refresh token
- **WHEN** a client presents a refresh token that was already exchanged
- **THEN** the system responds with HTTP 401 and revokes every refresh token of that session

#### Scenario: Refresh after the membership ends
- **WHEN** a client refreshes tokens for an organization where the user no longer has an active membership
- **THEN** the system issues no token with that organization as the active one

### Requirement: Sign-out
The system SHALL let a client end its session, after which the session's refresh tokens are no longer accepted.

#### Scenario: Sign out
- **WHEN** a client signs out presenting its refresh token
- **THEN** the system responds with HTTP 204 and later refreshes with any token of that session fail with HTTP 401

### Requirement: Current user
The system SHALL let an authenticated user read its own identity, its active organization and the organizations where it has an active membership, and nothing about other users.

#### Scenario: Read the current user
- **WHEN** an authenticated user requests its own information
- **THEN** the system responds with HTTP 200, the user identifier, its email, the active organization if any, and the identifier and name of each organization where it has an active membership

#### Scenario: Organizations of other users
- **WHEN** an authenticated user requests its own information
- **THEN** the response does not include any organization where that user has no active membership
