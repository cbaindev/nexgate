# admin-shell Specification

## Purpose
Aplicación web progresiva de Nexgate: sesión con cookie httpOnly, organización activa, navegación por permisos, cliente generado del contrato, presentación de errores, interfaz en español, sistema de diseño y tema, selector de sitio por permiso y las pantallas de sitios, de espacios y puertas, de accesos (personas, credenciales, permisos, horarios y prueba de acceso), de dispositivos (con el código de reclamo) y de eventos.

## Requirements

### Requirement: Installable progressive web application
The web application SHALL be a single-page progressive web application that can be installed on phones and computers, SHALL keep only its static files in the device cache, and SHALL never store API responses in the device cache.

#### Scenario: Install the application
- **WHEN** a user opens the application in a browser that supports installation
- **THEN** the browser offers to install it with the Nexgate name and icon, and once installed it opens in its own window

#### Scenario: Open without network
- **WHEN** an installed application is opened without network
- **THEN** it shows its frame and a notice that there is no connection, and shows no data from a previous session

#### Scenario: API responses are not cached
- **WHEN** a user has used the application and the device cache is inspected
- **THEN** it contains only the static files of the application and no response from the API

### Requirement: Sign-in and session
The web application SHALL let a user sign in with email and password, SHALL keep the access token only in memory and the refresh token only in a cookie that scripts cannot read, SHALL renew the session before the access token expires, and SHALL let the user sign out.

#### Scenario: Sign in
- **WHEN** a user signs in with valid credentials and a single membership
- **THEN** the application shows the home of that organization with the user's email and organization name

#### Scenario: Invalid credentials
- **WHEN** a user signs in with invalid credentials
- **THEN** the application shows a single message that the email or password is not valid, without saying which one

#### Scenario: Too many attempts
- **WHEN** sign-in attempts exceed the rate allowed by the server
- **THEN** the application shows that there were too many attempts and to try again in a minute

#### Scenario: Reload keeps the session
- **WHEN** a signed-in user reloads the page or reopens the installed application while the session is valid
- **THEN** the application restores the session without asking for credentials

#### Scenario: Tokens are not readable by scripts
- **WHEN** a user is signed in
- **THEN** neither the access token nor the refresh token is stored in local storage, session storage or a cookie readable by scripts

#### Scenario: Expired session
- **WHEN** the session can no longer be renewed, because it was revoked or expired
- **THEN** the application returns to the sign-in page and discards all data of the session

#### Scenario: Sign out
- **WHEN** a user signs out
- **THEN** the server revokes the session, the application discards all its data and shows the sign-in page, and reloading does not restore the session

### Requirement: Active organization
The web application SHALL work on one active organization at a time, SHALL ask a user with several memberships to choose it, and SHALL let the user switch it, changing the active organization only through the server.

#### Scenario: Several memberships
- **WHEN** a user with memberships in several organizations signs in
- **THEN** the application lists those organizations and enters the chosen one

#### Scenario: Switch organization
- **WHEN** a user switches to another organization from the menu
- **THEN** the application obtains new tokens for that organization, discards every cached datum of the previous one, and shows the navigation of its permissions there

#### Scenario: Membership ended
- **WHEN** the user's membership in the active organization ends while the application is open
- **THEN** the next renewal fails for that organization and the application asks the user to choose another one or to sign in again

### Requirement: Navigation by permissions
The web application SHALL show each section only to users with the read permission of that section in the active organization (sites, spaces and access points, access, devices, events and members), and the platform section only to platform operators. Navigation SHALL only adapt the interface and SHALL NOT replace the checks of the server.

#### Scenario: Administrator
- **WHEN** an administrator of an organization signs in
- **THEN** the navigation shows sites, spaces and access points, access, devices, events and members, and not platform

#### Scenario: Guard
- **WHEN** a guard signs in
- **THEN** the navigation shows sites, spaces and access points, access, devices and events, and not members

#### Scenario: Resident
- **WHEN** a resident signs in
- **THEN** the navigation shows only the home and sites

#### Scenario: Platform operator
- **WHEN** a platform operator signs in
- **THEN** the navigation shows platform

#### Scenario: Section without permission by address
- **WHEN** a user opens by address a section it has no permission for
- **THEN** the application shows a page that says the user has no permission for that section, and requests no data of it

#### Scenario: Section not built yet
- **WHEN** a user with permission opens a section whose screens are not built yet
- **THEN** the application shows that the section arrives in a coming change

### Requirement: Client generated from the contract
The web application SHALL call the API only through a client whose types are generated from the versioned API contract, and its verification SHALL fail when the generated types do not match the contract.

#### Scenario: Contract changes
- **WHEN** the versioned API contract changes and the types are not regenerated
- **THEN** the verification of the web application fails and says how to regenerate them

#### Scenario: Requests use the active session
- **WHEN** the application calls an operation that requires authentication
- **THEN** the request carries the current access token, and a request answered with HTTP 401 is retried once after renewing the session

### Requirement: Error presentation
The web application SHALL present the errors of the API in Spanish: validation errors next to each field, missing permission, missing resource, too many attempts, no connection and unexpected errors with their correlation identifier, without showing internal details.

#### Scenario: Validation errors
- **WHEN** an operation is rejected with HTTP 400 and errors per field
- **THEN** each message appears next to its field

#### Scenario: Missing permission
- **WHEN** an operation is rejected with HTTP 403
- **THEN** the application says the user has no permission for that operation

#### Scenario: Unexpected error
- **WHEN** an operation fails with HTTP 500
- **THEN** the application shows a generic message with the correlation identifier of the request

#### Scenario: No connection
- **WHEN** an operation cannot reach the server
- **THEN** the application says there is no connection and lets the user retry

### Requirement: Sites screen
The web application SHALL let users list the sites of the active organization they can see, and let users with permission to manage sites create and modify them.

#### Scenario: List sites
- **WHEN** a user with permission to read sites opens the sites section
- **THEN** the application lists the visible sites with their name and time zone

#### Scenario: Create a site
- **WHEN** an administrator creates a site with a valid name and time zone
- **THEN** the site appears in the list without reloading the page

#### Scenario: Read-only users
- **WHEN** a user without permission to manage sites opens the sites section
- **THEN** the application shows no action to create or modify sites

#### Scenario: Invalid site
- **WHEN** an administrator creates a site with an empty name
- **THEN** the form shows the server's message next to the name field

### Requirement: Spanish interface
The web application SHALL show every text of its interface in Spanish from a translation catalog, so that other languages can be added without changing the screens.

#### Scenario: Texts come from the catalog
- **WHEN** the verification of the web application runs
- **THEN** it fails if a key used by a screen is missing from the Spanish catalog

### Requirement: Visual identity
The web application SHALL present the Nexgate visual identity consistently on every screen, in the browser and once installed: the Nexgate logo, the brand colors and the brand typeface, and it SHALL load every visual resource from its own origin, so that it looks the same without network.

#### Scenario: Brand on every screen
- **WHEN** a user opens the sign-in page, the choice of organization or any section
- **THEN** the screen shows the Nexgate logo and uses the brand colors and typeface

#### Scenario: Installed application
- **WHEN** a user installs the application
- **THEN** the icon, the splash screen and the window bar use the Nexgate logo and brand colors

#### Scenario: Without network
- **WHEN** an installed application is opened without network
- **THEN** the frame keeps the brand typeface and logo, because no visual resource is requested from another origin

### Requirement: Color theme
The web application SHALL offer a light theme and a dark theme, SHALL follow the theme of the operating system by default, SHALL let the user choose light, dark or system from the user menu, and SHALL remember that choice on the device, storing nothing else besides it. The chosen theme SHALL apply before the first screen is painted.

#### Scenario: Follow the system
- **WHEN** a user who never chose a theme opens the application with the operating system in dark mode
- **THEN** the application shows the dark theme, and switches to light when the operating system switches to light

#### Scenario: Choose a theme
- **WHEN** a user chooses the light theme from the user menu while the operating system is in dark mode
- **THEN** the application switches to light at once and keeps it after reloading the page

#### Scenario: Choice survives the session
- **WHEN** a user who chose the dark theme signs out
- **THEN** the sign-in page keeps the dark theme

#### Scenario: Only the preference is stored
- **WHEN** a user chose a theme and the storage of the browser is inspected
- **THEN** it contains only the theme preference and no token nor datum of the session

#### Scenario: No flash
- **WHEN** a user with the dark theme chosen reloads the page
- **THEN** the page is never painted in the light theme while loading

### Requirement: Loading and empty states
The web application SHALL show placeholders with the shape of the content while a list is loading, and SHALL show an empty state that explains what the section holds when a list has no items, including the main action of the section only when the user has permission to perform it.

#### Scenario: Loading
- **WHEN** a user opens the sites section and the list has not arrived yet
- **THEN** the application shows placeholders with the shape of the site cards and announces to assistive technologies that the content is loading

#### Scenario: Empty list with permission
- **WHEN** an administrator opens the sites section and the organization has no sites
- **THEN** the application shows an empty state that explains what a site is and offers to create the first one

#### Scenario: Empty list without permission
- **WHEN** a user without permission to manage sites opens the sites section and sees no site
- **THEN** the application shows the empty state without the action to create a site

### Requirement: Visual accessibility
The web application SHALL keep a contrast of at least WCAG 2.1 AA for text and controls in both themes, SHALL show a visible focus indicator on every interactive control reached with the keyboard, SHALL let every action be performed with the keyboard, and SHALL disable its animations when the operating system asks to reduce motion.

#### Scenario: Keyboard focus
- **WHEN** a user moves through the sign-in page, the navigation, the user menu and the sites form with the keyboard
- **THEN** every control shows a visible focus indicator and can be activated with the keyboard

#### Scenario: Contrast
- **WHEN** the text and control colors of both themes are checked
- **THEN** body text and controls reach at least the WCAG 2.1 AA contrast ratio against their background

#### Scenario: Reduced motion
- **WHEN** the operating system asks to reduce motion
- **THEN** panels, menus and navigation appear without animations

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

### Requirement: Space tree screen
The web application SHALL show the spaces of the chosen site as a tree that can be expanded and collapsed and filtered by name, SHALL show for the chosen space its details, the access points that lead to it and its access path from outside in order, and SHALL let users with permission to manage spaces create the root space, add child spaces, modify and delete spaces, offering each action only on the spaces the user can manage.

#### Scenario: Show the tree
- **WHEN** a user with permission to read spaces opens a site whose spaces are a building with floors and units
- **THEN** the application shows the building, its floors under it and the units under each floor, each one with the name of its type

#### Scenario: Filter by name
- **WHEN** a user writes part of the name of a unit in the filter
- **THEN** the tree shows that unit and the spaces that contain it, and hides the others

#### Scenario: Details and access path
- **WHEN** a user chooses a unit
- **THEN** the application shows its type, whether it is a common area, the access points that lead to it, and the access points of its access path from outside to the unit in order

#### Scenario: Site without spaces
- **WHEN** an administrator opens a site without spaces
- **THEN** the application shows an empty state that explains the space tree and offers to create the root space

#### Scenario: Add a child space
- **WHEN** an administrator adds a floor under a building with a valid name
- **THEN** the floor appears under the building without reloading the page

#### Scenario: Conflict from the server
- **WHEN** an administrator adds a space with the name of one of its siblings
- **THEN** the form shows the message of the server and keeps the data entered

#### Scenario: Delete with confirmation
- **WHEN** an administrator deletes a leaf space and confirms
- **THEN** the space disappears from the tree, and nothing is deleted if the administrator cancels

#### Scenario: Space that cannot be deleted
- **WHEN** an administrator deletes a space that has children or access points
- **THEN** the application shows the message of the server and the space stays in the tree

#### Scenario: Read-only users
- **WHEN** a guard opens the spaces and access points section
- **THEN** the application shows the tree, the details and the access paths without any action to create, modify or delete

#### Scenario: Manager scoped to a space
- **WHEN** a user who can manage spaces only in building A opens the site
- **THEN** the application shows the whole tree of the site, offers to modify and add spaces only inside building A, and does not offer to create the root space

### Requirement: Access points screen
The web application SHALL list the access points of the chosen site with their type, the space they lead to, the space they are entered from and their fail mode, and SHALL let users with permission to manage spaces create, modify and delete them, offering each action only on the access points whose target space the user can manage.

#### Scenario: List access points
- **WHEN** a user with permission to read spaces opens the access points tab of a site
- **THEN** the application lists each access point with its type, the name of the space it leads to, the name of the space it is entered from or that it is entered from outside, and its fail mode explained in words

#### Scenario: Create an access point
- **WHEN** an administrator creates an access point choosing its type, target space, origin space and fail mode
- **THEN** the access point appears in the list without reloading the page

#### Scenario: Fail mode is required
- **WHEN** an administrator opens the form to create an access point
- **THEN** no fail mode is chosen beforehand, and the form explains what happens to the door without power in each mode

#### Scenario: Access point that cannot be deleted
- **WHEN** an administrator deletes an access point controlled by a device and confirms
- **THEN** the application shows the message of the server and the access point stays in the list

#### Scenario: Read-only users
- **WHEN** a guard opens the access points tab
- **THEN** the application lists the access points without any action to create, modify or delete

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
