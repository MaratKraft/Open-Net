# Contributing

Thank you for helping improve Open Net. Use GitHub Issues for reproducible bugs and feature proposals. Include the Open Net version, Windows version, normal/portable mode, component version, steps, expected behavior and actual behavior. Remove proxy secrets, personal paths and identifying data from logs and screenshots.

Discuss substantial changes in an issue before opening a pull request. Keep changes focused, explain the user impact and list checks actually performed. Follow the surrounding C# style; this release has no enforced formatter. See [build and test instructions](docs/BUILD.md).

Never commit runtime profiles, network binaries, keys, tokens or test output. Safe tests must use owned stub processes: do not launch real network tools or drivers in CI. Contributions to original project code are under the [MIT license](LICENSE); preserve third-party notices. Report security problems as described in [SECURITY.md](SECURITY.md).

## Development Workflow

The canonical source is [MaratKraft/Open-Net](https://github.com/MaratKraft/Open-Net). `main` is stable. Develop features in `feature/...` and fixes in `fix/...`; use meaningful commits and a pull request for every proposed merge. Merge into `main` only after checks and explicit approval from the project owner. Never force-push `main`, delete the stable branch, or rewrite published tags or release assets.

**Open Net — Development** owns implementation, architecture, UI, tests, branches, commits, pull requests and local test builds. **Open Net — GitHub & Releases** owns final release validation, release documentation/notices/checksums, approved merges when explicitly assigned, tags and publishing. Do not publish a GitHub Release without the owner's explicit authorization. Do not merge a development PR as part of preparing it.

Keep secrets, profiles, user data, network binaries and temporary build/test output out of Git. Tests use only owned safe stubs; real network tools, drivers, DNS, routes, firewall changes, foreign-process termination and reboots require separate authorization. Preserve the preceding stable source/artifacts.

Before editing, check status/remotes, fetch branches/tags, verify the published baseline and preserve uncommitted work. Do not use destructive resets/cleanups. For v0.3.2 use `feature/ui-theme-0.3.2`.

Only implemented and supported modules appear in the application. Compatible official upstream releases must not require a new Open Net release solely because their version number changed; preserve source/digest/format validation and explicit failure for unsupported formats.

Recommended `main` protection: require pull requests and successful Windows CI, block force pushes and deletion, and prevent direct pushes. Change repository protection settings only with separate owner approval. The owner may be unable to approve their own PR formally: record human approval before an explicitly assigned merge rather than silently bypassing it.
