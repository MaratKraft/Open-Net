# Contributing

Thank you for helping improve Open Net. Use GitHub Issues for reproducible bugs and feature proposals. Include the Open Net version, Windows version, normal/portable mode, component version, steps, expected behavior and actual behavior. Remove proxy secrets, personal paths and identifying data from logs and screenshots.

Discuss substantial changes in an issue before opening a pull request. Keep changes focused, explain the user impact and list checks actually performed. Follow the surrounding C# style; this release has no enforced formatter. See [build and test instructions](docs/BUILD.md).

Never commit runtime profiles, network binaries, keys, tokens or test output. Safe tests must use owned stub processes: do not launch real network tools or drivers in CI. Contributions to original project code are under the [MIT license](LICENSE); preserve third-party notices. Report security problems as described in [SECURITY.md](SECURITY.md).
