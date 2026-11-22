## ADDED Requirements

### Requirement: Subject registry
The system SHALL allow registering subjects in a site of the active organization, each with a kind (person or anonymous) and an alias of 1 to 100 characters, and SHALL NOT store personal data such as documents, phone numbers or emails for a subject.

#### Scenario: Register a subject
- **WHEN** a caller with permission to manage access registers a subject in a site with a valid kind and alias
- **THEN** the system responds with HTTP 201 and the subject identifier, site identifier, kind and alias

#### Scenario: Invalid subject
- **WHEN** a caller registers a subject with an empty alias, an alias longer than 100 characters or an unknown kind
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

#### Scenario: Site of another organization or that does not exist
- **WHEN** a caller registers a subject in a site that belongs to another organization or does not exist
- **THEN** the system responds with HTTP 404, the same response in both cases, and no subject is created

### Requirement: Subject listing, details, modification and deletion
The system SHALL list the subjects of a site ordered by alias, return a single subject, allow changing its alias and kind, and allow deleting it only when no credential or grant refers to it.

#### Scenario: List subjects
- **WHEN** a caller with permission to read access lists the subjects of a site
- **THEN** the system responds with HTTP 200 and only the subjects of that site, ordered by alias

#### Scenario: Modify a subject
- **WHEN** a caller with permission to manage access changes the alias of a subject
- **THEN** the system responds with HTTP 200 and the updated subject

#### Scenario: Delete a subject without credentials or grants
- **WHEN** a caller with permission to manage access deletes a subject that has no credentials and no grants
- **THEN** the system responds with HTTP 204 and the subject no longer exists

#### Scenario: Delete a subject in use
- **WHEN** a caller deletes a subject that has a credential or a grant
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and the subject is kept

#### Scenario: Subject of another organization
- **WHEN** a caller requests, modifies or deletes a subject that belongs to another organization
- **THEN** the system responds with HTTP 404, the same response as for a subject that does not exist, and nothing changes

### Requirement: Access permissions and site scope
The system SHALL require the access read permission to read subjects, schedules, credentials, grants, compiled policies and simulations, and the access manage permission to create, modify and delete them, and SHALL apply these permissions only to the sites they cover entirely (organization or site scope).

#### Scenario: Read without permission
- **WHEN** an authenticated caller without the access read permission lists the subjects of a site
- **THEN** the system responds with HTTP 403 and a problem details document with a stable error code

#### Scenario: Site outside the scope
- **WHEN** a caller whose access read permission has the scope of site A lists the subjects or grants of site B
- **THEN** the system responds with HTTP 404, the same response as for a site that does not exist

#### Scenario: Reader tries to manage
- **WHEN** a guard, who can read access but not manage it, registers a subject or creates a grant
- **THEN** the system responds with HTTP 403 and nothing changes

#### Scenario: Space scope does not cover access data
- **WHEN** a guard whose only assignment has the scope of a space lists the subjects of the site that contains it
- **THEN** the system responds with HTTP 404

### Requirement: Subject isolation between organizations
The system SHALL store every subject with the organization of its site and SHALL NOT read or write subjects outside the active organization, even if the application omits a filter.

#### Scenario: Direct query without a filter
- **WHEN** the application role queries the subjects table without a filter while organization A is active
- **THEN** only subjects of organization A are returned

#### Scenario: Write with another organization
- **WHEN** the application role tries to insert or update a subject with the identifier of an organization other than the active one
- **THEN** the database rejects the operation
