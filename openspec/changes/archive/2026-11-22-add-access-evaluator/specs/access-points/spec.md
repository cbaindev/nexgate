## MODIFIED Requirements

### Requirement: Access point listing, details, modification and deletion
The system SHALL list the access points of a site ordered by name, return a single access point, allow changing all of its attributes with the rules of creation except its site, and allow deleting it only when no grant has it as target.

#### Scenario: List the access points of a site
- **WHEN** a caller with permission to read spaces lists the access points of a site
- **THEN** the system responds with HTTP 200 and only the access points of that site, ordered by name

#### Scenario: Modify an access point
- **WHEN** a caller with permission to manage spaces changes the name, type, target space, origin space or fail mode of an access point
- **THEN** the system responds with HTTP 200 and the updated access point

#### Scenario: Delete an access point
- **WHEN** a caller with permission to manage spaces deletes an access point that no grant has as target
- **THEN** the system responds with HTTP 204 and the access point no longer exists

#### Scenario: Delete an access point targeted by a grant
- **WHEN** a caller deletes an access point that is the target of a grant, ended or not
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and the access point is kept

#### Scenario: Read an access point of another organization
- **WHEN** a caller requests, modifies or deletes an access point that belongs to another organization
- **THEN** the system responds with HTTP 404, the same response as for an access point that does not exist, and nothing changes
