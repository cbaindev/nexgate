## ADDED Requirements

### Requirement: Compiled policy of an access point
The system SHALL compile, for an access point, a policy that contains the time zone of its site, the table of UTC offsets of that time zone for the following 400 days, the time windows used, the active credentials of the site with their validity, and one entry for each pair of a credential and a grant that covers the access point and has not ended, with the window, the validity, the remaining uses and whether it denies.

#### Scenario: Read the compiled policy
- **WHEN** a caller with permission to read access requests the compiled policy of an access point of a visible site
- **THEN** the system responds with HTTP 200, the policy format version, the access point, the time zone, the offsets, the windows, the credentials, the entries and a hash of the content

#### Scenario: Entries follow coverage
- **WHEN** a subject with one card holds an allow grant to unit 5B and the policy of the hall door of building B is compiled
- **THEN** the policy has no entry for that card

#### Scenario: Validity of an entry
- **WHEN** a credential is valid from day 1 to day 20 and its grant from day 10 to day 30
- **THEN** the entry is valid from day 10 to day 20

#### Scenario: Ended grants are not compiled
- **WHEN** a grant has already ended at the moment of compilation
- **THEN** the policy has no entry for it

#### Scenario: Credentials are hashed
- **WHEN** a policy is compiled
- **THEN** it contains the keyed hash of each credential with the key of the site and never its value

#### Scenario: Access point out of reach
- **WHEN** a caller requests the policy of an access point of another organization, of a site outside its scope or that does not exist
- **THEN** the system responds with HTTP 404, the same response in all cases

### Requirement: Deterministic policy hash
The system SHALL compute the policy hash over a canonical form of its content, so that the same grants, credentials, schedules and time zone produce the same hash and any change in them that affects the access point produces a different hash.

#### Scenario: Same content, same hash
- **WHEN** the policy of an access point is compiled twice without changes in between
- **THEN** both compilations have the same hash

#### Scenario: A relevant change modifies the hash
- **WHEN** a grant that covers the access point is created or deleted between two compilations
- **THEN** the hashes differ

#### Scenario: An unrelated change keeps the hash
- **WHEN** a grant that does not cover the access point is created between two compilations
- **THEN** both compilations have the same hash
