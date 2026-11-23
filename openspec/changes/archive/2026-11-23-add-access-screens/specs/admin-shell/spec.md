## MODIFIED Requirements

### Requirement: Site selection
The web application SHALL let the user choose, in every section that works inside a site, one of the sites of the active organization that the user can see and that the read permission of that section covers, SHALL keep the chosen site in the address of the page, and SHALL choose it by itself when only one site qualifies. In the access section, only the sites that the access read permission covers entirely (organization or site scope) qualify.

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

## ADDED Requirements

### Requirement: Access screen for subjects
The web application SHALL list the subjects of the chosen site, SHALL show the credentials and grants of the chosen subject, and SHALL let users who manage access of the whole site maintain subjects, credentials and grants. Dates and times SHALL be entered and shown in the time zone of the site.

#### Scenario: List subjects
- **WHEN** a user with permission to read access opens the access section of a site
- **THEN** the application lists the subjects of the site ordered by alias, each with its kind

#### Scenario: Subject details
- **WHEN** a user chooses a subject
- **THEN** the application shows its credentials with their type and whether they are valid, not valid yet or expired, and its grants with the effect, the target with its full path, the schedule, the validity and the remaining uses

#### Scenario: No personal data
- **WHEN** an administrator opens the form of a subject
- **THEN** the form asks only for the kind and the alias, and explains that no document, phone number or email should be entered

#### Scenario: Register a credential
- **WHEN** an administrator registers a card with a value for a subject
- **THEN** the credential appears in the details of the subject without its value, and the value is no longer in the form, the address of the page or the storage of the browser

#### Scenario: PIN is hidden while typing
- **WHEN** an administrator types the value of a PIN credential
- **THEN** the field hides the value

#### Scenario: Duplicate credential
- **WHEN** an administrator registers a credential with the type and value of another credential of the site
- **THEN** the form shows the message of the server and the credential is not created

#### Scenario: Grant to a unit within a schedule
- **WHEN** an administrator grants a subject access to unit 5B with the schedule "Lunes a viernes de 8 a 17"
- **THEN** the grant appears in the details of the subject with its target "Torre A › Piso 5 › 5B" and its schedule

#### Scenario: Validity in the time zone of the site
- **WHEN** an administrator of a site in America/Argentina/Cordoba gives a grant validity from 2026-12-01 08:00
- **THEN** the application sends the instant 2026-12-01T11:00:00Z and shows 01/12/2026 08:00 back, whatever the time zone of the browser

#### Scenario: Deny grant
- **WHEN** an administrator creates a grant with the deny effect
- **THEN** the form does not offer a maximum of uses, and the grant is marked as a denial that wins over any permission

#### Scenario: Subject in use
- **WHEN** an administrator deletes a subject that has credentials or grants and confirms
- **THEN** the application shows the message of the server and the subject stays in the list

#### Scenario: Read-only users
- **WHEN** a guard opens the access section
- **THEN** the application shows the subjects, their credentials and grants and the schedules without any action to create, modify or delete

### Requirement: Schedules screen
The web application SHALL list the schedules of the chosen site with their windows described in words, and SHALL let users with permission to manage access of the whole site create, modify and delete schedules with an editor of windows by days of the week and start and end times.

#### Scenario: Windows in words
- **WHEN** a user opens the schedules of a site with a schedule from Monday to Friday from 08:00 to 17:00
- **THEN** the application shows it as "Lunes a viernes, 08:00 a 17:00"

#### Scenario: Create a schedule with two windows
- **WHEN** an administrator creates a schedule with a window from Monday to Friday from 08:00 to 17:00 and another on Saturday from 09:00 to 13:00
- **THEN** the schedule appears in the list with both windows without reloading the page

#### Scenario: Invalid window
- **WHEN** an administrator saves a window without days or whose end is not later than its start
- **THEN** the form marks that window and does not send it

#### Scenario: Schedule in use
- **WHEN** an administrator deletes a schedule that a grant uses and confirms
- **THEN** the application shows the message of the server and the schedule stays in the list

### Requirement: Access test
The web application SHALL let users with permission to read access of the whole site test whether a credential would open an access point of the site now or at a date and time of the site, SHALL show the decision and its reason in words, and SHALL offer the same test from each access point of the spaces and access points section. The tested value SHALL NOT be kept after the test.

#### Scenario: Allowed pass
- **WHEN** a user tests the card of a subject with an allow grant to unit 5B at the unit door within its schedule
- **THEN** the application shows that the person would pass, because a permission allows it at that moment

#### Scenario: Denied pass with its reason
- **WHEN** a user tests a value that no credential of the site has
- **THEN** the application shows that the person would not pass, because the credential is unknown, expired or not valid yet

#### Scenario: Deny wins
- **WHEN** a user tests a credential of a subject with an allow grant and a deny grant that apply at that moment
- **THEN** the application shows that the person would not pass, because a denial wins over any permission

#### Scenario: Test from an access point
- **WHEN** a user with permission to read access chooses "Probar acceso" on an access point of the spaces and access points section
- **THEN** the test opens with that access point already chosen

#### Scenario: Without access permission
- **WHEN** a user who can read spaces but not access opens the access points of a site
- **THEN** the application does not offer "Probar acceso"

#### Scenario: Test is not an event
- **WHEN** a user runs a test
- **THEN** the application says that the test does not open the door, is not recorded and does not use up uses
