## MODIFIED Requirements

### Requirement: Space deletion
The system SHALL allow deleting a space only when it has no child spaces, no access point gives access to it or is entered from it, and no role assignment or pending invitation has it as scope.

#### Scenario: Delete a leaf space
- **WHEN** a caller with permission to manage spaces deletes a space without children and without related access points, role assignments or pending invitations
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

### Requirement: Space permissions and site scope
The system SHALL require the spaces read permission to list and read spaces and the spaces manage permission to create, modify and delete them, SHALL apply a site-scoped permission only to the spaces of that site, and SHALL apply a space-scoped permission only to that space and its descendants.

#### Scenario: Read without permission
- **WHEN** an authenticated caller with an active organization and without the spaces read permission lists the spaces of a site
- **THEN** the system responds with HTTP 403 and a problem details document with a stable error code

#### Scenario: Site outside the read scope
- **WHEN** a caller whose spaces read permission has the scope of site A lists or reads spaces of site B
- **THEN** the system responds with HTTP 404, the same response as for a site or space that does not exist

#### Scenario: Visible site outside the manage scope
- **WHEN** a caller who can read the spaces of a site but cannot manage them creates, modifies or deletes a space of that site
- **THEN** the system responds with HTTP 403 and nothing changes

#### Scenario: Space-scoped reader lists the spaces of its site
- **WHEN** a caller whose only spaces read permission has the scope of building A lists the spaces of the site
- **THEN** the system responds with HTTP 200 and only building A and its descendants, in tree order

#### Scenario: Space-scoped manager works inside its subtree
- **WHEN** an administrator whose only assignment has the scope of building A creates a floor under building A, renames building A or deletes a floor of building A
- **THEN** the system responds with HTTP 201, 200 and 204 respectively

#### Scenario: Space-scoped manager with a parent outside its subtree
- **WHEN** an administrator whose only assignment has the scope of building A creates a space whose parent is the root of the site or building B
- **THEN** the system responds with HTTP 400, the same response as for a parent that does not exist, and nothing changes

#### Scenario: Space-scoped manager creates a root
- **WHEN** an administrator whose only assignment has the scope of a space of site A creates a root space in site A or in site B
- **THEN** the system responds with HTTP 403 for site A, which it can see, and HTTP 404 for site B, and nothing changes

#### Scenario: Anonymous caller
- **WHEN** a caller without an access token lists spaces
- **THEN** the system responds with HTTP 401
