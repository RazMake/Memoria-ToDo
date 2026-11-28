# How to write the changelog

Each app's changelog tells its users what changed in each version. [Version tags](../architecture/version-tags.md#each-app-has-its-own-tags-and-notes) owns which file each release reads; Android's separate file is added when publication is implemented. Today [`CHANGELOG.md`](../../CHANGELOG.md) supplies desktop notes, shown in [What is new](../architecture/desktop/updates.md#what-is-new-shows-each-newer-releases-notes). Both files follow the rules on this page.

The user decided what goes in and who it's written for. Proposed, each a decision to confirm: the file's shape, and the other rules this page gives.

## Add an entry with each change a user would notice

The person or agent who delivers a change adds its entry under `## Unreleased`, in the same change. Write in the affected app's changelog. A change users of both apps notice gets an entry for each audience; do not copy internal descriptions between them. The table lists what gets an entry:

| Change | Entry |
| --- | --- |
| A new feature, or a feature removed | Yes |
| Something that looks or works differently | Yes |
| A fix for a bug in a released version | Yes, when users could have met it |
| A fix for a bug no release had, such as one in a feature still under `## Unreleased` | No; correct that feature's entry instead, if it now reads wrong |
| Refactors, tests, documentation, build, tooling, and AI instructions | No |

- **Keep `## Unreleased` a list of what's new since the last release, not a history of the work.** When a later change alters something already listed there, edit its entry. When a change is undone, delete its entry.
- **No user-facing change, no entry.** The [delivery report](process.md#delivery-report) says which entry a change added, or that it added none.

## Write for a user who has never seen the code

Readers know computers, not this app's internals.

- **Say what the user can now do, or what they'll see**, in one or two plain sentences: "You can now pick a color for each tag." not "Added TagColorStore."
- **Use only terms known across the industry**, such as tray icon, keyboard shortcut, sync, or Markdown. Explain anything else, such as a window's name, on first use in each entry: "Task View, the window that lists your tasks, now ...".
- **Never name code**: no modules, classes, files, tests, settings keys, issue numbers, commit IDs, or agents.
- **Follow [writing docs](writing-docs.md#say-it-plainly)** for the rest.

## The file keeps one section per version, newest first

```markdown
# Changelog

<One line saying what the file is for.>

## Unreleased

- You can now ...

## 1.3.0

- ...
```

- **`## Unreleased` comes first.** Add it back, empty, when it's missing.
- **Each released version has a `## <version>` heading**, such as `## 1.3.0`: the version alone, with no `v` and no date. The GitHub release has the date.
- **Entries are a bulleted list.** They may use Markdown, such as bold text and web links.
- **A released section never changes.** Its release has already published it.

## Releasing a version moves `## Unreleased` under the version's heading

Before you tag a version, rename `## Unreleased` to the version's heading, such as `## 1.3.0`, then commit and push. The [version tag script](../architecture/version-tags.md#a-version-is-tagged-only-when-the-changelog-has-its-notes) refuses to tag without that section, and its preview tells you the version to write.

When nothing users notice changed, write one entry that says so, such as "- No changes you'll notice. This version improves how the app is built." A version always has notes.
