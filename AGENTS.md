# Repository instructions

Follow the permanent [Development Workflow](CONTRIBUTING.md#development-workflow). GitHub MaratKraft/Open-Net is canonical; use feature/fix branches, meaningful commits and pull requests. Never merge into main, alter published tags/assets, publish releases, or change branch protection without the required explicit user approval.

Preserve the working UI and network adapters. Use centralized theme resources. Display only implemented modules. Do not commit runtime state, secrets, downloaded network binaries, generated artifacts or private test output. Safe tests may run only owned stub processes, never real Zapret/WinDivert/TG listeners or system network changes.

Build/test commands and fixture requirements are in docs/BUILD.md. Read the applicable code and documentation before changing their contracts. Report only checks actually executed; a UI/stub test is not a real network test.
