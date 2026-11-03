# Requirement: Solution restructure into domain folders

| | |
|---|---|
| **Branch** | `features/2026-11-03-solution-restructure` |
| **Milestone** | Housekeeping (no milestone feature) |
| **Status** | Approved |
| **Author** | Chinthaka (plan approved), Claude (execution) |
| **Date** | 2026-11-03 |

## Summary

Reorganise every project so files are grouped into folders by domain, interfaces live in a flat `Interfaces/` folder per project, and test projects mirror the folder structure and namespaces of the code they test. No behaviour changes; type names stay the same; namespaces follow the folders.

## Why

With every type in its own file (previous housekeeping), the project roots became long flat lists. Grouping by domain lets a reader navigate one domain (for example KQL evidence) vertically through Domain, Application, Infrastructure and the tests.

## Acceptance criteria

1. Files in Domain, Application, Infrastructure, Worker, Server and LogForwarder are grouped into domain folders: `Events`, `Kql`, `Grounding`, `Diagnoses`, `Reporting`, `Pipeline`, `Options`, `Status` as applicable. `Common` is reserved for cross-domain items.
2. All interfaces of a project live in a flat `Interfaces/` folder at the project root.
3. Every file's namespace matches its folder path (folder = namespace).
4. Test projects mirror the folder structure and namespaces of the classes they test; shared test helpers live in `Common/`.
5. The leftover sample projects (ApiService, Functions, EmailNotifier), AppHost and ServiceDefaults are unchanged.
6. The dead file `src/DevOpsCopilot.LogForwarder/LogRecord.cs` (fully commented out) is deleted.
7. `dotnet build` and `dotnet test` pass with no behaviour change; JSON contracts (status endpoint, dashboard document, Event Hubs payloads) are unchanged.
