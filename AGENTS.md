# Repository Guidelines

## Project Structure & Module Organization
The project targets Unity 2022.3.62f2 (see `ProjectSettings/ProjectVersion.txt`). Gameplay scripts live under `Assets/Scripts/` with folders for `Tank`, `Managers`, `Camera`, `UI`, and `Shell`; accompanying prefabs reside in `Assets/Prefabs/`, while shared materials, sprites, and audio clips sit in their respective top-level directories. Scenes ship in `Assets/Scenes/`, with `_Complete-Game.unity` acting as the reference level. `Assets/Editor/` holds utility editor scripts, and `Packages/manifest.json` locks the package versions that all contributors should share.

## Build, Test, and Development Commands
Launch the project locally with `/Applications/Unity/Hub/Editor/2022.3.62f2/Unity.app/Contents/MacOS/Unity -projectPath "$(pwd)"`; adjust the path if Unity is installed elsewhere. Produce a headless Windows build via the same binary with `-batchmode -buildWindows64Player Build/Windows/TankBattle.exe -quit`, changing the target directory per platform. Run automated checks using `-batchmode -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -quit`. During iteration, prefer the in-editor Play button and keep Unity-generated output under `Logs/` out of version control.

## Coding Style & Naming Conventions
Follow Unity’s C# house style: four-space indentation, `PascalCase` for classes and public members, and the existing `m_` prefix for private serialized fields (see `Assets/Scripts/Tank/TankMovement.cs`). Favour early `return` statements over deep nesting, keep methods under roughly 50 lines, and use `//` comments sparingly to clarify intent. Document public APIs and scriptable settings with XML comments when they are consumed outside the declaring class.

## Testing Guidelines
No first-party tests ship yet; add new Edit Mode suites under `Assets/Tests/EditMode` and Play Mode suites under `Assets/Tests/PlayMode`. Name test files after the component under test (e.g., `TankMovementTests.cs`) and use the Unity Test Framework attributes (`[UnityTest]` or `[Test]`) for coroutine behaviour and pure logic, respectively. Store XML results in `Logs/` and aim to cover movement, firing, scoring, and UI flows present in `_Complete-Game.unity`.

## Commit & Pull Request Guidelines
With no existing Git history to mirror, adopt Conventional Commits (`feat:`, `fix:`, `chore:`) and write imperative, present-tense subjects. Bundle related scene, prefab, and script edits together so reviewers can trace asset changes. Pull requests should outline the gameplay impact, link to relevant issue IDs, include clips or screenshots for visual adjustments, and list which automated tests or in-editor scenarios were exercised.
