## ADDED Requirements

### Requirement: API description document
The system SHALL provide an OpenAPI description of every business endpoint of the current API version, and that description SHALL be the contract from which clients are generated. Each operation MUST declare a unique and stable operation identifier, a summary in Spanish, the schema of its request body and of each success response, every error response it can return, and whether it requires an access token. Health endpoints MUST NOT appear in the description.

#### Scenario: Every business endpoint is described
- **WHEN** the description is generated
- **THEN** it contains exactly one operation for each business endpoint under the current version prefix, each with a unique operation identifier
- **AND** it contains no health endpoint

#### Scenario: Success responses declare their schema
- **WHEN** an operation returns a body on success
- **THEN** the description declares that status code with the schema of the body
- **AND** an operation without a body on success declares its status code without a schema

#### Scenario: Error responses use the standard error format
- **WHEN** an operation can respond with an error, such as validation, missing authentication, missing organization context, missing permission, not found, conflict or too many requests
- **THEN** the description declares each of those status codes with the standard error format schema, which includes the error code, the title, the status and the correlation identifier, plus the invalid fields for validation errors

#### Scenario: Operations that require a token
- **WHEN** an operation requires an authenticated user
- **THEN** the description marks it as requiring a bearer access token and declares the HTTP 401 response
- **AND** an operation that does not require a token, such as sign-in or invitation acceptance, is not marked as requiring one

#### Scenario: Stable output
- **WHEN** the description is generated twice from the same code
- **THEN** both documents are identical

### Requirement: Versioned API contract
The system SHALL keep the OpenAPI description of the current API version as a file in the repository, and the automated tests MUST fail when the description generated from the code differs from that file.

#### Scenario: API changed without updating the contract
- **WHEN** an endpoint, request, response or error is added, removed or changed and the versioned file is not updated
- **THEN** the automated tests fail and identify the difference

#### Scenario: Contract updated with the API
- **WHEN** the versioned file is regenerated together with the API change
- **THEN** the automated tests pass and the change to the contract is visible in the same diff as the code

### Requirement: Interactive API reference in development
The system SHALL serve the OpenAPI description and an interactive API reference only in the development environment. The reference MUST allow entering an access token and calling protected operations with it, and those calls MUST go through the same authentication, organization membership, permissions and tenant isolation as any other client.

#### Scenario: Reference in development
- **WHEN** a person opens the interactive reference in the development environment
- **THEN** the system shows every operation of the description, grouped by module

#### Scenario: Calling a protected operation from the reference
- **WHEN** a person enters the access token of a user and calls an operation from the reference
- **THEN** the system responds exactly as it would to any client with that token, including HTTP 403 when the user lacks the permission and only the data of the active organization

#### Scenario: Outside development
- **WHEN** a client requests the interactive reference or the description in any environment other than development
- **THEN** the system responds with HTTP 404
