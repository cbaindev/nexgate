## ADDED Requirements

### Requirement: Schedules
The system SHALL allow defining schedules in a site of the active organization, each with a name of 1 to 100 characters unique in the site ignoring case and 1 to 20 time windows, where each window has one or more days of the week, a start time and an end time later than the start, in minutes of the day in the time zone of the site, from 00:00 up to 24:00.

#### Scenario: Create a schedule
- **WHEN** a caller with permission to manage access creates the schedule "Lunes a viernes de 8 a 17" with the days Monday to Friday from 08:00 to 17:00
- **THEN** the system responds with HTTP 201 and the schedule identifier, name and windows

#### Scenario: Invalid window
- **WHEN** a caller creates a schedule with a window without days, with an end not later than its start or with a time outside 00:00 to 24:00
- **THEN** the system responds with HTTP 400 and a problem details document that lists the windows field

#### Scenario: Duplicate schedule name
- **WHEN** a caller creates or renames a schedule with a name already used in the same site, ignoring case
- **THEN** the system responds with HTTP 409 and a problem details document with a stable error code

#### Scenario: List, modify and delete schedules
- **WHEN** a caller lists the schedules of a site, changes the windows of a schedule or deletes a schedule that no grant uses
- **THEN** the system responds with HTTP 200, 200 and 204 respectively

#### Scenario: Delete a schedule in use
- **WHEN** a caller deletes a schedule that a grant uses
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and the schedule is kept

### Requirement: Credentials
The system SHALL allow registering credentials for a subject of the same site, each with a type (rf, card or pin), a value of 1 to 64 characters and an optional validity period, SHALL store only a keyed hash of the value with a key specific to the site, and SHALL NOT return or log the value or its hash.

#### Scenario: Register a credential
- **WHEN** a caller with permission to manage access registers a card with a valid value for a subject of the site
- **THEN** the system responds with HTTP 201 and the credential identifier, subject, type and validity, without the value

#### Scenario: The value is never returned
- **WHEN** a caller lists or reads the credentials of a site
- **THEN** no response contains the value of a credential or its hash

#### Scenario: Duplicate credential in the site
- **WHEN** a caller registers a credential with the same type and value as another credential of the same site
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and no credential is created

#### Scenario: Same value in different sites
- **WHEN** a caller registers a card with the same value in two different sites
- **THEN** both credentials are created

#### Scenario: Subject of another site
- **WHEN** a caller registers a credential for a subject of another site, of another organization or that does not exist
- **THEN** the system responds with HTTP 400, the same response in all cases, with a problem details document that lists the subject field

#### Scenario: Invalid validity or type
- **WHEN** a caller registers a credential whose validity ends before it starts or with an unknown type
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

#### Scenario: List, modify and delete credentials
- **WHEN** a caller lists the credentials of a site, filtered or not by subject, changes the validity of a credential or deletes it
- **THEN** the system responds with HTTP 200, 200 and 204 respectively, and a deleted credential no longer opens any access point

### Requirement: Grants
The system SHALL allow creating grants in a site of the active organization, each with a subject of the site, a target that is a space or an access point of the site, an optional schedule of the site, an optional validity period, an effect (allow or deny), the origin `manual` and an optional maximum number of uses for allow grants.

#### Scenario: Create an allow grant to a space
- **WHEN** a caller with permission to manage access grants a subject access to unit 5B from Monday to Friday from 08:00 to 17:00
- **THEN** the system responds with HTTP 201 and the grant identifier, subject, target, schedule, validity, effect, origin and remaining uses

#### Scenario: Create a deny grant
- **WHEN** a caller creates a grant with the deny effect for a subject and a target
- **THEN** the system responds with HTTP 201 and the grant with the deny effect

#### Scenario: Target, subject or schedule outside the site
- **WHEN** a caller creates a grant whose subject, target or schedule belongs to another site, to another organization or does not exist
- **THEN** the system responds with HTTP 400, the same response in all cases, with a problem details document that lists the invalid field

#### Scenario: Invalid grant
- **WHEN** a caller creates a grant with a validity that ends before it starts, an unknown effect or target type, a maximum of uses lower than 1, or a maximum of uses on a deny grant
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

#### Scenario: List and delete grants
- **WHEN** a caller lists the grants of a site, filtered or not by subject, or deletes a grant
- **THEN** the system responds with HTTP 200 or 204, and a deleted grant stops applying to the next compilation and evaluation

### Requirement: Grant coverage
The system SHALL consider that a grant to a space covers every access point on the access path to that space (the access points that give access to the space or to one of its ancestors), and that a grant to an access point covers only that access point.

#### Scenario: Grant to a unit
- **WHEN** a subject holds an allow grant to unit 5B, whose access path has the main gate, the hall door of building A, the floor door and the unit door
- **THEN** the grant covers those four access points and no access point of building B

#### Scenario: Grant to an intermediate space
- **WHEN** a subject holds an allow grant to building A
- **THEN** the grant covers the main gate and the hall door of building A, and not the floor door or the unit door

#### Scenario: Grant to an access point
- **WHEN** a subject holds an allow grant to the pool door
- **THEN** the grant covers only the pool door

### Requirement: Access data isolation between organizations
The system SHALL store every schedule, credential and grant with the organization of its site and SHALL NOT read or write them outside the active organization, even if the application omits a filter.

#### Scenario: Direct query without a filter
- **WHEN** the application role queries the schedules, credentials or grants tables without a filter while organization A is active
- **THEN** only rows of organization A are returned

#### Scenario: Write with another organization
- **WHEN** the application role tries to insert or update a schedule, credential or grant with the identifier of an organization other than the active one
- **THEN** the database rejects the operation

#### Scenario: Resource of another organization through the API
- **WHEN** a caller requests, modifies or deletes a schedule, credential or grant that belongs to another organization
- **THEN** the system responds with HTTP 404, the same response as for one that does not exist, and nothing changes
