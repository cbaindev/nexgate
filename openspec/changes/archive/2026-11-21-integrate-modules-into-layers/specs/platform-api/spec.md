## MODIFIED Requirements

### Requirement: Module isolation
The system SHALL keep business modules independent, so that a module only depends on the public contracts of other modules and its persistence is only reachable through its own service layer.

#### Scenario: Direct reference between modules
- **WHEN** a module references the internal code of another module instead of its contracts
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: Layer dependency against the allowed direction
- **WHEN** a layer of a module references a layer it is not allowed to depend on, such as the domain referencing persistence or persistence referencing the application services
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: API layer bypassing module contracts
- **WHEN** an HTTP endpoint of the API depends on a module type that is not part of that module's public contracts
- **THEN** the automated architecture tests fail and the build is rejected

#### Scenario: Persistence reached without the module's service layer
- **WHEN** another module, or an HTTP endpoint of the API, reads or writes the data of a module through that module's persistence instead of through its service layer
- **THEN** the automated architecture tests fail and the build is rejected
- **AND** the only way to reach that data from outside the module is a public contract that applies the organization isolation of the active request

#### Scenario: Shared code depending on modules or on the web layer
- **WHEN** shared cross-cutting code references a business module, a process entry point or the web framework
- **THEN** the automated architecture tests fail and the build is rejected
