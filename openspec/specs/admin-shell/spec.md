# admin-shell Specification

## Purpose
Aplicación web progresiva de Nexgate: sesión con cookie httpOnly, organización activa, navegación por permisos, cliente generado del contrato, presentación de errores, interfaz en español y la pantalla de sitios.

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
