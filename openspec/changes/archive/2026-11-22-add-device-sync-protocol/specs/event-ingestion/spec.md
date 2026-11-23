## ADDED Requirements

### Requirement: Access event ingestion
The system SHALL accept from an authenticated device batches of 1 to 100 access events, each with a device-generated identifier, the instant, an access point of the device's site, the credential hash if one was read, the decision, the reason and the policy version used. The system SHALL validate the whole batch before storing any event, and SHALL store each event with its reception instant and, when the hash matches a credential of the site, that credential and its subject.

#### Scenario: Send events
- **WHEN** a device sends a batch of valid events
- **THEN** the system responds with HTTP 200, the number of accepted events and the number of duplicates, and the events can be queried

#### Scenario: Known credential
- **WHEN** a device sends an event with the hash of a credential of its site
- **THEN** the stored event refers to that credential and to its subject

#### Scenario: Unknown credential
- **WHEN** a device sends an event with a hash that matches no credential of its site
- **THEN** the event is stored without credential, without subject and without the hash

#### Scenario: Invalid batch
- **WHEN** a device sends an empty batch, more than 100 events, or an event with an unknown decision or reason, without identifier or instant
- **THEN** the system responds with HTTP 400, a problem details document that names each invalid event and field, and no event of the batch is stored

#### Scenario: Access point of another site
- **WHEN** a device sends an event for an access point of another site, of another organization or that does not exist
- **THEN** the system responds with HTTP 400, the same response in the three cases, and no event of the batch is stored

### Requirement: Idempotent ingestion
The system SHALL store each event of a device at most once, identified by the device and the identifier the device generated, so that a device can resend a batch whose response it did not receive.

#### Scenario: Batch sent twice
- **WHEN** a device sends the same batch twice
- **THEN** the second response reports all events as duplicates and each event is stored once

#### Scenario: Partially repeated batch
- **WHEN** a device sends a batch where some events were already stored
- **THEN** only the new events are stored and the response counts the rest as duplicates

### Requirement: Events recorded offline
The system SHALL accept events that happened while the device had no network, keeping the instant reported by the device as the instant of the event and the instant of reception apart.

#### Scenario: Late events
- **WHEN** a device sends, after hours without network, events that happened while it was offline
- **THEN** the events are stored with the instant at which they happened and the instant at which they were received, and the query orders them by the instant at which they happened

### Requirement: Use consumption
The system SHALL consume one use of a grant with limited uses when it stores, for the first time, an allowed event of a known credential at an access point that grant covers, choosing among the allow grants of that credential's subject that applied at the instant of the event. When an applicable allow grant without a use limit exists, the system SHALL NOT consume any use. Remaining uses SHALL NOT go below zero.

#### Scenario: Grant with one use
- **WHEN** a device sends an allowed event for a credential whose only applicable grant has one use left
- **THEN** the grant has no uses left and the next policy package of every device that controls a covered access point has a new version where that entry has no uses left

#### Scenario: Unlimited grant also applies
- **WHEN** a device sends an allowed event for a credential that has an applicable grant without a use limit and another with limited uses
- **THEN** no use is consumed

#### Scenario: Duplicate event
- **WHEN** a device resends an allowed event that was already stored
- **THEN** no further use is consumed

#### Scenario: Two devices offline
- **WHEN** two devices that were offline each send an allowed event for a grant that had one use left
- **THEN** both events are stored and the grant has no uses left, never a negative number

#### Scenario: Denied event
- **WHEN** a device sends a denied event
- **THEN** no use is consumed

### Requirement: Access event query
The system SHALL list the access events of a site, from the most recent to the oldest by the instant they happened, with optional filters by access point, subject and range of instants, in pages of at most 100 events with a cursor for the next page, and SHALL require the permission to read events with the scope of the whole site.

#### Scenario: Query events
- **WHEN** a caller with permission to read events lists the events of a site
- **THEN** the system responds with HTTP 200 and the events of that site, each with its identifier, instants, device, access point, decision, reason, credential and subject, most recent first

#### Scenario: Filter by access point and dates
- **WHEN** a caller lists the events of a site filtered by an access point and a range of instants
- **THEN** only events of that access point inside the range are returned

#### Scenario: Next page
- **WHEN** a caller lists events with a page size and then requests the page indicated by the returned cursor
- **THEN** the second page continues where the first ended, without repetitions

#### Scenario: Resident cannot read events
- **WHEN** a resident lists the events of a site
- **THEN** the system responds with HTTP 403

#### Scenario: Site of another organization
- **WHEN** a caller lists the events of a site that belongs to another organization or does not exist
- **THEN** the system responds with HTTP 404, the same response in both cases

### Requirement: Event isolation between organizations
The system SHALL keep the access events of each organization isolated, so that no query, including direct database queries made with the application role, returns events of another organization.

#### Scenario: Direct query from another organization
- **WHEN** the application role queries the events table with organization A active
- **THEN** it obtains only events of organization A

### Requirement: Event data is not logged
The system SHALL NOT write credential hashes received in events to logs or traces.

#### Scenario: Logs of an ingestion
- **WHEN** a device sends events with known and unknown credential hashes
- **THEN** none of those hashes appears in the logs
