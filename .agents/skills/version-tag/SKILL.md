---
name: version-tag
description: Clear a version tag with .agents/version-tag.mjs. Use when asked to delete a version tag. To release a version, it makes no tag and names the release command instead.
---

# Clear a version tag

The design is in [version tags](../../../docs/architecture/version-tags.md). Run every command from the repository root.

1. Pick `node .agents/version-tag.mjs --clear` for the latest version tag, or `--clear <tag>` for the one the user names.
2. Run that command with `--dry-run` added, and show the user the plan it prints.
3. Ask the user to confirm. Only after an explicit yes, run the command on the preview's last line, exactly as printed: don't add, drop, or change any argument. Anything else stops you.
4. If a run fails or refuses, show the script's message and stop. Never edit the approved command, commit, pull, stash, switch branches, or otherwise work around a check. If the user asks again, start again from step 1 with a new preview.
5. Asked to release a version, make no tag. Tell the user to run `node .agents\release.mjs` or the **release a version** task, which need them at a terminal.
