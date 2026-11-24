# Connect an AI assistant

An AI assistant on your PC can work with your tasks and contacts in Memoria-ToDo. That includes GitHub Copilot in VS Code, Claude Desktop, Claude Code, and Codex. It can list, add, edit, resolve, and delete tasks, manage contacts and tags, sync, and read the app's log. It can't set up Git access or the repository, update the app, or change shortcuts. [What an assistant can do](../architecture/desktop/mcp-server.md#each-tool-is-one-operation-the-user-can-do) lists every operation.

## Connect it in three steps

1. **Open Configure** from the tray icon. Under Application, find the **AI assistants** card.
2. **Check that "Let AI assistants on this PC use Memoria-ToDo" is on.** It's on unless you turned it off. The line below it says whether assistants can connect now.
3. **Click Copy beside your assistant, and paste the text here**:
   - **VS Code**: into `.vscode/mcp.json` in a workspace, or into your user `mcp.json`.
   - **Claude Desktop**: into `claude_desktop_config.json`.
   - **Claude Code**: run the copied command in a terminal.
   - **Codex**: into Codex's `config.toml`.

   Restart the assistant, or reload its MCP servers, so it finds Memoria-ToDo. If the file already lists other servers, merge in only the `memoria-todo` entry.

The copied text starts the app's own program with `--mcp`. If the app isn't running, this starts it with only its tray icon. Copy the text from Configure: it holds the app's path on your PC.

## What to know

- **Only your own programs on this PC can connect.** Other users on the PC and other computers can't.
- **The assistant's changes are real and sync to your other devices.** Deleting a task or a contact can't be undone. A resolved task can be reopened. Most assistants ask you before they delete.
- **The assistant never touches the task you're typing in Add, or the text and images you haven't saved in an edit.** It can change a task you're editing, and both changes are kept, [field by field](../architecture/tasks.md#two-versions-of-a-task-merge-field-by-field). When you both change the same field, the later save wins.
- **An assistant running as administrator can't connect**, because the app doesn't run as administrator.
- **When the app updates, the assistant loses its connection.** It reconnects the next time it needs Memoria-ToDo.

## When it doesn't connect

- **The card says "Off"**: turn on the check box.
- **The card shows an error in red**: the error says what went wrong.
- **The assistant says the app is running but AI access is off**: turn it on in Configure.
- **The assistant says it gave up waiting for the app**: the app didn't start within 30 seconds. Start it from the Start menu, then try again.
- **Still stuck**: open Logs from the tray icon and look for entries from the MCP server.
