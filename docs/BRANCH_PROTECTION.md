# Main branch protection

Verified on 2026-11-02: the repository's [Protect main ruleset](https://github.com/MaratKraft/Open-Net/rules/26346998) is active for the default branch, `main`. The owner configured this protection after the v0.4.0 release.

Current rules:

- Changes must go through a pull request. Required approving reviews: **0**, so a sole maintainer is not blocked by an impossible self-approval requirement.
- The **Windows build and safe checks** check must pass, with **GitHub Actions** as the expected source.
- The PR branch must be up to date with main before merging.
- Branch deletion and force pushes are blocked.
- The bypass list is empty. Review-thread resolution is not currently a required rule.

These GitHub rules supplement the permanent [Development Workflow](../CONTRIBUTING.md#development-workflow): merging still requires explicit owner approval, and publishing a release requires separate authorization. Do not bypass protection or change its settings without separate approval.

Earlier recommendations in historical workflow reports describe the state at the time of those reports, not the current protection settings.

Official sources: [creating repository rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository), [available rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets).
