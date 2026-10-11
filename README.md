# Code Escape

Code Escape is a 2D/2.5D PC educational game that teaches beginner programming. The player controls Alex through six locations of a mystery story. Doors, locks and patrolling hazards block the way, and each one opens a short programming question that must be answered correctly to continue.

> Capstone project. Course and institution details: **[to be completed by the team]**

## Group members
Group number: **IT22**

- Jean Joseph C. Mallari
- Mark Daniel M. Haguisan
- Louis Angelo D. Junio
- Alynus Job C. Tombado

## Tools and technologies
- Unity 6000.5.10f1 (C#, Universal Render Pipeline, Input System, uGUI)
- Unity Gaming Services: Authentication (anonymous sign-in) and Cloud Save, via `com.unity.services.cloudsave` 3.4.1
- Unity PlayerPrefs for the local save
- Git and GitHub (GitHub Desktop)
## Features
- **Three languages:** Python, PHP and SQL. One is chosen per playthrough, with its own level-map progress.
- **Six levels:** Home, Maple Street, Warehouse 7, Riverside Tech Facility, Old Station and the Sentinel Control Room, played from a level map. Cleared levels can be replayed.
- **Questions:** 48 predefined questions (10 obstacle questions per language, plus 18 hazard-challenge questions). Types include multiple choice, predict the output, fill the missing code, fix the incorrect code and choose the correct code. Answers are compared with stored solutions; player code is never executed.
- **Hints and scoring:** +100 points per solved challenge, -25 for the first hint on a question, with a rank on the results screen.
- **Story systems:** cutscenes, dialogue, objectives, collectible clues with a journal, settings, and an SDG 4 information panel.
- **Progress saving:** a local save (PlayerPrefs) plus a **Unity Cloud Save** copy that loads on start. See [Documentation/CloudSave.md](Documentation/CloudSave.md).

## Controls
| Mode | Keys |
|---|---|
| Side view | A / D or Left / Right to walk, W / Up / Space to jump |

Menus can be used with the mouse or keyboard (Esc closes panels).

## Requirements
- **Unity 6000.5.10f1** (install through Unity Hub)
- Windows PC
- An **internet connection** to play, because progression is loaded from and saved to Unity Cloud Save before gameplay starts
- A Unity account with this project linked to a Unity Dashboard project (Cloud Save enabled): see the setup section in [Documentation/CloudSave.md](Documentation/CloudSave.md)

## Run the project
1. Clone the repository and add the folder in Unity Hub (use the exact editor version above).
2. Open the project. The first import can take a while.
3. Open `Assets/Scenes/00_MainMenu.unity` and press **Play**.
4. Wait for the "Connecting to Unity Cloud Save" screen to finish, then choose **Play Adventure**.

To build, use File > Build Profiles (all 12 scenes in `Assets/Scenes` are already in Build Settings).

## Project structure
| Path | Contents |
|---|---|
| `Assets/Scenes` | Main menu, language select, level map, intro, six level scenes, ending, results |
| `Assets/Scripts/Programming` | Challenge manager, question data and database, language selection |
| `Assets/Scripts/Obstacles` | Programming obstacles, catch hazards and their movement |
| `Assets/Scripts/Player` | Player controller and cameras |
| `Assets/Scripts/Game` | Game state, level and level-map progress, scene transitions, results, pause |
| `Assets/Scripts/SaveSystem` | Local save (SaveManager, ProgressSaveService, save data classes) |
| `Assets/Scripts/Cloud` | Unity Cloud Save integration and its on-screen status UI |
| `Assets/Scripts/UI`, `Story`, `Clues`, `Dialogue`, `Cutscenes`, `Audio` | Menus and HUD, story flow, clues, dialogue, cutscenes, audio |
| `Assets/ScriptableObjects/Questions` | The question assets (Python, PHP, SQL, hazards) |
| `Documentation` | Cloud Save notes and setup |

## Database and data
- **Storage used:** Unity Cloud Save (player data, key-value) through Unity Authentication anonymous sign-in, plus a local save in PlayerPrefs. No credentials are stored in the project; the Unity project is linked to a Unity Dashboard project.
- **Saved:** current level, highest unlocked level, completed levels, total score, achievements, last played time, and a snapshot used to restore the level map. Saved automatically when a level is cleared or unlocked, when score changes and when the main menu opens.
- **Retrieved and displayed:** loaded when the game starts, then shown on the level map, the Continue panel and the "Cloud progress" panel.
- **Details, field list and testing:** [Documentation/CloudSave.md](Documentation/CloudSave.md)

## Cloud Save setup for a new machine
No passwords, keys or service account files are needed or stored in this repository. After cloning:
1. Open the project in Unity 6000.5.10f1 and sign in with your Unity account.
2. Make sure the project is linked to a Unity Dashboard project (Edit > Project Settings > Services) with Cloud Save enabled. The link is kept in `ProjectSettings`.
3. Press Play. The game signs in anonymously and loads or creates the player's progress.

If Unity shows a Package Manager error, check that `Packages/manifest.json` lists each package only once. Full details: [Documentation/CloudSave.md](Documentation/CloudSave.md).
## Documentation
- [Cloud Save progression](Documentation/CloudSave.md): setup, data structure, how loading and saving work, testing and limitations

## Known limitations
- Online-only prototype.
- Anonymous sign-in has no account recovery or cross-device sync: a new device or cleared data starts with empty progress. A login and registration system is planned and will be implemented soon; it is not implemented yet.
- Progress and scores are written by the game client and are not cheat-proof.

## References
- Unity Cloud Save documentation: https://docs.unity.com/ugs/en-us/manual/cloud-save/manual
- Unity Authentication documentation: https://docs.unity.com/ugs/en-us/manual/authentication/manual/overview
- Unity Gaming Services overview: https://docs.unity.com/ugs/en-us/manual/overview/manual/unity-gaming-services-home

