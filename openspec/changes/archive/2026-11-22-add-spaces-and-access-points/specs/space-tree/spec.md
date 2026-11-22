## ADDED Requirements

### Requirement: Space creation
The system SHALL allow creating spaces inside a site of the active organization, forming one tree per site: the root space has the site type and no parent, and every other space has a parent in the same site and one of the types building, floor, unit, common area, parking, lot or zone. Each space SHALL have a name of 1 to 200 characters that is unique among its siblings ignoring case, a flag that marks it as common, and SHALL be at most 10 levels below the root.

#### Scenario: Create the root space
- **WHEN** a caller with permission to manage spaces creates a space of the site type without a parent in a site that has no root yet
- **THEN** the system responds with HTTP 201 and the space identifier, site identifier, type, name, common flag, a null parent and depth 0

#### Scenario: Create a child space
- **WHEN** a caller creates a space of the building type whose parent is the root of the same site
- **THEN** the system responds with HTTP 201, the parent identifier and depth 1

#### Scenario: Second root in a site
- **WHEN** a caller creates a space without a parent in a site that already has a root
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and no space is created

#### Scenario: Space without a parent that is not the site type
- **WHEN** a caller creates a space without a parent and with a type other than site, or a space with a parent and the site type
- **THEN** the system responds with HTTP 400 and a problem details document that lists the type field

#### Scenario: Parent in another site or that does not exist
- **WHEN** a caller creates a space whose parent belongs to another site, to another organization or does not exist
- **THEN** the system responds with HTTP 400, the same response in all cases, with a problem details document that lists the parent field

#### Scenario: Duplicate name among siblings
- **WHEN** a caller creates or renames a space with a name already used by a sibling, ignoring case
- **THEN** the system responds with HTTP 409 and a problem details document with a stable error code, and nothing changes

#### Scenario: Same name under different parents
- **WHEN** a caller creates a space named "Piso 1" under building A and another named "Piso 1" under building B
- **THEN** both spaces are created

#### Scenario: Tree too deep
- **WHEN** a caller creates a space whose depth would exceed 10
- **THEN** the system responds with HTTP 400 and a problem details document that lists the parent field

#### Scenario: Invalid name or type
- **WHEN** a caller creates a space with an empty name, a name longer than 200 characters or a type outside the allowed values
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

#### Scenario: Site of another organization or that does not exist
- **WHEN** a caller creates a space in a site that belongs to another organization or does not exist
- **THEN** the system responds with HTTP 404, the same response in both cases, and no space is created

### Requirement: Space listing and details
The system SHALL list the spaces of a site of the active organization in tree order, where every space appears after its parent and siblings are ordered by name, and SHALL return the details of a single space.

#### Scenario: List the spaces of a site
- **WHEN** a caller with permission to read spaces lists the spaces of a site
- **THEN** the system responds with HTTP 200 and all the spaces of that site, each with its parent and depth, in tree order

#### Scenario: Site without spaces
- **WHEN** a caller lists the spaces of a site that has none
- **THEN** the system responds with HTTP 200 and an empty list

#### Scenario: Read a space
- **WHEN** a caller with permission to read spaces requests a space of a visible site
- **THEN** the system responds with HTTP 200 and the space details

#### Scenario: Read a space of another organization
- **WHEN** a caller requests a space that belongs to another organization
- **THEN** the system responds with HTTP 404, the same response as for a space that does not exist

### Requirement: Space modification
The system SHALL allow changing the name, the type and the common flag of a space, keeping the rules of creation, and SHALL NOT allow changing its site or its parent.

#### Scenario: Rename a space
- **WHEN** a caller with permission to manage spaces changes the name and the common flag of a space
- **THEN** the system responds with HTTP 200 and the updated space

#### Scenario: Change the type of the root
- **WHEN** a caller changes the type of the root space to a type other than site, or the type of a non-root space to site
- **THEN** the system responds with HTTP 400 and a problem details document that lists the type field

### Requirement: Space deletion
The system SHALL allow deleting a space only when it has no child spaces and no access point gives access to it or is entered from it.

#### Scenario: Delete a leaf space
- **WHEN** a caller with permission to manage spaces deletes a space without children and without related access points
- **THEN** the system responds with HTTP 204 and the space no longer exists

#### Scenario: Delete a space with children or access points
- **WHEN** a caller deletes a space that has child spaces or related access points
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and the space is kept

### Requirement: Space permissions and site scope
The system SHALL require the spaces read permission to list and read spaces and the spaces manage permission to create, modify and delete them, and SHALL apply a site-scoped permission only to the spaces of that site.

#### Scenario: Read without permission
- **WHEN** an authenticated caller with an active organization and without the spaces read permission lists the spaces of a site
- **THEN** the system responds with HTTP 403 and a problem details document with a stable error code

#### Scenario: Site outside the read scope
- **WHEN** a caller whose spaces read permission has the scope of site A lists or reads spaces of site B
- **THEN** the system responds with HTTP 404, the same response as for a site or space that does not exist

#### Scenario: Visible site outside the manage scope
- **WHEN** a caller who can read the spaces of a site but cannot manage them creates, modifies or deletes a space of that site
- **THEN** the system responds with HTTP 403 and nothing changes

#### Scenario: Anonymous caller
- **WHEN** a caller without an access token lists spaces
- **THEN** the system responds with HTTP 401

### Requirement: Space isolation between organizations
The system SHALL store every space with the organization of its site and SHALL NOT read or write spaces outside the active organization, even if the application omits a filter.

#### Scenario: Direct query without a filter
- **WHEN** the application role queries the spaces table without a filter while organization A is active
- **THEN** only spaces of organization A are returned

#### Scenario: Write with another organization
- **WHEN** the application role tries to insert or update a space with the identifier of an organization other than the active one
- **THEN** the database rejects the operation
