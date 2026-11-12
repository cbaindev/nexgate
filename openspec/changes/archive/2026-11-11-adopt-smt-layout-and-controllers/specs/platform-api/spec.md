## MODIFIED Requirements

### Requirement: API versioning
The system SHALL expose every business endpoint under a versioned path prefix.

#### Scenario: Versioned business endpoint
- **WHEN** a client calls a business endpoint using the current version prefix
- **THEN** the system processes the request

#### Scenario: Unversioned path
- **WHEN** a client calls a business endpoint without a version prefix
- **THEN** the system responds with HTTP 404

#### Scenario: Unsupported version
- **WHEN** a client calls a business endpoint with a version prefix that the system does not support
- **THEN** the system responds with HTTP 404 as a problem details document and does not process the request

### Requirement: Module isolation
The system SHALL keep business modules independent, so that a module only depends on the public contracts of other modules.

#### Scenario: Direct reference between modules
- **WHEN** a module references the internal code of another module instead of its contracts
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: Layer dependency against the allowed direction
- **WHEN** a layer of a module references a layer it is not allowed to depend on, such as the domain referencing persistence or persistence referencing the application services
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: API layer bypassing module contracts
- **WHEN** an HTTP endpoint of the API depends on a module type that is not part of that module's public contracts
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: Shared code depending on modules or on the web layer
- **WHEN** shared cross-cutting code references a business module, a process entry point or the web framework
- **THEN** the automated architecture tests fail and the build is rejected
