# policy-sync Specification

## Purpose
Paquete de política firmado por dispositivo con versión monotónica, descarga condicional, latido y funcionamiento sin conexión con la última política válida.

## Requirements

### Requirement: Device policy package
The system SHALL deliver to an authenticated device a policy package in format 1 that contains the device identifier, the site identifier, a version, the instant it was issued, the compiled policy of each access point the device controls ordered by access point identifier, delivered together with a signature over the exact bytes of the package made with the platform policy signing key using ECDSA P-256 with SHA-256.

#### Scenario: Download the policy
- **WHEN** an active device that controls two access points downloads its policy
- **THEN** the system responds with HTTP 200, a package with the policy of both access points in the format of `policy-compilation` and a version, and the signature of the package

#### Scenario: Signature can be verified
- **WHEN** a device verifies the signature of the package it downloaded with the policy verification key it received when it was claimed
- **THEN** the signature is valid

#### Scenario: Tampered package
- **WHEN** any field of a downloaded package is changed and its signature is verified again with the same key
- **THEN** the signature is invalid, and the shared vectors include such a case so the firmware rejects it

#### Scenario: Device without access points
- **WHEN** an active device that controls no access point downloads its policy
- **THEN** the system responds with HTTP 200 and a signed package with no access point policies

#### Scenario: Grant changes reach the device
- **WHEN** an administrator creates a grant that covers an access point of the device and the device downloads its policy again
- **THEN** the package contains the new entry

### Requirement: Monotonic policy version
The system SHALL assign each device a policy version that starts at 1 with its first package and increases by one each time the content of its package changes, and SHALL NOT change the version when the content is the same. Assigning or removing access points of the device SHALL count as a change of content.

#### Scenario: Same content keeps the version
- **WHEN** a device downloads its policy twice and nothing that affects it changed in between
- **THEN** both packages have the same version and the same content

#### Scenario: Changed content increases the version
- **WHEN** a credential of the device's site is created between two downloads
- **THEN** the second package has the version of the first plus one

#### Scenario: Version is kept after reclaiming
- **WHEN** a device is claimed again with a new claim code and downloads its policy
- **THEN** the version is not lower than the last version it was given before

### Requirement: Conditional policy download
The system SHALL allow a device to state the policy version it holds when downloading, and SHALL respond that there are no changes, without a body, when that version is the current one.

#### Scenario: Device already has the current version
- **WHEN** a device downloads its policy stating the current version
- **THEN** the system responds with HTTP 304 and no body

#### Scenario: Device has an older version
- **WHEN** a device downloads its policy stating an older version, for example after working offline
- **THEN** the system responds with HTTP 200 and the package of the current version

### Requirement: Device heartbeat
The system SHALL accept from an authenticated device a heartbeat with its firmware version and the policy version it holds, SHALL record the instant of the last contact and the reported versions, and SHALL respond with the current policy version, the server time and the polling interval in seconds. Every authenticated device request SHALL update the instant of the last contact.

#### Scenario: Heartbeat with an old policy
- **WHEN** a device sends a heartbeat reporting a policy version lower than the current one
- **THEN** the system responds with HTTP 200 and the current version, and the device details show the reported version and the last contact

#### Scenario: Server time for a device without a reliable clock
- **WHEN** a device sends a heartbeat
- **THEN** the response includes the server time in UTC, so a device without network time can correct its clock

#### Scenario: Invalid heartbeat
- **WHEN** a device sends a heartbeat with a negative policy version or a firmware version longer than 50 characters
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

### Requirement: Offline operation with the last valid policy
The system SHALL keep the policy package valid for the device until a newer version replaces it, so that a device without network keeps deciding with the last package it verified.

#### Scenario: Device returns after being offline
- **WHEN** a device that was offline while grants changed sends a heartbeat with its old version
- **THEN** the response tells it the current version, and its next download returns the current package

#### Scenario: Package downloaded before going offline remains verifiable
- **WHEN** a device verifies, without network, a package it downloaded earlier
- **THEN** the signature is valid with the key it received when it was claimed
