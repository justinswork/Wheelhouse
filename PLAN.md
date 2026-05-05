# Wheelhouse — Full Development Plan

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Tech Stack](#2-tech-stack)
3. [Solution Architecture](#3-solution-architecture)
4. [UI Design Philosophy](#4-ui-design-philosophy)
5. [Phased Roadmap](#5-phased-roadmap)
   - [Phase 1: Foundation & App Shell](#phase-1-foundation--app-shell)
   - [Phase 2: Core Git Workflows](#phase-2-core-git-workflows)
   - [Phase 3: Advanced Git Operations](#phase-3-advanced-git-operations)
   - [Phase 4: Hosting Integrations](#phase-4-hosting-integrations)
   - [Phase 5: Advanced Features](#phase-5-advanced-features)
   - [Phase 6: Polish & Distribution](#phase-6-polish--distribution)
6. [Key Technical Challenges](#6-key-technical-challenges)
7. [CI/CD & Distribution](#7-cicd--distribution)
8. [Future Contributor Guidelines](#8-future-contributor-guidelines)

---

## 1. Project Overview

**Wheelhouse** is a free, open-source, native Windows Git GUI client inspired by SmartGit but designed with a modern, single-window tabbed interface. It targets developers who want a full-featured Git client without the licensing cost or the fragmented multi-window UX of existing tools.

### Goals

- Full feature parity with SmartGit over time
- Single-window, tabbed UI — no separate popup windows for log, merge tool, blame, etc.
- Integrated terminal pane synced to the active repository
- First-class support for GitHub and Azure DevOps (with an extensible abstraction for future providers)
- Native Windows performance — no Electron, no JVM overhead
- Fully open source under the Apache 2.0 license

### Non-Goals (v1)

- Cross-platform support (Windows-only by design; .NET 10 + WPF)
- SVN/Mercurial/Perforce bridge support
- GitLab / Bitbucket hosting integrations (designed to add later, not in initial scope)
- Mobile or web companion app

### Key Improvements Over SmartGit

| SmartGit Behavior | Wheelhouse Approach |
|---|---|
| Opens separate windows for Log, Working Tree, Merge Tool, Blame | Everything lives in tabs within a single main window |
| No integrated terminal | Terminal pane docked at the bottom, toggleable, with multiple tabs |
| Paid license required for commercial use | 100% free, Apache 2.0 |
| Java/SWT — feels non-native on Windows | Pure WPF — native Windows look, feel, and performance |

---

## 2. Tech Stack

### Runtime & Framework

| Component | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 | Latest LTS-adjacent; best performance, modern C# features |
| UI Framework | WPF | Mature, proven for complex data-heavy Windows apps; XAML + data binding |
| Language | C# 13 | Familiar to contributors; excellent tooling |
| Min OS | Windows 10 1903 (build 18362) | Covers >95% of active Windows installs; required for ConPTY terminal API |

### Core NuGet Packages

| Package | Purpose |
|---|---|
| `LibGit2Sharp` | Git operations (wraps libgit2 — no shelling out to git.exe) |
| `CommunityToolkit.Mvvm` | MVVM boilerplate: `ObservableObject`, `RelayCommand`, source generators |
| `Microsoft.Extensions.DependencyInjection` | DI container |
| `Microsoft.Extensions.Hosting` | App host / lifetime management |
| `Microsoft.Extensions.Logging` | Structured logging |
| `AvalonEdit` | Syntax-highlighted diff and code viewer (WPF-native) |
| `Microsoft.Web.WebView2` | Chromium-based web view for the terminal emulator (xterm.js host) |
| `Octokit` | GitHub REST API client |
| `Microsoft.TeamFoundationServer.Client` | Azure DevOps REST API client |
| `System.Text.Json` | JSON serialization for settings, API payloads |
| `xunit` + `NSubstitute` | Unit testing framework + mocking |

### Terminal Implementation

The integrated terminal will use **WebView2 + xterm.js** backed by the Windows **ConPTY API** (Pseudo Console):

- **ConPTY** (`CreatePseudoConsole` Win32 API, available on Windows 10 1903+) spawns shell processes with a real PTY
- **xterm.js** (loaded inside a WebView2 panel) handles VT100/ANSI rendering with full color and mouse support
- A lightweight .NET bridge marshals I/O between the ConPTY process and the WebView2 JS context via `PostWebMessageAsString` / `WebMessageReceived`
- Shell selection: PowerShell 7, Windows PowerShell 5.1, CMD, Git Bash, WSL (auto-detected at startup)
- The terminal's working directory is automatically set to the active repository root when switching repos

---

## 3. Solution Architecture

### Project Structure

```
Wheelhouse.sln
├── src/
│   ├── Wheelhouse.Core/
│   │   Git domain model, LibGit2Sharp wrappers, diff engine, settings
│   │
│   ├── Wheelhouse.Hosting.Abstractions/
│   │   IHostingProvider, IPullRequest, IRemoteRepository, etc.
│   │
│   ├── Wheelhouse.Hosting.GitHub/
│   │   Octokit-based implementation of IHostingProvider
│   │
│   ├── Wheelhouse.Hosting.AzureDevOps/
│   │   TFS client-based implementation of IHostingProvider
│   │
│   ├── Wheelhouse.Terminal/
│   │   ConPTY process management, xterm.js bridge, shell detection
│   │
│   └── Wheelhouse.UI/
│       WPF application: App.xaml, MainWindow, Views, ViewModels, Themes
│
└── tests/
    ├── Wheelhouse.Core.Tests/
    ├── Wheelhouse.Hosting.GitHub.Tests/
    ├── Wheelhouse.Hosting.AzureDevOps.Tests/
    └── Wheelhouse.Terminal.Tests/
```

### Architectural Patterns

- **MVVM** throughout the UI layer (`CommunityToolkit.Mvvm` source generators for minimal boilerplate)
- **Dependency Injection** wired up in `App.xaml.cs` via `Microsoft.Extensions.Hosting`
- **Repository pattern** in `Wheelhouse.Core` — `IRepositoryService` abstracts all LibGit2Sharp calls so ViewModels never touch LibGit2Sharp directly
- **Mediator / message bus** (`CommunityToolkit.Mvvm`'s `WeakReferenceMessenger`) for cross-ViewModel communication (e.g., "commit was made" → refresh log + working tree simultaneously)
- **Hosting provider plugin pattern** — each `IHostingProvider` implementation is registered via DI and discovered at startup; adding a new provider (GitLab, Bitbucket) requires only a new project implementing the abstractions

### Hosting Provider Abstraction

```csharp
// Wheelhouse.Hosting.Abstractions
public interface IHostingProvider
{
    string Id { get; }              // "github", "azuredevops"
    string DisplayName { get; }
    Uri BaseUri { get; }

    Task<bool> AuthenticateAsync(CancellationToken ct);
    Task<bool> IsAuthenticatedAsync(CancellationToken ct);
    Task SignOutAsync(CancellationToken ct);

    Task<IEnumerable<IRemoteRepository>> GetRepositoriesAsync(CancellationToken ct);
    Task<IEnumerable<IPullRequest>> GetPullRequestsAsync(string repoUrl, CancellationToken ct);
    Task<IPullRequest> CreatePullRequestAsync(CreatePullRequestOptions options, CancellationToken ct);
    Task<IPullRequest> GetPullRequestAsync(string repoUrl, int number, CancellationToken ct);
    Task MergePullRequestAsync(string repoUrl, int number, MergeMethod method, CancellationToken ct);
    Task<IEnumerable<ICheckRun>> GetCheckRunsAsync(string repoUrl, string commitSha, CancellationToken ct);
    Task<IEnumerable<IReviewComment>> GetReviewCommentsAsync(string repoUrl, int prNumber, CancellationToken ct);
    Task AddReviewCommentAsync(string repoUrl, int prNumber, AddCommentOptions options, CancellationToken ct);
}
```

### Settings Storage

- User settings persisted as JSON to `%APPDATA%\Wheelhouse\settings.json`
- Repository-specific settings (e.g., associated hosting provider, preferred remote) stored in `%APPDATA%\Wheelhouse\repos\{repo-hash}.json`
- Credentials stored in **Windows Credential Manager** (never in flat files)
- Settings access via `ISettingsService` (injectable, mockable for tests)

---

## 4. UI Design Philosophy

### Single-Window Tabbed Interface

The core design departure from SmartGit: **everything happens in one window**. No dialogs spawn separate top-level windows. No "open in Log" button that launches a second app instance.

### Main Window Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│  File   View   Repository   Branch   Commit   Remote   Tools   Help  │
├──────────────────────────────────────────────────────────────────────┤
│  [▶ Fetch] [⇓ Pull] [⇑ Push] [⎇ Branch] [✎ Stash] [⋯ Flow]  ...   │
├─────────────────────┬────────────────────────────────────────────────┤
│  REPOSITORIES       │  [ Working Tree ]  [ Log ]  [ Blame ]  [ + ]  │
│  ─────────────────  ├────────────────────────────────────────────────┤
│  ▼ MyRepo    ●      │                                                │
│    ⎇ Branches       │         Main Content Panel                    │
│      ▶ local        │         (content changes per selected tab)     │
│        main         │                                                │
│        feature/x    │                                                │
│      ▶ remote       │                                                │
│    ◈ Tags           ├────────────────────────────────────────────────┤
│    ⊞ Stashes        │  Diff / Detail Panel                           │
│    ◫ Submodules     │  (diff of selected file or commit)             │
│    ⊡ Worktrees      │                                                │
├─────────────────────┴────────────────────────────────────────────────┤
│  ▼  TERMINAL  [PowerShell ×]  [+ New Tab]                [_ □ ×]    │
│  PS C:\repos\MyRepo>  _                                              │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│  main  ↑2 ↓0  ●  3 modified, 1 staged         MyRepo   Ready        │
└──────────────────────────────────────────────────────────────────────┘
```

### Tab Types

| Tab | Content |
|---|---|
| **Working Tree** | Staged / unstaged file list + commit message box |
| **Log** | Commit graph + commit details panel |
| **Blame** | Annotated file view (opens per-file) |
| **Pull Requests** | PR list + inline review (opens when hosting is connected) |
| **Reflog** | Full reflog viewer |
| Additional tabs can be opened contextually (e.g., File History, Bisect wizard) |

### Multiple Repositories

- The left sidebar supports multiple open repositories (listed at the top of the panel)
- Switching repositories instantly swaps all tab content to that repo's state
- Repositories can be pinned or grouped into workspaces (Phase 6)

### Terminal Pane

- Docked to the bottom of the window, collapsed by default
- Toggle with **Ctrl+`** (VS Code convention)
- Supports multiple shell tabs within the pane
- Working directory auto-follows the active repository

### Theme System

- Light and dark themes implemented as WPF `ResourceDictionary` files
- A `ThemeService` switches the active dictionary at runtime (no restart required)
- Color tokens follow Fluent Design naming conventions (`Background`, `Surface`, `OnSurface`, `Primary`, `PrimaryVariant`, etc.)
- System theme detection via `UISettings.ColorValuesChanged` (Windows 10+) with optional override in preferences

---

## 5. Phased Roadmap

---

### Phase 1: Foundation & App Shell

**Goal:** Buildable, runnable skeleton that can open a repository and perform basic commits. Establishes all architectural patterns before feature work begins.

#### Infrastructure

- [ ] Create solution with all projects (`Core`, `Hosting.Abstractions`, `Hosting.GitHub`, `Hosting.AzureDevOps`, `Terminal`, `UI`, and all test projects)
- [ ] Configure `Directory.Build.props` with shared TFM, nullable, implicit usings, and version
- [ ] Set up `Microsoft.Extensions.Hosting` app host in `App.xaml.cs`; wire DI container
- [ ] Establish MVVM base classes and `WeakReferenceMessenger` message catalog
- [ ] Settings service (read/write JSON to `%APPDATA%\Wheelhouse\settings.json`)
- [ ] Logging to `%APPDATA%\Wheelhouse\logs\` via `Microsoft.Extensions.Logging`
- [ ] Exception handling / crash dialog (unhandled exception → friendly error window with log path)

#### UI Shell

- [ ] `MainWindow` with dockable panel layout (repository sidebar, content area, diff panel, terminal pane)
- [ ] Tab system for content area (Working Tree, Log tabs as placeholders)
- [ ] Repository sidebar scaffold (tree with Branches, Tags, Stashes, Submodules, Worktrees nodes)
- [ ] Status bar (branch name, ahead/behind counts, dirty indicator, operation progress)
- [ ] Main menu structure (all items present, most greyed out initially)
- [ ] Toolbar scaffold
- [ ] Light theme resource dictionary
- [ ] Dark theme resource dictionary
- [ ] `ThemeService` with runtime switching; theme preference persisted to settings
- [ ] System theme auto-detection (honor Windows light/dark mode by default)

#### Repository Management

- [ ] Start / Welcome screen (recent repositories list, Open, Clone, Init buttons)
- [ ] Open existing repository (folder browser → validate `.git` present)
- [ ] Initialize new repository
- [ ] Clone repository (URL + destination path; progress reporting)
- [ ] Recent repositories list (persisted in settings; pin/remove support)
- [ ] Repository added → auto-populate sidebar branch tree

#### Core Git Layer (Wheelhouse.Core)

- [ ] `IRepositoryService` interface + `LibGit2SharpRepositoryService` implementation
- [ ] Repository open / close lifecycle
- [ ] Read HEAD, current branch, upstream tracking info
- [ ] List local branches, remote branches, tags
- [ ] List working tree status (staged, unstaged, untracked, conflicted files per file)
- [ ] Read file diff (staged diff, unstaged diff, diff between two commits)
- [ ] Stage file, unstage file, stage all, unstage all
- [ ] Commit (with message; amend last commit)
- [ ] Fetch (all remotes, specific remote)
- [ ] Pull (merge or rebase strategy)
- [ ] Push (to tracking remote; force option)

#### Working Tree View

- [ ] Two-pane file list: Staged Changes (top) + Unstaged Changes (bottom)
- [ ] File status icons (modified, added, deleted, renamed, conflicted, untracked)
- [ ] Click file → diff appears in the Diff panel
- [ ] Stage / unstage individual files via toolbar button or right-click
- [ ] Stage All / Unstage All buttons
- [ ] Commit message box with character count
- [ ] Commit button (disabled when message is empty or nothing staged)
- [ ] Amend toggle checkbox
- [ ] Author override (name + email) per commit

#### Basic Diff Viewer

- [ ] Side-by-side and unified diff toggle
- [ ] Added/removed line highlighting (green/red)
- [ ] Context lines
- [ ] Binary file indicator
- [ ] Large file warning (>1MB diff truncated with option to view anyway)

#### Basic Log View

- [ ] Flat commit list (no graph yet) with author, date, message, SHA
- [ ] Click commit → show commit details (message, author, date, changed files, stats)
- [ ] Click file in commit → show diff for that file in that commit

---

### Phase 2: Core Git Workflows

**Goal:** Daily-driver quality. A developer could use Wheelhouse as their primary Git client after this phase.

#### Commit Graph

- [ ] Custom WPF `Canvas`-based graph renderer
- [ ] Branch lanes with color assignment (consistent color per branch lifetime)
- [ ] Merge commit connectors
- [ ] HEAD indicator, current branch highlight
- [ ] Virtualized rendering (only render visible rows — must handle repos with 100k+ commits without lag)
- [ ] Graph column sorted: local branches, remote branches, tags in parallel lanes
- [ ] Tooltip on graph node showing refs that point to that commit

#### Diff Viewer Upgrade

- [ ] Integrate `AvalonEdit` for syntax-highlighted diff display
- [ ] Language detection by file extension → apply syntax highlighting to diff context
- [ ] **Hunk-level staging** — stage/unstage individual diff hunks without staging the whole file
- [ ] **Line-level staging** — select specific lines within a hunk to stage
- [ ] Collapse/expand individual hunks
- [ ] Ignore whitespace toggle
- [ ] Word-level diff highlighting within changed lines

#### Branch Management

- [ ] Create branch (from HEAD, from commit, from another branch)
- [ ] Checkout branch (with dirty working tree warning + stash option)
- [ ] Delete branch (local; with unmerged check + force option)
- [ ] Delete remote branch
- [ ] Rename branch
- [ ] Set upstream tracking branch
- [ ] Compare branches (ahead/behind graph + diff)
- [ ] Right-click branch in sidebar → context menu with all branch operations

#### Tag Management

- [ ] Create lightweight tag
- [ ] Create annotated tag (message, GPG sign option placeholder)
- [ ] Delete local tag
- [ ] Delete remote tag
- [ ] Push tag(s) to remote
- [ ] Fetch tags

#### Remote Management

- [ ] Add remote
- [ ] Remove remote
- [ ] Rename remote
- [ ] Edit remote URL
- [ ] Prune stale remote-tracking branches

#### Stash

- [ ] Stash with optional message
- [ ] Stash options: include untracked, include ignored
- [ ] Apply stash (keep or drop after applying)
- [ ] Pop stash
- [ ] Drop stash
- [ ] View stash diff
- [ ] Stash list in sidebar with timestamps and messages

#### Merge

- [ ] Merge branch into current (fast-forward, no-fast-forward, squash, no-commit options)
- [ ] Merge commit message editor
- [ ] Abort merge
- [ ] Post-merge conflict detection → prompt to open Conflict Resolver (Phase 3)

#### Rebase

- [ ] Rebase current branch onto another branch/commit
- [ ] Rebase options (preserve merges, autosquash)
- [ ] Abort rebase
- [ ] Continue rebase (after manually resolving conflicts)
- [ ] Skip commit during rebase

#### Reset

- [ ] Reset current branch: soft, mixed, hard (to HEAD, to specific commit)
- [ ] Confirmation dialog for hard reset with diff summary of what will be lost

#### Revert

- [ ] Revert single commit (creates a new revert commit)
- [ ] Revert commit range
- [ ] Revert with no-commit option (stages changes without committing)

#### Cherry-Pick

- [ ] Cherry-pick single commit (from log right-click)
- [ ] Cherry-pick range of commits
- [ ] Cherry-pick with no-commit option
- [ ] Abort cherry-pick

---

### Phase 3: Advanced Git Operations

**Goal:** Power-user feature completeness. Covers everything a senior developer needs for complex repo management.

#### Interactive Rebase UI

- [ ] Launch from right-click on a commit in the Log view ("Rebase interactively from here...")
- [ ] Visual list of commits in the rebase sequence
- [ ] Drag-and-drop reorder commits
- [ ] Per-commit action selector: `pick`, `reword`, `edit`, `squash`, `fixup`, `drop`, `exec`
- [ ] Inline commit message editor for `reword`
- [ ] Squash/fixup message preview (shows combined message)
- [ ] Validate: detect commits that would have merge conflicts (warn but allow)
- [ ] Continue / abort / skip during mid-rebase conflicts

#### 3-Way Merge Conflict Resolver

- [ ] Opens as a tab within the main window (not a separate window)
- [ ] Three-pane layout: Ours (left) | Base (center) | Theirs (right) → Result (bottom)
- [ ] Navigate between conflict markers (Ctrl+↑/↓)
- [ ] Accept ours / accept theirs / accept both (ours first or theirs first) per conflict block
- [ ] Manual edit of the result pane
- [ ] Syntax highlighting in all four panes (AvalonEdit)
- [ ] Save and mark resolved
- [ ] Progress indicator: "2 of 5 conflicts resolved"
- [ ] Open from Working Tree conflicted file right-click or automatically after failed merge/rebase/cherry-pick

#### Blame View

- [ ] Opens as a tab (from file right-click → "Blame")
- [ ] File content annotated per line with: commit SHA (abbreviated), author, relative date
- [ ] Heat map coloring by commit age
- [ ] Click blame annotation → jump to that commit in the Log
- [ ] Follow renames across file history
- [ ] Navigate to parent commit's blame ("blame parent" for selected line)

#### File History

- [ ] Opens as a tab (from file right-click → "File History")
- [ ] Log filtered to commits that touched the selected file
- [ ] Follow renames (shows history through rename events)
- [ ] Click commit → diff for that file in that commit shown below
- [ ] Compare two commits' versions of the file side-by-side

#### Reflog Viewer

- [ ] Opens as a tab (Repository menu → "Reflog")
- [ ] Full reflog list with action, message, SHA, relative date
- [ ] Filter by branch/ref
- [ ] Right-click entry → checkout, reset to, cherry-pick
- [ ] Search reflog entries

#### Worktrees

- [ ] List linked worktrees in sidebar
- [ ] Add worktree (path + branch selection or new branch)
- [ ] Remove worktree (with prune option)
- [ ] Lock / unlock worktree
- [ ] Open worktree in new Wheelhouse tab or new window (user preference)
- [ ] Prune stale worktree metadata

#### Git Flow

- [ ] **Initialize Git Flow** — configure branch naming (`main`/`develop`/`feature/`/`release/`/`hotfix/`/`support/`) with defaults editable
- [ ] Flow status indicator in status bar (current flow branch type)
- [ ] **Feature branches:** Start feature (creates `feature/<name>` from develop), Finish feature (merges back, deletes branch), Publish feature, Track remote feature
- [ ] **Release branches:** Start release (creates `release/<version>` from develop), Finish release (merges to main + develop, creates tag), Publish, Track
- [ ] **Hotfix branches:** Start hotfix (from main), Finish hotfix (merges to main + develop, tags main)
- [ ] **Support branches:** Start support (from a tag on main), long-lived maintenance branch
- [ ] Git Flow config stored in `.gitflow` or `.git/config` (compatible with `gitflow` CLI)
- [ ] Git Flow toolbar section (or dedicated panel) with quick-action buttons when in a flow repo

#### Bisect

- [ ] **Start bisect wizard** — opens a guided side-panel tab
- [ ] Mark current commit as "bad"
- [ ] Mark a known-good commit (from log picker or SHA input)
- [ ] Git automatically checks out the midpoint commit
- [ ] "Mark as Good" / "Mark as Bad" buttons presented prominently
- [ ] Progress indicator: "~4 steps remaining"
- [ ] Log view highlights bisect range (good/bad/untested commits color-coded)
- [ ] Run automated bisect command (specify a test command that exits 0/1)
- [ ] Bisect result: highlights the first bad commit, shows summary
- [ ] Reset / abort bisect

#### Sparse Checkout

- [ ] View current sparse checkout cone/patterns
- [ ] Add / remove paths from sparse checkout
- [ ] Switch between cone mode and no-cone mode
- [ ] Warning if repo is not using sparse checkout (offer to enable)

---

### Phase 4: Hosting Integrations

**Goal:** GitHub and Azure DevOps workflows fully integrated — PR creation, review, CI status — without leaving Wheelhouse.

#### Authentication & Credential Management

- [ ] `IHostingProvider` abstraction fully implemented (see Architecture section)
- [ ] **GitHub authentication:** OAuth device flow (no client secret needed) + Personal Access Token fallback
- [ ] **Azure DevOps authentication:** Personal Access Token + Azure AD (MSAL) OAuth flow
- [ ] All tokens stored in **Windows Credential Manager** (never in flat files)
- [ ] Account management UI: add/remove/switch accounts, show token expiry
- [ ] Per-repository provider association (auto-detected from remote URL; manually overridable)

#### GitHub Integration

- [ ] **Clone from GitHub** — browse authenticated user's repos + orgs; search by name; includes forks
- [ ] **PR list** — "Pull Requests" tab per repo; filter by open/closed/draft, reviewer, label
- [ ] **Create PR** — branch, base, title, description (Markdown editor), reviewers, labels, draft toggle
- [ ] **PR review view** — diff per file with inline comment threads; approve / request changes / comment
- [ ] **Add review comment** — line-level comments with thread reply support
- [ ] **Merge PR** — merge commit, squash, or rebase merge strategy
- [ ] **CI / Check status** — per-commit status indicators in the Log (✓ / ✗ / ⏳); click to expand check details
- [ ] **Issue linking** — `#123` in commit messages rendered as clickable links to GitHub issues
- [ ] **Fork detection** — show upstream remote; offer "Sync fork" shortcut
- [ ] **Notifications** — badge on PRs tab for new review requests / mentions (polling, no webhook needed)

#### Azure DevOps Integration

- [ ] **Clone from AzDO** — browse organizations → projects → repositories
- [ ] **PR list** — list active PRs for the repo; filter by status, reviewer, author
- [ ] **Create PR** — title, description, reviewers, work item links, auto-complete option
- [ ] **PR review view** — file diff with inline comment threads; vote (approve, approve with suggestions, wait for author, reject)
- [ ] **Complete PR** — merge strategies: merge commit, squash, rebase + fast forward, semi-linear merge; delete branch after option
- [ ] **Build/pipeline status** — per-commit build status badges in Log view; click to open pipeline run detail (in-app web view or browser)
- [ ] **Work item linking** — `#1234` in commit messages/PR descriptions linked to AzDO work items
- [ ] **Branch policies** — display active policies on a branch (required reviewers, build validation, etc.); warn when PR would violate a policy

---

### Phase 5: Advanced Features

**Goal:** The features that make Wheelhouse a serious tool — terminal, submodules, LFS, SSH management.

#### Integrated Terminal

- [ ] Terminal pane implemented with **WebView2 + xterm.js + ConPTY**
- [ ] Toggle with **Ctrl+`**; resizable pane height; remembers last height per session
- [ ] Multiple terminal tabs within the pane (`+` button to add, click `×` to close)
- [ ] Shell selector per tab: PowerShell 7, Windows PowerShell 5.1, Command Prompt, Git Bash, WSL distros (auto-detected from system)
- [ ] Working directory auto-set to active repository root when opening a new tab
- [ ] "Open Terminal Here" context menu item on folders in the file tree
- [ ] Font size and font family configurable in settings (monospace fonts only)
- [ ] Full ANSI/VT100 support (colors, cursor movement, mouse events)
- [ ] Copy/paste with Ctrl+C / Ctrl+V (Ctrl+C for interrupt uses Ctrl+Shift+C for copy, same as Windows Terminal)
- [ ] Terminal profile management (save named profiles with shell + args + env vars)

#### Submodules

- [ ] Submodule nodes in repository sidebar (expandable, showing each submodule's status)
- [ ] Submodule status indicators: clean, modified, uninitialized, out of sync
- [ ] **Initialize** submodule(s)
- [ ] **Update** submodule(s) (checkout tracked commit; `--remote` option to pull latest)
- [ ] **Clone with submodules** option when cloning
- [ ] **Open submodule** as a new repository tab (full Wheelhouse UI for the submodule)
- [ ] **Add submodule** (URL + path)
- [ ] **Remove submodule** (handles `.gitmodules`, `.git/config`, directory cleanup)
- [ ] **Sync** submodule remote URLs
- [ ] `foreach` operation: run a Git command across all submodules

#### Git LFS

- [ ] LFS status indicators in Working Tree file list (📦 icon for LFS-tracked files)
- [ ] LFS pointer vs. actual content detection (warn if LFS is not installed/initialized)
- [ ] **Track patterns** — add glob patterns to `.gitattributes` for LFS tracking
- [ ] **Untrack patterns** — remove from LFS tracking
- [ ] **Fetch LFS objects** for a range of commits
- [ ] **Prune LFS cache** (clean unreferenced local LFS objects)
- [ ] **LFS Locks** — view locked files, acquire lock, release lock (requires LFS server support)
- [ ] LFS storage usage display in repository info panel
- [ ] Install LFS hooks (`git lfs install`) if not present when opening a LFS repo

#### SSH Key Management

- [ ] SSH key list (reads from `~/.ssh/` by convention + custom paths)
- [ ] Generate new Ed25519 or RSA key pair (name, passphrase optional)
- [ ] Import existing private key
- [ ] Display public key with one-click copy (for pasting into GitHub/AzDO)
- [ ] Add key to SSH agent (Windows OpenSSH agent integration)
- [ ] Remove key from agent
- [ ] Test SSH connection to a host (`ssh -T git@github.com`)
- [ ] Known hosts viewer (`~/.ssh/known_hosts`)

#### GPG Commit Signing

- [ ] Detect installed GPG keys (call `gpg --list-secret-keys`)
- [ ] Configure signing key per repository or globally
- [ ] Sign commits by default toggle (per repo + global settings)
- [ ] Signature verification indicators in Log view (✓ Verified, ✗ Bad signature, ? Unverified)
- [ ] Tooltip showing signing key fingerprint and trust level

#### Advanced Log & Search

- [ ] **Filter toolbar** in Log view: filter by author, date range, file path, commit message regex, branch
- [ ] **Search commits** (Ctrl+F in Log): full text search across commit messages
- [ ] **Search file content at commit** (find when a string was introduced or removed)
- [ ] Log view column customization (show/hide: graph, SHA, author, date, refs, message)
- [ ] Save named log filters (e.g., "My commits this week")
- [ ] Highlight commits matching a filter without hiding others

---

### Phase 6: Polish & Distribution

**Goal:** Production-quality release ready for public consumption.

#### Performance

- [ ] Profile and optimize graph rendering for repositories with >100k commits
- [ ] Virtualize all long lists (branches, tags, files) — only render visible rows
- [ ] Background threading for all Git operations (no UI freezes); progress shown in status bar
- [ ] Incremental repository status refresh (inotify-style file watcher via `FileSystemWatcher`)
- [ ] Repository index caching (avoid re-reading full index on every refresh)

#### Keyboard Shortcuts

- [ ] Comprehensive default shortcut map (document all shortcuts)
- [ ] Fully remappable via settings JSON
- [ ] Shortcut cheat sheet overlay (Ctrl+? to open)
- [ ] Vim-style navigation mode option for Log and file lists (j/k/gg/G)

#### Accessibility

- [ ] Full keyboard navigation (every action reachable without a mouse)
- [ ] Screen reader support (AutomationProperties on all controls)
- [ ] High contrast theme (respects Windows High Contrast mode)
- [ ] Minimum touch target sizes for trackpad users

#### Portable Mode

- [ ] `--portable` launch flag: store settings and credentials in `<exe-dir>\data\` instead of `%APPDATA%`
- [ ] Credentials in portable mode stored in an encrypted local file (DPAPI) rather than Windows Credential Manager
- [ ] No registry writes in portable mode
- [ ] ZIP distribution (no installer) for portable mode

#### Auto-Update

- [ ] Check for new GitHub Releases on startup (with opt-out setting)
- [ ] Show update notification in status bar / notification area
- [ ] Download and apply MSIX update package in background
- [ ] Release notes shown in an in-app dialog before applying update

#### Localization

- [ ] All UI strings extracted to `.resx` resource files (English baseline)
- [ ] Localization framework in place so community contributors can add languages
- [ ] RTL layout support scaffolding

#### Telemetry (Opt-In)

- [ ] Anonymous crash reports (structured exception + stack trace; no code content, no file paths)
- [ ] Optional anonymous feature usage telemetry (which features are used; no repo data)
- [ ] Prominent opt-in dialog on first launch; clearly opt-in, never opt-out

#### Distribution

- [ ] **GitHub Releases** — MSIX package + portable ZIP attached to each release tag
- [ ] **MSIX package** — signed with a code signing certificate; includes auto-update support
- [ ] **WinGet submission** — `Wheelhouse.Wheelhouse` package ID in the winget-pkgs repo
- [ ] **Project website** — landing page with screenshots, feature list, download button (links to latest GitHub Release); hosted on GitHub Pages initially
- [ ] **Chocolatey** (stretch goal) — community Chocolatey package

---

## 6. Key Technical Challenges

### Commit Graph Rendering

The branch graph is the hardest UI piece to get right. Key considerations:

- **Lane assignment algorithm**: each branch gets a lane; merge commits must connect their parent lanes with connector lines; the algorithm must be stable (branches don't jump lanes as you scroll)
- **Virtualization**: only compute and render the visible viewport rows — a 200k commit repo must scroll at 60fps
- **Recommended approach**: compute the full lane assignment once on load (or incrementally as new commits are fetched), store as a lightweight data structure (array of `(laneIndex, connectors[])` per commit), and render with a `DrawingContext` on a virtualized `Canvas`
- Reference implementations to study: GitExtensions, gitg, and the open-source `git-graph` algorithm papers

### 3-Way Merge Tool

- The result pane must allow free editing while still tracking which conflict blocks are resolved
- Conflict blocks need to be tracked as range-overlaid markers in AvalonEdit, not embedded in the text itself
- Saving must write the actual merged content (not the conflict markers) to disk and stage the file

### Terminal Emulator (ConPTY + WebView2)

- ConPTY requires Windows 10 1903+ and is a Win32 API — needs P/Invoke interop
- The WebView2 host must relay I/O between the xterm.js terminal and the ConPTY process with minimal latency
- Resizing the terminal must call `ResizePseudoConsole` and post a resize event to xterm.js simultaneously
- Shell detection must handle WSL distribution enumeration (registry + `wsl --list`)

### Git LFS

- LibGit2Sharp does not natively support LFS — LFS operations must shell out to `git lfs` commands
- LFS pointer file detection can be done by checking for the `version https://git-lfs.github.com/spec/v1` header
- LFS must be detected as installed at startup; graceful degradation if not installed

### Large Repository Performance

- LibGit2Sharp holds a native handle; repositories must be properly disposed and not held open across long idle periods
- `FileSystemWatcher` for repository change detection must be debounced aggressively (50ms minimum) to avoid flooding on large `git checkout` operations
- The commit graph lane assignment should be computed on a background thread and streamed to the UI as it completes

---

## 7. CI/CD & Distribution

### GitHub Actions Pipelines

#### `build.yml` — Triggered on every push and PR

```
Steps:
1. Checkout
2. Setup .NET 10
3. Restore NuGet packages
4. Build (Release config)
5. Run all unit tests (xUnit)
6. Upload test results artifact
```

#### `package.yml` — Triggered on version tag push (`v*.*.*`)

```
Steps:
1. Checkout
2. Setup .NET 10
3. Restore
4. Build Release
5. Publish self-contained win-x64 executable
6. Create MSIX package (using MSIX Packaging Tool or MSBuild SDK)
7. Sign MSIX (with cert stored in GitHub Secrets)
8. Create portable ZIP
9. Create GitHub Release with MSIX + ZIP as assets
10. Trigger WinGet PR (using winget-releaser action)
```

#### `codeql.yml` — Weekly security scan

```
Steps:
1. CodeQL analysis (C#)
2. Upload SARIF results to GitHub Security tab
```

### Versioning

- Semantic versioning: `MAJOR.MINOR.PATCH`
- Version embedded in `Directory.Build.props` as `<Version>`
- Git tag `v1.2.3` triggers the package pipeline

### MSIX

- MSIX package identity: `Wheelhouse.Wheelhouse`
- Installer UI: minimal (no wizard — MSIX installs silently)
- Package family name used for auto-update check

---

## 8. Future Contributor Guidelines

*(To be expanded when the project goes public. Placeholder structure below.)*

### Getting Started

- Prerequisites: Visual Studio 2022 or JetBrains Rider, .NET 10 SDK, Git 2.40+
- Clone the repo, open `Wheelhouse.sln`, set `Wheelhouse.UI` as startup project, run

### Contribution Scope

- Bug reports and feature requests via GitHub Issues
- Pull requests welcome; all PRs require at least one approval and passing CI
- New hosting providers (GitLab, Bitbucket, etc.) are explicitly welcomed and follow the `IHostingProvider` pattern

### Code Style

- Follow the `.editorconfig` in the repo root
- MVVM strictly enforced — ViewModels must not take dependencies on WPF types
- No direct LibGit2Sharp usage outside `Wheelhouse.Core`
- All public APIs documented with XML doc comments

### Adding a New Hosting Provider

1. Create `Wheelhouse.Hosting.<ProviderName>` project
2. Implement `IHostingProvider` from `Wheelhouse.Hosting.Abstractions`
3. Register in `Wheelhouse.UI`'s DI setup
4. Add authentication flow to the Account Settings UI
5. Add integration tests against the provider's sandbox/test environment
