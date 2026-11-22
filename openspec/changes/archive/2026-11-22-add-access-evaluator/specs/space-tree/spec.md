## MODIFIED Requirements

### Requirement: Space deletion
The system SHALL allow deleting a space only when it has no child spaces, no access point gives access to it or is entered from it, no role assignment or pending invitation has it as scope, and no grant has it as target.

#### Scenario: Delete a leaf space
- **WHEN** a caller with permission to manage spaces deletes a space without children and without related access points, role assignments, pending invitations or grants
- **THEN** the system responds with HTTP 204 and the space no longer exists

#### Scenario: Delete a space with children or access points
- **WHEN** a caller deletes a space that has child spaces or related access points
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and the space is kept

#### Scenario: Delete a space used as a role scope
- **WHEN** a caller deletes a space that is the scope of a role assignment, valid or not, or of an invitation that is neither accepted nor expired
- **THEN** the system responds with HTTP 409, the same problem details document as for a space with children, and the space is kept

#### Scenario: Delete a space after its role scope is revoked
- **WHEN** the only role assignment with the scope of a leaf space is revoked and a caller deletes that space
- **THEN** the system responds with HTTP 204

#### Scenario: Delete a space targeted by a grant
- **WHEN** a caller deletes a space that is the target of a grant, ended or not
- **THEN** the system responds with HTTP 409, the same problem details document as for a space with children, and the space is kept
