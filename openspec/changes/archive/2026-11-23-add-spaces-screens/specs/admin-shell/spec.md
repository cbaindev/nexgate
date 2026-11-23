## ADDED Requirements

### Requirement: Site selection
The web application SHALL let the user choose, in every section that works inside a site, one of the sites of the active organization that the user can see, SHALL keep the chosen site in the address of the page, and SHALL choose it by itself when the user can see only one site.

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
