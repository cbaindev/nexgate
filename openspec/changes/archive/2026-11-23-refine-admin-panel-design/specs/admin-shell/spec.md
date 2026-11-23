## ADDED Requirements

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
