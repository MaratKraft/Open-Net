# Recommended main protection

Read-only check on 2026-10-12: the public MaratKraft/Open-Net repository exposes admin permission to the connected account; main was not protected and the public rulesets list was empty. No GitHub protection settings were changed.

GitHub rulesets are available for public repositories, including GitHub Free. Proposed main branch ruleset:

- Require changes through a pull request, with no routine direct-push bypass.
- Require the **Windows build and safe checks** status check from GitHub Actions after its first successful run.
- Block force pushes and branch deletion.
- Require resolution of review discussions.
- Restrict the ability to update main directly; reserve any exceptional bypass for an explicitly approved recovery.

Keep the human owner's explicit approval before merging and separate release authorization. A sole maintainer cannot approve their own PR in GitHub: do not set an impossible self-approval gate. Use an additional reviewer if formal approvals are required; otherwise require PR/CI and keep explicit owner approval as the workflow rule.

These are proposals. Changing settings needs separate owner confirmation. Never enable auto-merge as part of this development task.

Official sources: [creating repository rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository), [available rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets).
