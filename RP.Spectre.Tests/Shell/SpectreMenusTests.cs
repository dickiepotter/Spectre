namespace RP.Spectre.Tests.Shell
{
    using System.Linq;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Mechanics;
    using RP.Spectre.Shell;
    using RP.Spectre.State;

    /// <summary>
    /// Spectre's menu trees (build brief S21.2): the rows exist, "Continue" reflects whether a save is
    /// present, and activating rows drives legal <see cref="AppStateMachine"/> transitions and edits the live
    /// settings.
    /// </summary>
    [TestClass]
    public sealed class SpectreMenusTests
    {
        [TestMethod]
        public void MainMenu_HasTheExpectedRows_AndDisablesContinueWithoutASave()
        {
            var app = new AppStateMachine(AppState.MainMenu);
            var settings = new SpectreSettings();

            Menu noSave = SpectreMenus.MainMenu(app, settings, hasSave: false, () => { }, () => { }, () => { });
            noSave.Items.Select(i => i.Label).Should().ContainInOrder("New Game", "Continue", "Settings", "Quit");
            noSave.Items[1].Enabled.Should().BeFalse(); // Continue disabled

            Menu withSave = SpectreMenus.MainMenu(app, settings, hasSave: true, () => { }, () => { }, () => { });
            withSave.Items[1].Enabled.Should().BeTrue();
        }

        [TestMethod]
        public void NewGame_StartsPlaying_AndRunsTheHook()
        {
            var app = new AppStateMachine(AppState.MainMenu);
            bool started = false;
            Menu main = SpectreMenus.MainMenu(app, new SpectreSettings(), false, () => started = true, () => { }, () => { });
            var nav = new MenuController(main);

            nav.Activate(); // "New Game" is the first enabled row
            started.Should().BeTrue();
            app.Current.Should().Be(AppState.Playing);
        }

        [TestMethod]
        public void Quit_TransitionsToExiting()
        {
            var app = new AppStateMachine(AppState.MainMenu);
            bool quit = false;
            Menu main = SpectreMenus.MainMenu(app, new SpectreSettings(), false, () => { }, () => { }, () => quit = true);
            var nav = new MenuController(main);

            // New Game (0) -> Settings (2, Continue disabled is skipped) -> Quit (3)
            nav.MoveUp(); // wrap up to the last row, "Quit"
            nav.Current.Selected!.Label.Should().Be("Quit");
            nav.Activate();

            app.Current.Should().Be(AppState.Exiting);
            quit.Should().BeTrue();
        }

        [TestMethod]
        public void PauseMenu_ResumeReturnsToPlaying()
        {
            var app = new AppStateMachine(AppState.Playing);
            app.TryTransitionTo(AppState.Paused);

            Menu pause = SpectreMenus.PauseMenu(app, new SpectreSettings(), () => { });
            var nav = new MenuController(pause);

            nav.Activate(); // "Resume"
            app.Current.Should().Be(AppState.Playing);
        }

        [TestMethod]
        public void Settings_CycleDifficulty_EditsTheLiveSettings()
        {
            var settings = new SpectreSettings { Difficulty = "Standard" };
            Menu menu = SpectreMenus.SettingsMenu(settings);
            var nav = new MenuController(menu);

            // Walk to "Cycle difficulty" and activate it once.
            while (nav.Current.Selected!.Label != "Cycle difficulty") nav.MoveDown();
            nav.Activate();

            settings.Difficulty.Should().Be("Hard"); // Standard -> Hard
        }

        [TestMethod]
        public void NextDifficulty_CyclesAndRecoversFromCustom()
        {
            SpectreMenus.NextDifficulty("Story").Should().Be("Standard");
            SpectreMenus.NextDifficulty("Standard").Should().Be("Hard");
            SpectreMenus.NextDifficulty("Hard").Should().Be("Story");
            SpectreMenus.NextDifficulty("Custom").Should().Be("Standard");
        }
    }
}
