# Development workflow changes

CONTRIBUTING.md now contains the permanent Development Workflow: canonical GitHub source, stable main, feature/fix branches, meaningful commits, PRs, explicit merge/release approval, separation of Development and GitHub & Releases, preservation of tags/assets and safe testing/data rules. AGENTS.md points future repository tasks to this workflow. The workspace-level AGENTS.md identifies this Git copy and treats preceding prototypes/builds as archives.

The PR template provides a concise implementation/checks/review checklist. The Windows workflow now triggers for feature/fix pushes and PRs and runs twelve CI-safe checks separately from the interactive local suite before packaging clean test artifacts. It does not publish a release, tag or merge.

README retains download links for the published v0.3.1. Build instructions describe current v0.3.2 development outputs and distinguish CI-safe/local visual/network-file checks. Protection proposals are in BRANCH_PROTECTION.md; settings were read only. ChatGPT persistent instructions cannot be edited with the available tools/native surface and are not claimed saved; PROJECT_INSTRUCTIONS.md contains the exact text to append manually to the existing Open Net project's instructions.
