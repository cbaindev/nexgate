## MODIFIED Requirements

### Requirement: Site selection
The web application SHALL let the user choose, in every section that works inside a site, one of the sites of the active organization that the user can see and that the read permission of that section covers, SHALL keep the chosen site in the address of the page, and SHALL choose it by itself when only one site qualifies. In the access, devices and events sections, only the sites that the read permission of the section covers entirely (organization or site scope) qualify.

#### Scenario: Only one visible site
- **WHEN** a user who can see only one site opens the spaces and access points section
- **THEN** the application shows the spaces of that site without asking which site

#### Scenario: Several visible sites
- **WHEN** a user who can see several sites opens the spaces and access points section without a site in the address
- **THEN** the application asks the user to choose a site and requests no spaces until the user chooses one

#### Scenario: Chosen site survives a reload
- **WHEN** a user chooses a site and reloads the page
- **THEN** the application shows the same site

#### Scenario: Site that is not visible
- **WHEN** a user opens by address a site that the user cannot see or that does not exist
- **THEN** the application says the site is not available and lets the user choose another one

#### Scenario: Organization without sites
- **WHEN** a user opens the spaces and access points section and the organization has no visible sites
- **THEN** the application explains that spaces belong to a site and, if the user can create sites, offers to go to the sites section

#### Scenario: Access section with a space scope
- **WHEN** a guard whose access read permission covers site A entirely and only a space of site B opens the access section
- **THEN** the application offers only site A and requests no access data of site B

#### Scenario: Devices and events with a space scope
- **WHEN** a guard whose permissions to read devices and events cover site A entirely and only a space of site B opens the devices or the events section
- **THEN** the application offers only site A and requests no devices or events of site B

## ADDED Requirements

### Requirement: Devices screen
The web application SHALL list the devices of the chosen site, SHALL show for the chosen device its state, last contact, policy synchronization, firmware, certificate expiration and controlled access points, and SHALL let users who manage devices of the whole site register, rename, assign access points, issue a new claim code, revoke and delete devices.

#### Scenario: List devices
- **WHEN** a user with permission to read devices opens the devices section of a site
- **THEN** the application lists the devices of the site ordered by name, each with its state

#### Scenario: Device details
- **WHEN** a user chooses an active device that contacted the server two minutes ago, reported an older policy version than the current one and controls the main gate
- **THEN** the application shows that it is online, that it was last seen two minutes ago, that a policy update is pending, its firmware and certificate expiration, and the main gate

#### Scenario: Device without contact
- **WHEN** a user chooses an active device whose last contact was an hour ago
- **THEN** the application shows that it has no contact and since when

#### Scenario: Certificate about to expire
- **WHEN** a user chooses a device whose certificate expires in less than 30 days
- **THEN** the application warns that the certificate is about to expire

#### Scenario: Assign access points
- **WHEN** an administrator assigns the main gate and the hall door to a device
- **THEN** the device shows exactly those access points without reloading the page

#### Scenario: Access point of another device
- **WHEN** an administrator opens the assignment of access points of a device while another device controls the main gate
- **THEN** the main gate cannot be chosen and shows the name of the device that controls it

#### Scenario: Revoke with confirmation
- **WHEN** an administrator revokes a device and confirms
- **THEN** the device shows the revoked state, and the confirmation explained that the device stops working until it is claimed again with a new code

#### Scenario: Read-only users
- **WHEN** a guard opens the devices section
- **THEN** the application shows the devices and their details without any action to register, modify, assign, issue codes, revoke or delete

### Requirement: Claim code presentation
The web application SHALL show the claim code of a device only right after registering the device or issuing a new code, as readable text and as a QR code with the same text, with its expiration in the time zone of the site and an action to copy it, and SHALL discard it when the user closes that view.

#### Scenario: Code after registering
- **WHEN** an administrator registers a device
- **THEN** the application shows the claim code, its QR code, its expiration and a copy action, and says that the code will not be shown again

#### Scenario: Code is not kept
- **WHEN** an administrator closes the view of a claim code
- **THEN** the code is no longer in the page, the address or the storage of the browser, and the details of the device show only that a claim is pending and until when

#### Scenario: New code
- **WHEN** an administrator issues a new claim code for an active device and confirms
- **THEN** the application shows the new code, and the confirmation explained that the device stops working until it is claimed with it

### Requirement: Events screen
The web application SHALL list the access events of the chosen site from the most recent, with filters by access point, subject and range of dates kept in the address, SHALL load older events on request, and SHALL refresh the list every 15 seconds while the page is visible, the range has no end and the user did not pause it.

#### Scenario: List events
- **WHEN** a guard opens the events section of a site
- **THEN** the application lists the events from the most recent, each with its time in the time zone of the site, access point, subject, result with its reason in words and device

#### Scenario: Subject without access permission
- **WHEN** a user who can read events but not access opens the events section
- **THEN** the application shows the events without the names of the subjects and requests no subjects

#### Scenario: Filter by access point and dates
- **WHEN** a user filters the events by the main gate and a range of dates
- **THEN** the application lists only the events of the main gate in that range, and the filters remain after reloading the page

#### Scenario: Load more
- **WHEN** a user asks for more events at the end of the list
- **THEN** the application adds the next page of older events without repeating any

#### Scenario: New events arrive
- **WHEN** a device sends a new event while a user has the events section open
- **THEN** within 15 seconds the event appears at the top, highlighted, and assistive technologies announce how many new events arrived

#### Scenario: Paused
- **WHEN** a user pauses the refresh
- **THEN** the application requests no new events until the user resumes it

#### Scenario: Event recorded offline
- **WHEN** an event was received more than two minutes after it happened
- **THEN** the application marks it as recorded without connection
