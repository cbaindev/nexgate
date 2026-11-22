# access-points Specification

## Purpose
Puntos de acceso de un sitio (puerta, portón, barrera, molinete, ascensor): el espacio hacia el que dan acceso, el espacio desde el que se entra, el modo de fallo explícito y la derivación del camino a un destino, que es la regla de cobertura del motor de permisos (D-13).

## Requirements

### Requirement: Access point creation
The system SHALL allow creating access points inside a site of the active organization, each with a name of 1 to 200 characters unique in the site ignoring case, a type (door, gate, barrier, turnstile or elevator), the space of the same site it gives access to, an optional space of the same site it is entered from, and a fail mode (fail-safe or fail-secure) that MUST be stated explicitly.

#### Scenario: Valid access point
- **WHEN** a caller with permission to manage spaces creates an access point with a valid name, type, target space and fail mode
- **THEN** the system responds with HTTP 201 and the access point identifier, site identifier, name, type, target space, origin space and fail mode

#### Scenario: Missing fail mode
- **WHEN** a caller creates an access point without a fail mode or with a value outside the allowed ones
- **THEN** the system responds with HTTP 400, a problem details document that lists the fail mode field, and no access point is created

#### Scenario: Target or origin space in another site
- **WHEN** a caller creates an access point whose target or origin space belongs to another site, to another organization or does not exist
- **THEN** the system responds with HTTP 400, the same response in all cases, with a problem details document that lists the invalid space field

#### Scenario: Same target and origin
- **WHEN** a caller creates an access point whose origin space is its target space
- **THEN** the system responds with HTTP 400 and a problem details document that lists the origin field

#### Scenario: Duplicate name in the site
- **WHEN** a caller creates or renames an access point with a name already used by another access point of the same site, ignoring case
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and nothing changes

#### Scenario: Site of another organization or that does not exist
- **WHEN** a caller creates an access point in a site that belongs to another organization or does not exist
- **THEN** the system responds with HTTP 404, the same response in both cases, and no access point is created

### Requirement: Access point listing, details, modification and deletion
The system SHALL list the access points of a site ordered by name, return a single access point, allow changing all of its attributes with the rules of creation except its site, and allow deleting it.

#### Scenario: List the access points of a site
- **WHEN** a caller with permission to read spaces lists the access points of a site
- **THEN** the system responds with HTTP 200 and only the access points of that site, ordered by name

#### Scenario: Modify an access point
- **WHEN** a caller with permission to manage spaces changes the name, type, target space, origin space or fail mode of an access point
- **THEN** the system responds with HTTP 200 and the updated access point

#### Scenario: Delete an access point
- **WHEN** a caller with permission to manage spaces deletes an access point
- **THEN** the system responds with HTTP 204 and the access point no longer exists

#### Scenario: Read an access point of another organization
- **WHEN** a caller requests, modifies or deletes an access point that belongs to another organization
- **THEN** the system responds with HTTP 404, the same response as for an access point that does not exist, and nothing changes

### Requirement: Access path to a space
The system SHALL return, for a destination space, the access points on the path to it: every access point of its site whose target space is the destination or one of its ancestors, ordered from the root to the destination and, at the same depth, by name. Access points that give access to other branches of the tree SHALL NOT be part of the path.

#### Scenario: Path to a unit
- **WHEN** a site has the tree root, building A, floor 5 and unit 5B, with a main gate to the root, a hall door to building A, a floor door to floor 5 and a unit door to 5B, and a caller requests the access path to 5B
- **THEN** the system responds with HTTP 200 and the main gate, the hall door, the floor door and the unit door, in that order

#### Scenario: Path to an intermediate space
- **WHEN** a caller requests the access path to building A in the same site
- **THEN** the system responds with the main gate and the hall door only

#### Scenario: Access points of other branches
- **WHEN** the site also has building B with its own hall door and a caller requests the access path to 5B
- **THEN** the hall door of building B is not part of the response

#### Scenario: Path without access points
- **WHEN** no access point gives access to the destination or to any of its ancestors
- **THEN** the system responds with HTTP 200 and an empty list

#### Scenario: Destination out of reach
- **WHEN** a caller requests the access path to a space of another organization, of a site outside its read scope, or that does not exist
- **THEN** the system responds with HTTP 404, the same response in all cases

### Requirement: Access point permissions and site scope
The system SHALL protect access points with the same permissions and scope rules as spaces: reading requires the spaces read permission and creating, modifying and deleting require the spaces manage permission. A site-scoped permission SHALL apply to the access points of that site, and a space-scoped permission SHALL apply to the access points that give access to that space or one of its descendants.

#### Scenario: Site outside the read scope
- **WHEN** a caller whose spaces read permission has the scope of site A lists or reads access points of site B
- **THEN** the system responds with HTTP 404, the same response as for a site or access point that does not exist

#### Scenario: Visible site outside the manage scope
- **WHEN** a caller who can read the access points of a site but cannot manage them creates, modifies or deletes an access point of that site
- **THEN** the system responds with HTTP 403 and nothing changes

#### Scenario: Space-scoped reader lists access points
- **WHEN** a caller whose only spaces read permission has the scope of building A lists the access points of the site
- **THEN** the system responds with HTTP 200 and only the access points that give access to building A or its descendants

#### Scenario: Space-scoped reader reads the path to its spaces
- **WHEN** a caller whose only spaces read permission has the scope of building A requests the access path to a unit of building A
- **THEN** the system responds with HTTP 200 and the whole path, including the access points that give access to the ancestors of building A

#### Scenario: Space-scoped manager targets a space outside its subtree
- **WHEN** an administrator whose only assignment has the scope of building A creates an access point, or moves one of its access points, so that it gives access to a space outside building A
- **THEN** the system responds with HTTP 400, a problem details document that lists the target space field, and nothing changes

#### Scenario: Origin outside the subtree
- **WHEN** an administrator whose only assignment has the scope of building A creates an access point that gives access to building A and is entered from the root of the site
- **THEN** the system responds with HTTP 201

### Requirement: Access point isolation between organizations
The system SHALL store every access point with the organization of its site and SHALL NOT read or write access points outside the active organization, even if the application omits a filter.

#### Scenario: Direct query without a filter
- **WHEN** the application role queries the access points table without a filter while organization A is active
- **THEN** only access points of organization A are returned

#### Scenario: Write with another organization
- **WHEN** the application role tries to insert or update an access point with the identifier of an organization other than the active one
- **THEN** the database rejects the operation
