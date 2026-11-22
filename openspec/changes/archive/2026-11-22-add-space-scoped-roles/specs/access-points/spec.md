## MODIFIED Requirements

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
