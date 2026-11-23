# device-provisioning Specification

## Purpose
Alta de dispositivos por sitio, código de reclamo, reclamo con certificado de cliente emitido por Nexgate, autenticación mTLS, asignación de puntos de acceso, revocación y aislamiento.

## Requirements

### Requirement: Device registration
The system SHALL allow registering devices in a site of the active organization, each with a name of 1 to 200 characters unique in the site regardless of case, and SHALL return, only in the response that creates it, a single-use claim code that expires 7 days after it is issued. A new device SHALL be in the pending state until it is claimed.

#### Scenario: Register a device
- **WHEN** a caller with permission to manage devices registers a device in a site with a valid name
- **THEN** the system responds with HTTP 201, the device identifier, site identifier, name, the pending state, the claim code and its expiration

#### Scenario: Claim code is shown only once
- **WHEN** a caller reads, lists or modifies a device after registering it
- **THEN** no response contains the claim code

#### Scenario: Invalid device
- **WHEN** a caller registers a device with an empty name or a name longer than 200 characters
- **THEN** the system responds with HTTP 400 and a problem details document that lists the invalid fields

#### Scenario: Duplicated name
- **WHEN** a caller registers a device whose name differs only in case from another device of the same site
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and no device is created

#### Scenario: Site of another organization or that does not exist
- **WHEN** a caller registers a device in a site that belongs to another organization or does not exist
- **THEN** the system responds with HTTP 404, the same response in both cases, and no device is created

### Requirement: Device listing, details, modification and deletion
The system SHALL list the devices of a site ordered by name, return a single device with its state, its assigned access points, its last contact, its reported firmware and policy versions, the current policy version and the expiration of its certificate, allow changing its name, and allow deleting it. Deleting a device SHALL invalidate its certificate and release its access points.

#### Scenario: List devices
- **WHEN** a caller with permission to read devices lists the devices of a site
- **THEN** the system responds with HTTP 200 and only the devices of that site, ordered by name

#### Scenario: Modify a device
- **WHEN** a caller with permission to manage devices changes the name of a device
- **THEN** the system responds with HTTP 200 and the updated device

#### Scenario: Delete a device
- **WHEN** a caller with permission to manage devices deletes a device
- **THEN** the system responds with HTTP 204, the device no longer exists, its access points can be assigned to another device, and a request authenticated with its certificate is rejected with HTTP 401

#### Scenario: Device of another organization
- **WHEN** a caller requests, modifies or deletes a device that belongs to another organization
- **THEN** the system responds with HTTP 404, the same response as for a device that does not exist, and nothing changes

### Requirement: Access point assignment
The system SHALL allow replacing the set of access points a device controls with access points of the same site, and SHALL NOT allow an access point to be controlled by more than one device.

#### Scenario: Assign access points
- **WHEN** a caller with permission to manage devices assigns two access points of the device's site to a device
- **THEN** the system responds with HTTP 200 and the device lists exactly those two access points

#### Scenario: Access point already assigned to another device
- **WHEN** a caller assigns to a device an access point that another device controls
- **THEN** the system responds with HTTP 409, a problem details document with a stable error code, and no assignment changes

#### Scenario: Access point of another site or organization
- **WHEN** a caller assigns to a device an access point of another site, of another organization or that does not exist
- **THEN** the system responds with HTTP 400, a problem details document that names the field, the same response in the three cases, and no assignment changes

#### Scenario: Remove all access points
- **WHEN** a caller assigns an empty set of access points to a device
- **THEN** the system responds with HTTP 200 and the device controls no access point

### Requirement: Device claim
The system SHALL allow a device to claim its identity with a valid claim code and a certificate signing request for a P-256 key, without other authentication. On success the system SHALL issue a client certificate signed by the Nexgate device certificate authority, invalidate the code, activate the device, and return the certificate, the authority certificate, the site credential key and the policy verification key. The system SHALL limit claim attempts per client.

#### Scenario: Claim a device
- **WHEN** a device sends a valid, unexpired claim code and a valid certificate signing request
- **THEN** the system responds with HTTP 200, the device identifier, the site identifier, a client certificate whose subject identifies the device, the certificate of the authority, the site credential key, the policy verification key and the polling interval, and the device is active

#### Scenario: Claim code used twice
- **WHEN** a device sends a claim code that was already used
- **THEN** the system responds with HTTP 400 and a problem details document with a stable error code, the same response as for a code that does not exist, and no certificate is issued

#### Scenario: Expired claim code
- **WHEN** a device sends a claim code whose expiration has passed
- **THEN** the system responds with HTTP 400, the same response as for a code that does not exist, and no certificate is issued

#### Scenario: Invalid certificate signing request
- **WHEN** a device sends a valid claim code with a certificate signing request that cannot be read, whose signature is invalid or whose key is not P-256
- **THEN** the system responds with HTTP 400, a problem details document that names the field, no certificate is issued and the claim code remains valid

#### Scenario: Too many attempts
- **WHEN** a client exceeds the allowed number of claim attempts in the time window
- **THEN** the system responds with HTTP 429 and does not evaluate the code

#### Scenario: Reissue the claim code
- **WHEN** a caller with permission to manage devices issues a new claim code for an active device
- **THEN** the system responds with HTTP 200 and the new code and its expiration, the device returns to the pending state, and a request authenticated with its previous certificate is rejected with HTTP 401

### Requirement: Device authentication
The system SHALL authenticate every device request other than the claim with a client certificate issued by the Nexgate device certificate authority, accepted only when it is the current, unexpired certificate of an active device. A device request SHALL operate only on the organization and site of that device, taken from the verified certificate. Device certificates SHALL NOT be accepted on administration endpoints, nor user tokens on device endpoints.

#### Scenario: Request with the current certificate
- **WHEN** an active device makes a request with its current, unexpired certificate
- **THEN** the system processes the request in the organization and site of that device

#### Scenario: Request without a certificate
- **WHEN** a client calls a device endpoint other than the claim without a client certificate
- **THEN** the system responds with HTTP 401

#### Scenario: Certificate from another authority
- **WHEN** a client presents a certificate that the Nexgate device certificate authority did not issue, even with the subject of an existing device
- **THEN** the system responds with HTTP 401

#### Scenario: Revoked device
- **WHEN** a caller with permission to manage devices revokes a device and the device then makes a request with its certificate
- **THEN** the system responds with HTTP 401, and the device keeps its access points and can be claimed again with a new claim code

#### Scenario: User token on a device endpoint
- **WHEN** a client calls a device endpoint with a valid user access token and no client certificate
- **THEN** the system responds with HTTP 401

#### Scenario: Device certificate on an administration endpoint
- **WHEN** a client calls an administration endpoint with a device certificate and no user access token
- **THEN** the system responds with HTTP 401

### Requirement: Device permissions and site scope
The system SHALL require the permission to read devices to list and read devices, and the permission to manage devices to register, modify, assign, reissue codes, revoke and delete them, both with the scope of the whole site of the device.

#### Scenario: Guard reads devices
- **WHEN** a guard with the scope of a site lists the devices of that site
- **THEN** the system responds with HTTP 200

#### Scenario: Guard registers a device
- **WHEN** a guard registers a device
- **THEN** the system responds with HTTP 403 and nothing changes

#### Scenario: Scope of another site
- **WHEN** a caller whose permission to read devices has only the scope of site A reads a device of site B of the same organization
- **THEN** the system responds with HTTP 404, the same response as for a device that does not exist

#### Scenario: Space scope does not reach devices
- **WHEN** a guard whose only assignment has the scope of a space lists the devices of the site that contains it
- **THEN** the system responds with HTTP 404

### Requirement: Device isolation between organizations
The system SHALL keep the devices, their access point assignments, their certificates and their policy versions of each organization isolated, so that no query, including direct database queries made with the application role, returns data of another organization.

#### Scenario: Direct query from another organization
- **WHEN** the application role queries the device tables with organization A active
- **THEN** it obtains only rows of organization A

#### Scenario: Claim code of another organization
- **WHEN** a device claims with a code issued in organization B
- **THEN** the device is claimed in organization B and nothing of organization A is read or changed

### Requirement: Device secrets are not logged
The system SHALL NOT write claim codes, site credential keys, certificate signing requests, certificates or credential hashes to logs or traces.

#### Scenario: Logs of a claim
- **WHEN** a device is registered and claimed and then downloads its policy
- **THEN** none of the claim code, the site credential key, the certificate, nor the credential hashes of the policy appear in the logs
