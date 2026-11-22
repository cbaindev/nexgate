## ADDED Requirements

### Requirement: Access decision
The system SHALL decide whether a credential opens an access point at an instant using only the compiled policy of that access point. An invalid credential SHALL be denied with `credential_invalid`; among the entries that apply at the instant (validity and window), any deny SHALL win with `explicit_deny`; an allow with uses left SHALL give `allowed`; anything else SHALL be denied with `no_permission`.

#### Scenario: Allowed within the schedule
- **WHEN** a valid card has an allow entry with the window Monday to Friday from 08:00 to 17:00 and is presented on a Tuesday at 10:00 local time
- **THEN** the decision is allow with the reason `allowed`

#### Scenario: Outside the schedule
- **WHEN** the same card is presented on a Tuesday at 18:00 or on a Saturday at 10:00 local time
- **THEN** the decision is deny with the reason `no_permission`

#### Scenario: The window end is exclusive
- **WHEN** the same card is presented on a Tuesday at exactly 17:00 local time
- **THEN** the decision is deny with the reason `no_permission`

#### Scenario: Deny always wins
- **WHEN** a card has an allow entry without a window and a deny entry that applies at the instant
- **THEN** the decision is deny with the reason `explicit_deny`

#### Scenario: A deny outside its window does not apply
- **WHEN** a card has an allow entry without a window and a deny entry whose window does not contain the local time of the instant
- **THEN** the decision is allow with the reason `allowed`

#### Scenario: Unknown or expired credential
- **WHEN** a credential that is not in the policy, or one that is in the policy but outside its validity, is presented
- **THEN** the decision is deny with the reason `credential_invalid`

#### Scenario: No uses left
- **WHEN** a card has a single allow entry with zero remaining uses
- **THEN** the decision is deny with the reason `no_permission`

#### Scenario: Local time follows the time zone of the site
- **WHEN** a card with the window Monday to Friday from 08:00 to 17:00 in a site with the time zone America/Argentina/Cordoba is presented at 12:00 UTC on a Tuesday
- **THEN** the decision is allow, because it is 09:00 local time

#### Scenario: Works without a network
- **WHEN** the decision is made
- **THEN** it uses only the compiled policy, the presented credential hash and the instant, so a device can make it without a connection

### Requirement: Access simulation
The system SHALL let a caller with permission to read access simulate presenting a credential at an access point at an instant (now by default), compiling the current policy of that access point and applying the access decision, and SHALL NOT record the simulation as an access event or consume uses.

#### Scenario: Simulate an allowed pass
- **WHEN** an administrator simulates presenting a card of a subject with an allow grant to unit 5B at the unit door within its schedule
- **THEN** the system responds with HTTP 200, the decision allow and the reason `allowed`

#### Scenario: Simulate a pass at another branch
- **WHEN** the administrator simulates presenting the same card at the hall door of building B
- **THEN** the system responds with HTTP 200, the decision deny and the reason `no_permission`

#### Scenario: The credential value is not kept
- **WHEN** a simulation is made
- **THEN** the credential value is used only to compute its hash for that request and is not stored, returned or logged

#### Scenario: Access point out of reach
- **WHEN** a caller simulates at an access point of another organization, of a site outside its scope or that does not exist
- **THEN** the system responds with HTTP 404, the same response in all cases

### Requirement: Shared test vectors
The system SHALL keep in the repository a versioned set of test vectors, each with a compiled policy, a presented credential hash, an instant and the expected decision and reason, covering at least every scenario of the access decision, and the backend tests SHALL run every vector against the decision algorithm.

#### Scenario: Every vector passes
- **WHEN** the backend tests run
- **THEN** every vector in the set produces its expected decision and reason

#### Scenario: Vectors are independent from the backend
- **WHEN** a vector file is read
- **THEN** it contains only the compiled policy format, the inputs and the expected output, so that the firmware can run the same file without the backend
