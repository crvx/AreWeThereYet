# Are We There Yet?

A small KSP mod that shows what tourist contract tasks are still pending for Kerbals on the active vessel or in the crew roster.

Tired of checking each tourist to remember who still needs to go where? This mod lists all incomplete tourist tasks from active contracts in a single window — one click on the toolbar button, and you know exactly who still needs to fly by, orbit, or land where.

## How It Works

The mod finds all tourists on the active vessel (or all available tourists in KSC/VAB/SPH) and searches active contracts for their incomplete tasks. Tasks are displayed in a scrollable two-column window.

The toolbar button is available in **Flight**, **Map View**, **VAB**, **SPH**, and **Space Center** scenes. 

![Toolbar button](Images/toolbaricon.png)

Click it to open the "Are We There Yet?" window, click again to close. The task list updates automatically when contract parameters change.

![Window overview](Images/awtywindow.png)

Colored circles before each task indicate the destination body:
- One circle (●) for a planet or the Sun — the circle color matches the body's orbit
- Two circles (● ●) for a moon — the left circle is the parent planet, the right circle is the moon

Colors are defined in `Colors.cfg` and can be customized for any body.

A **Show completed** checkbox at the top of the window toggles display of completed tasks. Completed tasks are marked with a green checkmark (✓).

A **destination filter** dropdown lets you narrow the list to tasks for a specific planet or moon. Selecting a planet also shows tasks for all its moons.

![Destination filter dropdown](Images/dropdown.png)

## Installation

1. Download the latest release.
2. Extract the `GameData/AreWeThereYet` folder into your KSP `GameData/` directory.
3. The final structure should be:

```
GameData/AreWeThereYet/
├── Plugins/
│   └── AreWeThereYet.dll
├── Textures/
│   └── AreWeThereYet.png
├── Colors.cfg
├── LICENSE
└── README.md
```

## Building from Source

### Prerequisites

- .NET Framework SDK or Mono
- MSBuild
- A KSP installation with `KSP_Data/Managed/` containing the required assemblies

### Setup

1. Clone the repository:
   ```
   git clone https://github.com/crvx/AreWeThereYet.git
   cd AreWeThereYet
   ```

2. Create a symlink (Linux) or junction (Windows) named `KSP_Data` inside the `AreWeThereYet/` folder, pointing to your KSP installation's `KSP_Data` directory:

   **Linux:**
   ```bash
   ln -s /path/to/Kerbal\ Space\ Program/KSP_Data AreWeThereYet/KSP_Data
   ```

   **Windows (admin prompt):**
   ```cmd
   mklink /J AreWeThereYet\KSP_Data C:\Path\To\KSP\KSP_Data
   ```

   The `AreWeThereYet.csproj` references assemblies via `KSP_Data\Managed\*.dll`, so this symlink is required for compilation.

### Build

   ```bash
   msbuild AreWeThereYet/AreWeThereYet.csproj /p:Configuration=Release /t:Build
   ```

The compiled DLL will be at `AreWeThereYet/bin/Release/AreWeThereYet.dll`.

## License

MIT License. Copyright © crvx.
