---
name: version-tag
description: Make and push the next version tag, or clear one, with .agents/version-tag.mjs. Use when asked to release a version, bump major, minor, or patch, or delete a version tag.
---

# Make or clear a version tag

The design is in [version tags](../../../docs/architecture/version-tags.md). Run every command from the repository root.

1. Pick the command from the request:
   - `node .agents/version-tag.mjs <part>` for the part the user names, `major`, `minor`, or `patch`; with none named, leave `<part>` out, which means `minor`.
   - `node .agents/version-tag.mjs --clear` for the latest version tag, or `--clear <tag>` for the one the user names.
2. Run that command with `--dry-run` added, and show the user the plan it prints.
3. Ask the user to confirm. Only after an explicit yes, run the command on the preview's last line, exactly as printed: don't add, drop, or change any argument. Anything else stops you.
4. If a run fails or refuses, show the script's message and stop. Never edit the approved command, commit, pull, stash, switch branches, or otherwise work around a check. If the user asks again, start again from step 1 with a new preview.
   - One exception: when the message asks to rename `## Unreleased` in `CHANGELOG.md` to the version's heading, offer to make that rename. Only after an explicit yes, change that one line in the working tree, then stop: the user commits and pushes it.
