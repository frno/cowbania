namespace Cowbania.Host.Tests.Input;

internal static class InputTests
{
    public static IEnumerable<TestCase> Cases
    {
        get
        {
            yield return new TestCase("title screen starts only on a fresh Enter press", () =>
            {
                var title = new TitleScreenState();
                            title.Update(true);
                            Assert(!title.HasStarted, "Enter already held at startup does not skip the title film");
                            title.Update(false);
                            title.Update(true);
                            Assert(title.HasStarted, "fresh Enter press starts gameplay");
                            title.Update(false);
                            Assert(title.HasStarted, "title completion remains latched");
            });
            yield return new TestCase("title film loops behind a centered opaque logo", () =>
            {
                var root = FindRepositoryRoot();
                            foreach (var name in new[] { "cowbania_title_loop.cwvf", "cowbania_title_pixel_loop.cwvf" })
                            {
                                var filmPath = Path.Combine(root, "Assets", "Video", name);
                                using var film = File.OpenRead(filmPath);
                                using var reader = new BinaryReader(film);
                                Assert(new string(reader.ReadChars(4)) == "CWVF", $"{name} uses the expected stream signature");
                                var width = reader.ReadInt32();
                                var height = reader.ReadInt32();
                                var fps = reader.ReadInt32();
                                var frames = reader.ReadInt32();
                                Assert((width, height, fps) == (512, 288, 4),
                                    $"{name} uses the authored 16:9 low-frame-rate contract");
                                Assert((double)frames / fps >= 120,
                                    $"{name} runs for at least two minutes before looping");
                            }

                            var titleSource = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "TitleScreenRenderer.cs");
                            var draw = MethodBody(titleSource, "Draw");
                            AssertInOrder(draw, "Color.Black", "film?.CurrentFrame", "Color.White * 0.46f", "TitleLogo", "Color.White");
                            Assert(draw.Contains("viewport.Width / 2 - logoWidth / 2", StringComparison.Ordinal) &&
                                   draw.Contains("viewport.Height / 2 - logoHeight / 2", StringComparison.Ordinal),
                                "opaque title logo is centered in front of the translucent film");

                            var playerSource = ReadSource(root, "src", "Cowbania.Host", "Presentation", "Rendering", "TitleFilmPlayer.cs");
                            Assert(playerSource.Contains("% DurationSeconds", StringComparison.Ordinal) &&
                                   playerSource.Contains("currentFrame?.Dispose()", StringComparison.Ordinal),
                                "title playback loops by elapsed time while retaining only one decoded frame");
                            var project = ReadSource(root, "src", "Cowbania.Host", "Cowbania.Host.csproj");
                            Assert(project.Contains("Assets\\Video\\*.cwvf", StringComparison.Ordinal) &&
                                   project.Contains("Assets\\Art\\Title\\*.png", StringComparison.Ordinal),
                                "runtime film and foreground logo are copied into packaged builds");
                            var previousSelection = Environment.GetEnvironmentVariable("COWBANIA_TITLE_FILM");
                            try
                            {
                                Environment.SetEnvironmentVariable("COWBANIA_TITLE_FILM", null);
                                Assert(CowbaniaGame.SelectedTitleFilm() == "cowbania_title_pixel_loop.cwvf",
                                    "the pixel-art alternate is the default title film");
                                Environment.SetEnvironmentVariable("COWBANIA_TITLE_FILM", "cinematic");
                                Assert(CowbaniaGame.SelectedTitleFilm() == "cowbania_title_loop.cwvf",
                                    "the cinematic title film remains selectable for comparison");
                            }
                            finally
                            {
                                Environment.SetEnvironmentVariable("COWBANIA_TITLE_FILM", previousSelection);
                            }
            });
            yield return new TestCase("reload-completion shot signal drives host feedback despite ammo increase", () =>
            {
                var game = new GameWorld();
                            for (var shot = 0; shot < 6; shot++)
                            {
                                game.Update(new InputFrame(
                                        0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false),
                                    shot == 0 ? 0f : GameWorld.FireDelay);
                            }

                            game.Update(new InputFrame(
                                    0, false, false, System.Numerics.Vector2.UnitX, true, false, false, false),
                                GameWorld.ReloadDuration - GameWorld.FireDelay);

                            Assert(game.Ammo == 5 && game.PlayerShotAcceptedThisUpdate,
                                "core exposes the accepted shot when reload completion raises ammo from empty to five");
                            Assert(PlayerAnimationRestart.ShouldReset(
                                    PresentationAnimationState.Shoot,
                                    PresentationAnimationState.Shoot,
                                    game.PlayerShotAcceptedThisUpdate),
                                "the host restarts Shoot from the explicit signal even when Shoot is already selected");
            });
        }
    }
}
