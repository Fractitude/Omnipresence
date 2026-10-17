# Code style

The formatter is the contract. We format C# and Razor with **Rider's default code style settings**,
plus the repo's `.editorconfig` — that combination is what produced the files in `Code/`, `Editor/`,
`Addons/` and `Libraries/navi/Code/` today. Reformat Code (Ctrl+Alt+L) on the files you touched
before committing, and don't argue about layout in review.

This page records what that produces, so hand-written code arrives already formatted. If code here
and this page ever disagree, the formatter wins: run it, then fix this page.

## Indentation

- **Tabs, 4 wide, one per level.** Never spaces for indentation (`.editorconfig`:
  `indent_style = tab`, `tab_size = 4`). Tabs mean every reader sees their own width and the files
  don't shift when someone reformats.
- **Continuation lines use spaces when they align to something**, one tab when they only nest:
  - a wrapped Razor attribute list aligns under the first attribute, with spaces, so it lines up
    whatever a tab is worth;
  - a wrapped C# parameter list, a LINQ chain, and a collection expression's items are indented one
    tab deeper than the line that opened them.
- Razor markup nests one tab per element, one more inside `@if` / `@foreach` bodies. The `@code`
  block is plain C# and follows the rules below.

## New lines

- **CRLF, final newline** (`.editorconfig`). The index stores LF and git converts on checkout, so
  don't hand-edit line endings; just don't mix them in one file.
- **Braces on their own line, always (Allman)** — types, methods, properties, control flow, lambdas
  with block bodies, object initializers. `else`, `catch` and `finally` get their own line too
  (`csharp_new_line_before_open_brace = all`).
- **One blank line between members.** Consecutive fields that belong together stay adjacent; a field
  with a doc comment is its own group, so it gets a blank line after it:

  ```csharp
  (Package Package, bool Official)[] storePackages = [];

  int storeTotal;

  /// <summary>The computer's version when the lists were last fetched.</summary>
  int? knownVersion;

  bool polling;
  RealTimeSince sincePoll;
  ```

- **No blank line just inside `{` or `}`.**
- **Single-statement bodies stay on one line** (or on the next line, indented, for an `await`); don't
  add braces to them:

  ```csharp
  if ( !child.HasClass( "list" ) ) continue;

  if ( remaining > 0 )
  	await Task.Delay( (int)(remaining * 1000) );
  ```
- **`new X { … }` gets one member per line**, trailing comma, `};`
  (`csharp_new_line_before_members_in_object_initializers = true`).
- **`x with { … }` gets joined onto one line when it fits**, and one member per line when it
  doesn't. Write it one per line and let the formatter decide:

  ```csharp
  Update( uiState => uiState with
  {
  	Refreshing = false, StoreStatus = status, StoreRows = BuildStoreRows(), StoreTotal = storeTotal,
  } );
  ```

## Where a line breaks, and how

The right margin is **120** (Rider's default). Treat it as a limit, not a target: don't hand-wrap a
100-character line. When a line does go over, it breaks like this:

- **C# call or signature:** after the `(`, arguments on their own line indented one tab, the closing
  paren on the last argument's line:

  ```csharp
  static AppRow BuildRow(
  	(string Id, string Name, string Description, string Icon, string Color, bool Installed, string Type) app )
  ```

- **LINQ chain:** one call per line, each new line starting with `.`, indented one tab:

  ```csharp
  return apps
  	.Select( x => (App: x, FromStore: storeAppIds.Contains( x.Id )) )
  	.Where( x => Matches( kind, x.App.Installed, x.App.Type, x.FromStore ) )
  	.Select( x => BuildRow( x.App ) )
  	.ToArray();
  ```

- **Collection expression after `=>` or `=`:** the `[` goes on the next line, items one per line
  indented one tab, the closing `];` back at the statement's level:

  ```csharp
  static (string Key, string Name)[] Kinds =>
  [
  	("installed", "Installed"),
  	("store", "Store"),
  ];
  ```

- **Razor tag over the margin:** attributes wrap one group per line, aligned under the first
  attribute with spaces; the closing `>` or `/>` stays on the last line:

  ```razor
  <TextEntry Icon="search" Value:bind=@SearchText Placeholder="Search apps, then press Enter"
             HasClearButton=@true MaxLength=@( 80 ) onsubmit=@( () => ViewModel.SubmitSearch() )/>
  ```

- **Razor element whose content doesn't fit:** the content moves to its own line one tab deeper, and
  the closing tag may share that line (`<i>refresh</i></div>`); the element's closing tag may also
  be the thing that wraps (`…<i>filter_list</i>` then `</div>`). Never split an attribute
  expression across lines.
- **Comments are never reflowed.** Wrap `///` prose by hand at 120. You'll find older doc comments a
  few characters over; new ones shouldn't be.
- A line sometimes ends a little over 120 because the only thing left to break is a lambda attribute
  (`@onclick=@( () => … )`). That's fine — breaking inside it reads worse than the overflow.

## Spaces

The rule comes from `.editorconfig`: spaces inside the parentheses of calls and control flow,
nowhere else.

| Where | Rule | Example |
|---|---|---|
| Call and declaration argument lists | space inside | `Rating( package )`, `Task.Delay( (int)(remaining * 1000) )` |
| Empty argument lists | none | `base.Tick()`, `new()` |
| Control flow | space inside | `if ( x )`, `foreach ( var x in y )`, `switch ( x )` |
| Grouping parens, casts, `nameof`, target-typed `new` | **no** space inside | `(await Navi.GetApps( app ))`, `(int)(remaining * 1000)`, `nameof(app)`, `new(this)` |
| Razor inline expression | space inside | `@( entry.Expanded ? "Less" : "More" )`, `@onclick=@( () => ViewModel.OpenStore() )` |
| Dots, commas, binary operators | as usual | `x.Y`, `a, b`, `a ?? b`, `x is not null` |
| Square brackets | none inside | `[0]`, `[]` |
| Self-closing tag | none before `/>` | `<img class="thumb" src=@entry.Thumb/>` |

The mixed rules are deliberate, not an oversight: `csharp_space_between_parentheses =
control_flow_statements` plus `csharp_space_between_method_call_parameter_list_parentheses = true`
means the spaced style stops at casts and grouping parens.

## Modifiers

- **Class members are `private` unless stated, and we don't state it.** No `private` on fields,
  methods or nested types — C# already makes them private and the whole codebase relies on that.
  `public` and `internal` are written, because those aren't the default.
- A panel's `@code` block is the same: `viewModel`, `ViewModel`, `UiState` and its helpers carry no
  modifier.
- `.editorconfig` sets `dotnet_style_require_accessibility_modifiers = omit_if_default` so the IDE
  agrees with the code. If Rider still underlines a member for a missing `private`, its own
  Code Style > C# > Modifiers setting is overriding the file — fix it there, don't add the modifier.

## Razor specifics

- Attributes in the order we write them: `class`, then `tooltip`/data attributes, then event
  handlers last (`@onclick`, `@onmousedown`, `onsubmit`).
- One element per line. Small content stays inline with its tag (`<div class="official"
  tooltip="Official"><i>verified</i></div>`) unless the line overflows.
- `@code` is C#: all of the above applies inside it.
- Self-closing tags are `/>` with no space before it; `<root>` and friends get no space before `>`.

## What the formatter changes about hand-written code

Worth knowing, because these are the differences you'll see in a diff after a Reformat Code:

| Hand-written | Formatted |
|---|---|
| `@(expr)` | `@( expr )` |
| `nameof( app )` | `nameof(app)` |
| `( type is "core" or "system" )` | `(type is "core" or "system")` |
| `new( this )` | `new(this)` |
| `src=@entry.Thumb />` | `src=@entry.Thumb/>` |
| `static Foo[] Items => [` | `static Foo[] Items =>` then `[` on the next line |
| one member per line in a short `with { }` | members joined onto one line |
| two fields under one doc comment | blank line after the documented field |

## Committing formatting

- **Format the files you touched, not the repo.** A reformat-only commit that sweeps up unrelated
  files makes the real change unreviewable.
- Don't commit `.idea/` and don't add per-project style overrides there — `.editorconfig` is the
  only source of truth, because it travels with the repo.
- If a file's only change is formatting, say so in the commit message ("Format X with the project's
  formatter") so it's skipped in review.
