namespace RP.Spectre.Shell
{
    using System;
    using RP.Game.Mechanics;
    using RP.Spectre.State;

    /// <summary>
    /// Spectre's actual menu trees (build brief S21.2), built from the engine's generic <see cref="Menu"/>
    /// model and wired to the <see cref="AppStateMachine"/>. The engine owns the navigation machinery; this
    /// owns the <i>content</i> — which rows exist and what each one does — so the menus are game data layered
    /// over reusable plumbing. Activating a row drives a legal state transition (the machine rejects illegal
    /// ones), and the settings submenu mutates the live <see cref="SpectreSettings"/>.
    /// </summary>
    public static class SpectreMenus
    {
        /// <summary>The main menu. "Continue" is disabled when there is no save to resume.</summary>
        public static Menu MainMenu(
            AppStateMachine app, SpectreSettings settings, bool hasSave,
            Action onNewGame, Action onContinue, Action onQuit)
        {
            var continueRow = new MenuItem("Continue", () =>
            {
                onContinue();
                app.TryTransitionTo(AppState.Playing);
            })
            { Enabled = hasSave };

            return new Menu("SPECTRE",
                new MenuItem("New Game", () =>
                {
                    onNewGame();
                    app.TryTransitionTo(AppState.Playing);
                }),
                continueRow,
                new MenuItem("Settings", SettingsMenu(settings)),
                new MenuItem("Quit", () =>
                {
                    app.TryTransitionTo(AppState.Exiting);
                    onQuit();
                }));
        }

        /// <summary>The in-game pause menu overlaid on the frozen world.</summary>
        public static Menu PauseMenu(AppStateMachine app, SpectreSettings settings, Action onReturnToMainMenu)
        {
            return new Menu("PAUSED",
                new MenuItem("Resume", () => app.TryTransitionTo(AppState.Playing)),
                new MenuItem("Settings", SettingsMenu(settings)),
                new MenuItem("Abandon to Main Menu", () =>
                {
                    if (app.TryTransitionTo(AppState.MainMenu)) onReturnToMainMenu();
                }));
        }

        /// <summary>The settings submenu — toggles/cycles that edit the live settings; saving them is the
        /// caller's job when the menu closes.</summary>
        public static Menu SettingsMenu(SpectreSettings settings)
        {
            return new Menu("SETTINGS",
                new MenuItem("Toggle fullscreen", () => settings.Fullscreen = !settings.Fullscreen),
                new MenuItem("Toggle V-Sync", () => settings.VSync = !settings.VSync),
                new MenuItem("Toggle invert-Y", () => settings.InvertY = !settings.InvertY),
                new MenuItem("Cycle difficulty", () => settings.Difficulty = NextDifficulty(settings.Difficulty)));
        }

        /// <summary>Cycles Story → Standard → Hard → Story (Custom falls back into the cycle at Standard).</summary>
        public static string NextDifficulty(string current) => current switch
        {
            "Story" => "Standard",
            "Standard" => "Hard",
            "Hard" => "Story",
            _ => "Standard",
        };
    }
}
