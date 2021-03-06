#region Using Statements
using System; using ComponentBind;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;

using Lemma.Components;
using Lemma.Factories;
using Lemma.Util;
using System.Reflection;
using System.IO;
using System.Xml.Serialization;
using System.Threading;
#endregion

namespace Lemma
{
	public class GameMain : Main
	{
		public const string InitialMap = "start";

		public const string MenuMap = "..\\Menu\\menu";

		public class ExitException : Exception
		{
		}

		public const int ConfigVersion = 6;
		public const int MapVersion = 353;
		public const int Build = 353;

		private static Dictionary<string, string> maps = new Dictionary<string,string>
		{
			{ "start", "\\map apartment" },
			{ "rain", "\\map rain" },
			{ "dawn", "\\map dawn" },
			{ "monolith", "\\map monolith" },
			{ "forest", "\\map forest" },
			{ "valley", "\\map valley" },
		};

		public static Config.Lang[] Languages = new[] { Config.Lang.en, Config.Lang.ru };

		public class Config
		{
			public enum Lang { en, ru }
			public Property<Lang> Language = new Property<Lang>();
#if DEVELOPMENT
			public Property<bool> Fullscreen = new Property<bool> { Value = false };
#else
			public Property<bool> Fullscreen = new Property<bool> { Value = true };
#endif
			public Property<bool> Maximized = new Property<bool> { Value = false };
			public Property<Point> Origin = new Property<Point> { Value = new Point(50, 50) };
			public Property<Point> Size = new Property<Point> { Value = new Point(1280, 720) };
			public Property<Point> FullscreenResolution = new Property<Point> { Value = Point.Zero };
			public Property<float> MotionBlurAmount = new Property<float> { Value = 0.5f };
			public Property<float> Gamma = new Property<float> { Value = 1.0f };
			public Property<bool> EnableReflections = new Property<bool> { Value = true };
			public Property<bool> EnableBloom = new Property<bool> { Value = true };
			public Property<LightingManager.DynamicShadowSetting> DynamicShadows = new Property<LightingManager.DynamicShadowSetting> { Value = LightingManager.DynamicShadowSetting.High };
			public Property<bool> InvertMouseX = new Property<bool> { Value = false };
			public Property<bool> InvertMouseY = new Property<bool> { Value = false };
			public Property<float> MouseSensitivity = new Property<float> { Value = 1.0f };
			public Property<float> FieldOfView = new Property<float> { Value = MathHelper.ToRadians(80.0f) };
			public int Version;
			public string UUID;
			public Property<PCInput.PCInputBinding> Forward = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.W } };
			public Property<PCInput.PCInputBinding> Left = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.A } };
			public Property<PCInput.PCInputBinding> Right = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.D } };
			public Property<PCInput.PCInputBinding> Backward = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.S } };
			public Property<PCInput.PCInputBinding> Jump = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.Space, GamePadButton = Buttons.RightTrigger } };
			public Property<PCInput.PCInputBinding> Parkour = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.LeftShift, GamePadButton = Buttons.LeftTrigger } };
			public Property<PCInput.PCInputBinding> RollKick = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { MouseButton = PCInput.MouseButton.LeftMouseButton, GamePadButton = Buttons.RightStick } };
			public Property<PCInput.PCInputBinding> TogglePhone = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.Tab, GamePadButton = Buttons.Y } };
			public Property<PCInput.PCInputBinding> QuickSave = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.F5, GamePadButton = Buttons.Back } };
			public Property<PCInput.PCInputBinding> ToggleFullscreen = new Property<PCInput.PCInputBinding> { Value = new PCInput.PCInputBinding { Key = Keys.F11 } };
		}

		public class SaveInfo
		{
			public string MapFile;
			public int Version;
		}

		public bool CanSpawn = true;

		public Config Settings;
		private string settingsDirectory;
		private string saveDirectory;
		private string analyticsDirectory;
		private string settingsFile;

		private Property<Entity> player = new Property<Entity>();
		private Entity editor;
		private PCInput input;

		private bool loadingSavedGame;

		public Property<string> StartSpawnPoint = new Property<string>();

		public Command<Entity> PlayerSpawned = new Command<Entity>();

		private float respawnTimer = -1.0f;

		private bool saveAfterTakingScreenshot = false;

		private static Color highlightColor = new Color(0.0f, 0.175f, 0.35f);

		private DisplayModeCollection supportedDisplayModes;

		private const float startGamma = 10.0f;
		private static Vector3 startTint = new Vector3(2.0f);

		public const int RespawnMemoryLength = 200;
		public const float DefaultRespawnDistance = 0.0f;
		public const float DefaultRespawnInterval = 0.5f;
		public const float KilledRespawnDistance = 40.0f;
		public const float KilledRespawnInterval = 3.0f;

		public float RespawnDistance = DefaultRespawnDistance;
		public float RespawnInterval = DefaultRespawnInterval;

		private Vector3 lastPlayerPosition;

		private int displayModeIndex;

		private List<Property<PCInput.PCInputBinding>> bindings = new List<Property<PCInput.PCInputBinding>>();

		private ListContainer messages;

		private WaveBank musicWaveBank;
		public SoundBank MusicBank;

		public string Credits { get; private set; }

		public GameMain()
			: base()
		{
			this.graphics.PreparingDeviceSettings += delegate(object sender, PreparingDeviceSettingsEventArgs args)
			{
				this.supportedDisplayModes = args.GraphicsDeviceInformation.Adapter.SupportedDisplayModes;
				int i = 0;
				foreach (DisplayMode mode in this.supportedDisplayModes)
				{
					if (mode.Format == SurfaceFormat.Color && mode.Width == this.Settings.FullscreenResolution.Value.X && mode.Height == this.Settings.FullscreenResolution.Value.Y)
					{
						this.displayModeIndex = i;
						break;
					}
					i++;
				}
			};
#if DEVELOPMENT
			this.EditorEnabled.Value = true;
#else
			this.EditorEnabled.Value = false;
#endif
			this.settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lemma");
			if (!Directory.Exists(this.settingsDirectory))
				Directory.CreateDirectory(this.settingsDirectory);
			this.settingsFile = Path.Combine(this.settingsDirectory, "settings.xml");
			this.saveDirectory = Path.Combine(this.settingsDirectory, "saves");
			if (!Directory.Exists(this.saveDirectory))
				Directory.CreateDirectory(this.saveDirectory);
			this.analyticsDirectory = Path.Combine(this.settingsDirectory, "analytics");
			if (!Directory.Exists(this.analyticsDirectory))
				Directory.CreateDirectory(this.analyticsDirectory);

			try
			{
				// Attempt to load previous window state
				using (Stream stream = new FileStream(this.settingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
					this.Settings = (Config)new XmlSerializer(typeof(Config)).Deserialize(stream);
				if (this.Settings.Version != GameMain.ConfigVersion)
					throw new Exception();
			}
			catch (Exception) // File doesn't exist, there was a deserialization error, or we are on a new version. Use default window settings
			{
				this.Settings = new Config { Version = GameMain.ConfigVersion, };
			}

			if (this.Settings.UUID == null)
				Guid.NewGuid().ToString().Replace("-", string.Empty).Substring(0, 32);
			
			TextElement.BindableProperties.Add("Forward", this.Settings.Forward);
			TextElement.BindableProperties.Add("Left", this.Settings.Left);
			TextElement.BindableProperties.Add("Backward", this.Settings.Backward);
			TextElement.BindableProperties.Add("Right", this.Settings.Right);
			TextElement.BindableProperties.Add("Jump", this.Settings.Jump);
			TextElement.BindableProperties.Add("Parkour", this.Settings.Parkour);
			TextElement.BindableProperties.Add("RollKick", this.Settings.RollKick);
			TextElement.BindableProperties.Add("TogglePhone", this.Settings.TogglePhone);
			TextElement.BindableProperties.Add("QuickSave", this.Settings.QuickSave);
			TextElement.BindableProperties.Add("ToggleFullscreen", this.Settings.ToggleFullscreen);

			if (this.Settings.FullscreenResolution.Value.X == 0)
			{
				Microsoft.Xna.Framework.Graphics.DisplayMode display = Microsoft.Xna.Framework.Graphics.GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
				this.Settings.FullscreenResolution.Value = new Point(display.Width, display.Height);
			}

			// Restore window state
			if (this.Settings.Fullscreen)
				this.ResizeViewport(this.Settings.FullscreenResolution.Value.X, this.Settings.FullscreenResolution.Value.Y, true);
			else
				this.ResizeViewport(this.Settings.Size.Value.X, this.Settings.Size.Value.Y, false, false);
		}

		private const float messageFadeTime = 0.5f;
		private const float messageBackgroundOpacity = 0.75f;

		private Container buildMessage()
		{
			Container msgBackground = new Container();

			this.messages.Children.Add(msgBackground);

			msgBackground.Tint.Value = Color.Black;
			msgBackground.Opacity.Value = messageBackgroundOpacity;
			TextElement msg = new TextElement();
			msg.FontFile.Value = "Font";
			msg.WrapWidth.Value = 250.0f;
			msgBackground.Children.Add(msg);
			return msgBackground;
		}

		public Container ShowMessage(Entity entity, Func<string> text, params IProperty[] properties)
		{
			Container container = this.buildMessage();
			TextElement textElement = (TextElement)container.Children[0];
			textElement.Add(new Binding<string>(textElement.Text, text, properties));

			this.animateMessage(entity, container);

			return container;
		}

		private void animateMessage(Entity entity, Container container)
		{
			container.CheckLayout();
			Vector2 originalSize = container.Size;
			container.ResizeVertical.Value = false;
			container.EnableScissor.Value = true;
			container.Size.Value = new Vector2(originalSize.X, 0);

			Animation anim = new Animation
			(
				new Animation.Vector2MoveTo(container.Size, originalSize, messageFadeTime),
				new Animation.Set<bool>(container.ResizeVertical, true)
			);

			if (entity == null)
			{
				anim.EnabledWhenPaused.Value = false;
				this.AddComponent(anim);
			}
			else
				entity.Add(anim);
		}

		public Container ShowMessage(Entity entity, string text)
		{
			Container container = this.buildMessage();
			TextElement textElement = (TextElement)container.Children[0];
			textElement.Text.Value = text;

			this.animateMessage(entity, container);

			return container;
		}

		public void HideMessage(Entity entity, Container container, float delay = 0.0f)
		{
			if (container != null && container.Active)
			{
				container.CheckLayout();
				Animation anim = new Animation
				(
					new Animation.Delay(delay),
					new Animation.Set<bool>(container.ResizeVertical, false),
					new Animation.Vector2MoveTo(container.Size, new Vector2(container.Size.Value.X, 0), messageFadeTime),
					new Animation.Execute(container.Delete)
				);

				if (entity == null)
				{
					anim.EnabledWhenPaused.Value = false;
					this.AddComponent(anim);
				}
				else
					entity.Add(anim);
			}
		}

		public override void ClearEntities(bool deleteEditor)
		{
			base.ClearEntities(deleteEditor);
			this.messages.Children.Clear();
			this.AudioEngine.GetCategory("Music").Stop(AudioStopOptions.Immediate);
			this.AudioEngine.GetCategory("Default").Stop(AudioStopOptions.Immediate);
			this.AudioEngine.GetCategory("Ambient").Stop(AudioStopOptions.Immediate);
		}

		private void copySave(string src, string dst)
		{
			if (!Directory.Exists(dst))
				Directory.CreateDirectory(dst);

			string[] ignoredExtensions = new[] { ".cs", ".dll", ".xnb", };

			foreach (string path in Directory.GetFiles(src))
			{
				string filename = Path.GetFileName(path);
				if (filename == "thumbnail.jpg" || filename == "save.xml" || ignoredExtensions.Contains(Path.GetExtension(filename)))
					continue;
				File.Copy(path, Path.Combine(dst, filename));
			}

			foreach (string path in Directory.GetDirectories(src))
				this.copySave(path, Path.Combine(dst, Path.GetFileName(path)));
		}

		private UIComponent createMenuButton<Type>(string label, Property<Type> property)
		{
			return this.createMenuButton<Type>(label, property, x => x.ToString());
		}

		private UIComponent createMenuButton<Type>(string label, Property<Type> property, Func<Type, string> conversion)
		{
			UIComponent result = this.CreateButton();

			TextElement text = new TextElement();
			text.Name.Value = "Text";
			text.FontFile.Value = "Font";
			text.Text.Value = label;
			result.Children.Add(text);

			TextElement value = new TextElement();
			value.Position.Value = new Vector2(200.0f, value.Position.Value.Y);
			value.Name.Value = "Value";
			value.FontFile.Value = "Font";
			value.Add(new Binding<string, Type>(value.Text, conversion, property));
			result.Children.Add(value);

			return result;
		}

		public UIComponent CreateButton(Action action = null)
		{
			Container result = new Container();
			result.Tint.Value = Color.Black;
			result.Add(new Binding<Color, bool>(result.Tint, x => x ? GameMain.highlightColor : new Color(0.0f, 0.0f, 0.0f), result.Highlighted));
			result.Add(new Binding<float, bool>(result.Opacity, x => x ? 1.0f : 0.5f, result.Highlighted));
			result.Add(new NotifyBinding(delegate()
			{
				if (result.Highlighted)
					Sound.PlayCue(this, "Mouse");
			}, result.Highlighted));
			result.Add(new CommandBinding<Point>(result.MouseLeftUp, delegate(Point p)
			{
				Sound.PlayCue(this, "Click");
				if (action != null)
					action();
			}));
			return result;
		}

		public UIComponent CreateButton(string label, Action action = null)
		{
			UIComponent result = this.CreateButton(action);
			TextElement text = new TextElement();
			text.Name.Value = "Text";
			text.FontFile.Value = "Font";
			text.Text.Value = label;
			result.Children.Add(text);

			return result;
		}

#if ANALYTICS
		public Session.Recorder SessionRecorder;

		public void SaveAnalytics()
		{
			string map = this.MapFile;
			string filename = GameMain.Build.ToString() + "-" + (string.IsNullOrEmpty(map) ? "null" : Path.GetFileName(map)) + "-" + Guid.NewGuid().ToString().Replace("-", string.Empty).Substring(0, 32) + ".xml";
			this.SessionRecorder.Save(Path.Combine(this.analyticsDirectory, filename), map, this.TotalTime);
		}

		public string[] AnalyticsSessionFiles
		{
			get
			{
				return Directory.GetFiles(this.analyticsDirectory, "*", SearchOption.TopDirectoryOnly);
			}
		}
#endif

		public List<Session> LoadAnalytics(string map)
		{
			List<Session> result = new List<Session>();
			foreach (string file in Directory.GetFiles(this.analyticsDirectory, "*", SearchOption.TopDirectoryOnly))
			{
				Session s;
				try
				{
					s = Session.Load(file);
				}
				catch (Exception)
				{
					Log.d("Error loading analytics file " + file);
					continue;
				}

				if (s.Build == GameMain.Build)
				{
					string sessionMap = s.Map;
					if (sessionMap == null)
					{
						// Attempt to extract the map name from the filename
						string fileWithoutExtension = Path.GetFileNameWithoutExtension(file);

						int firstDash = fileWithoutExtension.IndexOf('-');
						int lastDash = fileWithoutExtension.LastIndexOf('-');

						if (firstDash == lastDash) // Old filename format "map-hash"
							sessionMap = fileWithoutExtension.Substring(0, firstDash);
						else // New format "build-map-hash"
							sessionMap = fileWithoutExtension.Substring(firstDash + 1, lastDash - (firstDash + 1));
					}
					if (sessionMap == map)
						result.Add(s);
				}
			}
			return result;
		}

		public TextElement CreateLink(string text, string url)
		{
			System.Windows.Forms.Form winForm = (System.Windows.Forms.Form)System.Windows.Forms.Form.FromHandle(this.Window.Handle);

			TextElement element = new TextElement();
			element.FontFile.Value = "Font";
			element.Text.Value = text;
			element.Add(new Binding<Color, bool>(element.Tint, x => x ? new Color(1.0f, 0.0f, 0.0f) : new Color(91.0f / 255.0f, 175.0f / 255.0f, 205.0f / 255.0f), element.Highlighted));
			element.Add(new CommandBinding<Point>(element.MouseLeftUp, delegate(Point mouse)
			{
				this.ExitFullscreen();
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url));
			}));
			element.Add(new CommandBinding<Point>(element.MouseOver, delegate(Point mouse)
			{
				winForm.Cursor = System.Windows.Forms.Cursors.Hand;
			}));
			element.Add(new CommandBinding<Point>(element.MouseOut, delegate(Point mouse)
			{
				winForm.Cursor = System.Windows.Forms.Cursors.Default;
			}));

			return element;
		}

		// Takes a screenshot and saves a directory with a copy of all the map files
		public Command Save = new Command();

		// Just saves the current map file
		public Command SaveCurrentMap = new Command();

		protected string currentSave;

		protected override void LoadContent()
		{
			bool firstInitialization = this.firstLoadContentCall;
			base.LoadContent();

			if (firstInitialization)
			{
				this.IsMouseVisible.Value = true;

				try
				{
					this.musicWaveBank = new WaveBank(this.AudioEngine, Path.Combine(this.Content.RootDirectory, "Game\\Music\\Music.xwb"));
					this.MusicBank = new SoundBank(this.AudioEngine, Path.Combine(this.Content.RootDirectory, "Game\\Music\\Music.xsb"));
				}
				catch (Exception)
				{
					// Don't HAVE to load music
				}

#if ANALYTICS
				this.SessionRecorder = new Session.Recorder();
				this.AddComponent(this.SessionRecorder);

				this.SessionRecorder.Add("Position", delegate()
				{
					Entity p = this.player;
					if (p != null && p.Active)
						return p.Get<Transform>().Position;
					else
						return Vector3.Zero;
				});

				this.SessionRecorder.Add("Health", delegate()
				{
					Entity p = this.player;
					if (p != null && p.Active)
						return p.Get<Player>().Health;
					else
						return 0.0f;
				});
#endif

				this.MapFile.Set = delegate(string value)
				{
					if (value == null || value.Length == 0)
					{
						this.MapFile.InternalValue = null;
						return;
					}

					try
					{
						string directory = this.currentSave == null ? null : Path.Combine(this.saveDirectory, this.currentSave);
						if (value == GameMain.MenuMap)
							directory = null; // Don't try to load the menu from a save game
						IO.MapLoader.Load(this, directory, value, false);
					}
					catch (FileNotFoundException)
					{
						this.MapFile.InternalValue = value;
						// Create a new map
						Entity world = Factory.Get<WorldFactory>().CreateAndBind(this);
						world.Get<Transform>().Position.Value = new Vector3(0, 3, 0);
						this.Add(world);

						Entity ambientLight = Factory.Get<AmbientLightFactory>().CreateAndBind(this);
						ambientLight.Get<Transform>().Position.Value = new Vector3(0, 5.0f, 0);
						ambientLight.Get<AmbientLight>().Color.Value = new Vector3(0.25f, 0.25f, 0.25f);
						this.Add(ambientLight);

						Entity map = Factory.Get<MapFactory>().CreateAndBind(this);
						map.Get<Transform>().Position.Value = new Vector3(0, 1, 0);
						this.Add(map);

						this.MapLoaded.Execute();
					}
				};

				this.Renderer.LightRampTexture.Value = "Images\\default-ramp";
				this.Renderer.EnvironmentMap.Value = "Images\\env0";

				this.input = new PCInput();
				this.AddComponent(this.input);

				new TwoWayBinding<LightingManager.DynamicShadowSetting>(this.Settings.DynamicShadows, this.LightingManager.DynamicShadows);
				new TwoWayBinding<float>(this.Settings.MotionBlurAmount, this.Renderer.MotionBlurAmount);
				new TwoWayBinding<float>(this.Settings.Gamma, this.Renderer.Gamma);
				new TwoWayBinding<bool>(this.Settings.EnableBloom, this.Renderer.EnableBloom);
				new TwoWayBinding<float>(this.Settings.FieldOfView, this.Camera.FieldOfView);

				// Message list
				this.messages = new ListContainer();
				this.messages.Alignment.Value = ListContainer.ListAlignment.Max;
				this.messages.AnchorPoint.Value = new Vector2(1.0f, 1.0f);
				this.messages.Reversed.Value = true;
				this.messages.Add(new Binding<Vector2, Point>(this.messages.Position, x => new Vector2(x.X * 0.9f, x.Y * 0.9f), this.ScreenSize));
				this.UI.Root.Children.Add(this.messages);

				ListContainer notifications = new ListContainer();
				notifications.Alignment.Value = ListContainer.ListAlignment.Max;
				notifications.AnchorPoint.Value = new Vector2(1.0f, 0.0f);
				notifications.Name.Value = "Notifications";
				notifications.Add(new Binding<Vector2, Point>(notifications.Position, x => new Vector2(x.X * 0.9f, x.Y * 0.1f), this.ScreenSize));
				this.UI.Root.Children.Add(notifications);

#if DEBUG
				Log.Handler = delegate(string log)
				{
					this.HideMessage(null, this.ShowMessage(null, log), 2.0f);
				};
#endif

				// Load strings
				this.Strings.Load(Path.Combine(this.Content.RootDirectory, "Strings.xlsx"));

				foreach (string file in Directory.GetFiles(Path.Combine(this.Content.RootDirectory, "Game"), "*.xlsx", SearchOption.TopDirectoryOnly))
					this.Strings.Load(file);

				bool controlsShown = false;

				// Toggle fullscreen
				this.input.Bind(this.Settings.ToggleFullscreen, PCInput.InputState.Down, delegate()
				{
					if (this.graphics.IsFullScreen) // Already fullscreen. Go to windowed mode.
						this.ExitFullscreen();
					else // In windowed mode. Go to fullscreen.
						this.EnterFullscreen();
				});

				new Binding<string, Config.Lang>(this.Strings.Language, x => x.ToString(), this.Settings.Language);
				new NotifyBinding(this.saveSettings, this.Settings.Language);

				// Fullscreen message
				Container msgBackground = new Container();
				this.UI.Root.Children.Add(msgBackground);
				msgBackground.Tint.Value = Color.Black;
				msgBackground.Opacity.Value = 0.2f;
				msgBackground.AnchorPoint.Value = new Vector2(0.5f, 1.0f);
				msgBackground.Add(new Binding<Vector2, Point>(msgBackground.Position, x => new Vector2(x.X * 0.5f, x.Y - 30.0f), this.ScreenSize));
				TextElement msg = new TextElement();
				msg.FontFile.Value = "Font";
				msg.Text.Value = "\\toggle fullscreen tooltip";
				msgBackground.Children.Add(msg);
				this.AddComponent(new Animation
				(
					new Animation.Delay(4.0f),
					new Animation.Parallel
					(
						new Animation.FloatMoveTo(msgBackground.Opacity, 0.0f, 2.0f),
						new Animation.FloatMoveTo(msg.Opacity, 0.0f, 2.0f)
					),
					new Animation.Execute(delegate() { this.UI.Root.Children.Remove(msgBackground); })
				));

				Property<UIComponent> currentMenu = new Property<UIComponent> { Value = null };

				// Pause menu
				ListContainer pauseMenu = new ListContainer();
				pauseMenu.Visible.Value = false;
				pauseMenu.Add(new Binding<Vector2, Point>(pauseMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.ScreenSize));
				pauseMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
				this.UI.Root.Children.Add(pauseMenu);
				pauseMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

				Animation pauseAnimation = null;

				Action hidePauseMenu = delegate()
				{
					if (pauseAnimation != null)
						pauseAnimation.Delete.Execute();
					pauseAnimation = new Animation
					(
						new Animation.Vector2MoveToSpeed(pauseMenu.AnchorPoint, new Vector2(1, 0.5f), 5.0f),
						new Animation.Set<bool>(pauseMenu.Visible, false)
					);
					this.AddComponent(pauseAnimation);
					currentMenu.Value = null;
				};

				Action showPauseMenu = delegate()
				{
					pauseMenu.Visible.Value = true;
					if (pauseAnimation != null)
						pauseAnimation.Delete.Execute();
					pauseAnimation = new Animation(new Animation.Vector2MoveToSpeed(pauseMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f));
					this.AddComponent(pauseAnimation);
					currentMenu.Value = pauseMenu;
				};

				// Settings to be restored when unpausing
				float originalBlurAmount = 0.0f;
				bool originalMouseVisible = false;
				Point originalMousePosition = new Point();

				RenderTarget2D screenshot = null;
				Point screenshotSize = Point.Zero;

				Action<bool> setupScreenshot = delegate(bool s)
				{
					this.saveAfterTakingScreenshot = s;
					screenshotSize = this.ScreenSize;
					screenshot = new RenderTarget2D(this.GraphicsDevice, screenshotSize.X, screenshotSize.Y, false, SurfaceFormat.Color, DepthFormat.Depth16);
					this.renderTarget = screenshot;
				};

				// Pause
				Action savePausedSettings = delegate()
				{
					// Take screenshot
					setupScreenshot(false);

					originalMouseVisible = this.IsMouseVisible;
					this.IsMouseVisible.Value = true;
					originalBlurAmount = this.Renderer.BlurAmount;

					// Save mouse position
					MouseState mouseState = this.MouseState;
					originalMousePosition = new Point(mouseState.X, mouseState.Y);

					pauseMenu.Visible.Value = true;
					pauseMenu.AnchorPoint.Value = new Vector2(1, 0.5f);

					// Blur the screen and show the pause menu
					if (pauseAnimation != null && pauseAnimation.Active)
						pauseAnimation.Delete.Execute();

					pauseAnimation = new Animation
					(
						new Animation.Parallel
						(
							new Animation.FloatMoveToSpeed(this.Renderer.BlurAmount, 1.0f, 1.0f),
							new Animation.Vector2MoveToSpeed(pauseMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f)
						)
					);
					this.AddComponent(pauseAnimation);

					currentMenu.Value = pauseMenu;

					if (this.MapFile.Value != GameMain.MenuMap)
					{
						this.AudioEngine.GetCategory("Default").Pause();
					}
				};

				// Unpause
				Action restorePausedSettings = delegate()
				{
					if (pauseAnimation != null && pauseAnimation.Active)
						pauseAnimation.Delete.Execute();

					// Restore mouse
					if (!originalMouseVisible)
					{
						// Only restore mouse position if the cursor was not visible
						// i.e., we're in first-person camera mode
						Microsoft.Xna.Framework.Input.Mouse.SetPosition(originalMousePosition.X, originalMousePosition.Y);
						MouseState m = new MouseState(originalMousePosition.X, originalMousePosition.Y, this.MouseState.Value.ScrollWheelValue, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
						this.LastMouseState.Value = m;
						this.MouseState.Value = m;
					}
					this.IsMouseVisible.Value = originalMouseVisible;

					this.saveSettings();

					// Unlur the screen and show the pause menu
					if (pauseAnimation != null && pauseAnimation.Active)
						pauseAnimation.Delete.Execute();

					this.Renderer.BlurAmount.Value = originalBlurAmount;
					pauseAnimation = new Animation
					(
						new Animation.Parallel
						(
							new Animation.Sequence
							(
								new Animation.Vector2MoveToSpeed(pauseMenu.AnchorPoint, new Vector2(1, 0.5f), 5.0f),
								new Animation.Set<bool>(pauseMenu.Visible, false)
							)
						)
					);
					this.AddComponent(pauseAnimation);

					if (screenshot != null)
					{
						screenshot.Dispose();
						screenshot = null;
						screenshotSize = Point.Zero;
					}

					currentMenu.Value = null;

					this.AudioEngine.GetCategory("Default").Resume();
				};

				// Load / save menu
				ListContainer loadSaveMenu = new ListContainer();
				loadSaveMenu.Visible.Value = false;
				loadSaveMenu.Add(new Binding<Vector2, Point>(loadSaveMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.ScreenSize));
				loadSaveMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
				loadSaveMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;
				this.UI.Root.Children.Add(loadSaveMenu);

				bool loadSaveShown = false;
				Animation loadSaveAnimation = null;

				Property<bool> saveMode = new Property<bool> { Value = false };

				Container dialog = null;

				Action<string, string, Action> showDialog = delegate(string question, string action, Action callback)
				{
					if (dialog != null)
						dialog.Delete.Execute();
					dialog = new Container();
					dialog.Tint.Value = Color.Black;
					dialog.Opacity.Value = 0.5f;
					dialog.AnchorPoint.Value = new Vector2(0.5f);
					dialog.Add(new Binding<Vector2, Point>(dialog.Position, x => new Vector2(x.X * 0.5f, x.Y * 0.5f), this.ScreenSize));
					dialog.Add(new CommandBinding(dialog.Delete, delegate()
					{
						loadSaveMenu.EnableInput.Value = true;
					}));
					this.UI.Root.Children.Add(dialog);

					ListContainer dialogLayout = new ListContainer();
					dialogLayout.Orientation.Value = ListContainer.ListOrientation.Vertical;
					dialog.Children.Add(dialogLayout);

					TextElement prompt = new TextElement();
					prompt.FontFile.Value = "Font";
					prompt.Text.Value = question;
					dialogLayout.Children.Add(prompt);

					ListContainer dialogButtons = new ListContainer();
					dialogButtons.Orientation.Value = ListContainer.ListOrientation.Horizontal;
					dialogLayout.Children.Add(dialogButtons);

					UIComponent okay = this.CreateButton("", delegate()
					{
						dialog.Delete.Execute();
						dialog = null;
						callback();
					});
					TextElement okayText = (TextElement)okay.GetChildByName("Text");
					okayText.Add(new Binding<string, bool>(okayText.Text, x => action + (x ? " gamepad" : ""), this.GamePadConnected));
					okay.Name.Value = "Okay";
					dialogButtons.Children.Add(okay);

					UIComponent cancel = this.CreateButton("\\cancel", delegate()
					{
						dialog.Delete.Execute();
						dialog = null;
					});
					dialogButtons.Children.Add(cancel);

					TextElement cancelText = (TextElement)cancel.GetChildByName("Text");
					cancelText.Add(new Binding<string, bool>(cancelText.Text, x => x ? "\\cancel gamepad" : "\\cancel", this.GamePadConnected));
				};

				Action hideLoadSave = delegate()
				{
					showPauseMenu();

					if (dialog != null)
					{
						dialog.Delete.Execute();
						dialog = null;
					}

					loadSaveShown = false;

					if (loadSaveAnimation != null)
						loadSaveAnimation.Delete.Execute();
					loadSaveAnimation = new Animation
					(
						new Animation.Vector2MoveToSpeed(loadSaveMenu.AnchorPoint, new Vector2(1, 0.5f), 5.0f),
						new Animation.Set<bool>(loadSaveMenu.Visible, false)
					);
					this.AddComponent(loadSaveAnimation);
				};

				Container loadSavePadding = new Container();
				loadSavePadding.Opacity.Value = 0.0f;
				loadSavePadding.PaddingLeft.Value = 8.0f;
				loadSaveMenu.Children.Add(loadSavePadding);

				ListContainer loadSaveLabelContainer = new ListContainer();
				loadSaveLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
				loadSavePadding.Children.Add(loadSaveLabelContainer);

				TextElement loadSaveLabel = new TextElement();
				loadSaveLabel.FontFile.Value = "Font";
				loadSaveLabel.Add(new Binding<string, bool>(loadSaveLabel.Text, x => x ? "S A V E" : "L O A D", saveMode));
				loadSaveLabelContainer.Children.Add(loadSaveLabel);

				TextElement loadSaveScrollLabel = new TextElement();
				loadSaveScrollLabel.FontFile.Value = "Font";
				loadSaveScrollLabel.Text.Value = "\\scroll for more";
				loadSaveLabelContainer.Children.Add(loadSaveScrollLabel);

				TextElement quickSaveLabel = new TextElement();
				quickSaveLabel.FontFile.Value = "Font";
				quickSaveLabel.Add(new Binding<bool>(quickSaveLabel.Visible, saveMode));
				quickSaveLabel.Text.Value = "\\quicksave instructions";
				loadSaveLabelContainer.Children.Add(quickSaveLabel);

				UIComponent loadSaveBack = this.CreateButton("\\back", hideLoadSave);
				loadSaveMenu.Children.Add(loadSaveBack);

				Action save = null;

				UIComponent saveNew = this.CreateButton("\\save new", delegate()
				{
					save();
					hideLoadSave();
					this.Paused.Value = false;
					restorePausedSettings();
				});
				saveNew.Add(new Binding<bool>(saveNew.Visible, saveMode));
				loadSaveMenu.Children.Add(saveNew);

				Scroller loadSaveScroll = new Scroller();
				loadSaveScroll.Add(new Binding<Vector2, Point>(loadSaveScroll.Size, x => new Vector2(276.0f, x.Y * 0.5f), this.ScreenSize));
				loadSaveMenu.Children.Add(loadSaveScroll);

				ListContainer loadSaveList = new ListContainer();
				loadSaveList.Orientation.Value = ListContainer.ListOrientation.Vertical;
				loadSaveList.Reversed.Value = true;
				loadSaveScroll.Children.Add(loadSaveList);

				Action<string> addSaveGame = delegate(string timestamp)
				{
					SaveInfo info = null;
					try
					{
						using (Stream stream = new FileStream(Path.Combine(this.saveDirectory, timestamp, "save.xml"), FileMode.Open, FileAccess.Read, FileShare.None))
							info = (SaveInfo)new XmlSerializer(typeof(SaveInfo)).Deserialize(stream);
						if (info.Version != GameMain.MapVersion)
							throw new Exception();
					}
					catch (Exception)
					{
						string savePath = Path.Combine(this.saveDirectory, timestamp);
						if (Directory.Exists(savePath))
						{
							try
							{
								Directory.Delete(savePath, true);
							}
							catch (Exception)
							{
								// Whatever. We can't delete it, tough beans.
							}
						}
						return;
					}

					UIComponent container = this.CreateButton();
					container.UserData.Value = timestamp;

					ListContainer layout = new ListContainer();
					layout.Orientation.Value = ListContainer.ListOrientation.Vertical;
					container.Children.Add(layout);

					Sprite sprite = new Sprite();
					sprite.IsStandardImage.Value = true;
					sprite.Image.Value = Path.Combine(this.saveDirectory, timestamp, "thumbnail.jpg");
					layout.Children.Add(sprite);

					TextElement label = new TextElement();
					label.FontFile.Value = "Font";
					label.Text.Value = timestamp;
					layout.Children.Add(label);

					container.Add(new CommandBinding<Point>(container.MouseLeftUp, delegate(Point p)
					{
						if (saveMode)
						{
							loadSaveMenu.EnableInput.Value = false;
							showDialog("\\overwrite prompt", "\\overwrite", delegate()
							{
								container.Delete.Execute();
								save();
								Directory.Delete(Path.Combine(this.saveDirectory, timestamp), true);
								hideLoadSave();
								this.Paused.Value = false;
								restorePausedSettings();
							});
						}
						else
						{
							this.loadingSavedGame = true;
							hideLoadSave();
							this.Paused.Value = false;
							restorePausedSettings();
							this.currentSave = timestamp;
							this.MapFile.Value = info.MapFile;
						}
					}));

					loadSaveList.Children.Add(container);
					loadSaveScroll.ScrollToTop();
				};

				Action createNewSave = delegate()
				{
					string newSave = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
					if (newSave != this.currentSave)
					{
						this.copySave(this.currentSave == null ? IO.MapLoader.MapDirectory : Path.Combine(this.saveDirectory, this.currentSave), Path.Combine(this.saveDirectory, newSave));
						this.currentSave = newSave;
					}
				};

				this.SaveCurrentMap.Action = delegate()
				{
					if (this.currentSave == null)
						createNewSave();
					IO.MapLoader.Save(this, Path.Combine(this.saveDirectory, this.currentSave), this.MapFile);
				};

				save = delegate()
				{
					createNewSave();

					using (Stream stream = File.OpenWrite(Path.Combine(this.saveDirectory, this.currentSave, "thumbnail.jpg")))
						screenshot.SaveAsJpeg(stream, 256, (int)(screenshotSize.Y * (256.0f / screenshotSize.X)));

					this.SaveCurrentMap.Execute();

					try
					{
						using (Stream stream = new FileStream(Path.Combine(this.saveDirectory, this.currentSave, "save.xml"), FileMode.Create, FileAccess.Write, FileShare.None))
							new XmlSerializer(typeof(SaveInfo)).Serialize(stream, new SaveInfo { MapFile = this.MapFile, Version = GameMain.MapVersion });
					}
					catch (InvalidOperationException e)
					{
						throw new Exception("Failed to save game.", e);
					}

					addSaveGame(this.currentSave);
				};

				this.Save.Action = delegate()
				{
					if (screenshot == null)
						setupScreenshot(true);
					else
					{
						// Delete the old save thumbnail.
						string oldSave = this.currentSave;
						if (oldSave != null)
						{
							UIComponent container = loadSaveList.Children.FirstOrDefault(x => ((string)x.UserData.Value) == this.currentSave);
							if (container != null)
								container.Delete.Execute();
						}

						// Create the new save.
						save();

						// Delete the old save files.
						// We have to do this AFTER creating the new save
						// because it copies the old save to create the new one
						if (oldSave != null)
							Directory.Delete(Path.Combine(this.saveDirectory, oldSave), true);

						this.saveAfterTakingScreenshot = false;
						screenshot.Dispose();
						screenshot = null;
						screenshotSize = Point.Zero;
					}
				};

				foreach (string saveFile in Directory.GetDirectories(this.saveDirectory, "*", SearchOption.TopDirectoryOnly).Select(x => Path.GetFileName(x)).OrderBy(x => x))
					addSaveGame(saveFile);

				saveNew.Add(new CommandBinding<Point>(saveNew.MouseLeftUp, delegate(Point p)
				{
				}));

				// Settings menu
				bool settingsShown = false;
				Animation settingsAnimation = null;

				ListContainer settingsMenu = new ListContainer();
				settingsMenu.Visible.Value = false;
				settingsMenu.Add(new Binding<Vector2, Point>(settingsMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.ScreenSize));
				settingsMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
				this.UI.Root.Children.Add(settingsMenu);
				settingsMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

				Container settingsLabelPadding = new Container();
				settingsLabelPadding.PaddingLeft.Value = 8.0f;
				settingsLabelPadding.Opacity.Value = 0.0f;
				settingsMenu.Children.Add(settingsLabelPadding);

				ListContainer settingsLabelContainer = new ListContainer();
				settingsLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
				settingsLabelPadding.Children.Add(settingsLabelContainer);

				TextElement settingsLabel = new TextElement();
				settingsLabel.FontFile.Value = "Font";
				settingsLabel.Text.Value = "\\options title";
				settingsLabelContainer.Children.Add(settingsLabel);

				TextElement settingsScrollLabel = new TextElement();
				settingsScrollLabel.FontFile.Value = "Font";
				settingsScrollLabel.Add(new Binding<string>(settingsScrollLabel.Text, delegate()
				{
					if (this.GamePadConnected)
						return "\\modify setting gamepad";
					else
						return "\\modify setting";
				}, this.GamePadConnected));
				settingsLabelContainer.Children.Add(settingsScrollLabel);

				Action hideSettings = delegate()
				{
					showPauseMenu();

					settingsShown = false;

					if (settingsAnimation != null)
						settingsAnimation.Delete.Execute();
					settingsAnimation = new Animation
					(
						new Animation.Vector2MoveToSpeed(settingsMenu.AnchorPoint, new Vector2(1, 0.5f), 5.0f),
						new Animation.Set<bool>(settingsMenu.Visible, false)
					);
					this.AddComponent(settingsAnimation);
				};

				UIComponent settingsBack = this.CreateButton("\\back", hideSettings);
				settingsMenu.Children.Add(settingsBack);

				UIComponent fullscreenResolution = this.createMenuButton<Point>("\\fullscreen resolution", this.Settings.FullscreenResolution, x => x.X.ToString() + "x" + x.Y.ToString());
				
				Action<int> changeFullscreenResolution = delegate(int scroll)
				{
					displayModeIndex = (displayModeIndex + scroll) % this.supportedDisplayModes.Count();
					while (displayModeIndex < 0)
						displayModeIndex += this.supportedDisplayModes.Count();
					DisplayMode mode = this.supportedDisplayModes.ElementAt(displayModeIndex);
					this.Settings.FullscreenResolution.Value = new Point(mode.Width, mode.Height);
				};

				fullscreenResolution.Add(new CommandBinding<Point>(fullscreenResolution.MouseLeftUp, delegate(Point mouse)
				{
					changeFullscreenResolution(1);
				}));
				fullscreenResolution.Add(new CommandBinding<Point, int>(fullscreenResolution.MouseScrolled, delegate(Point mouse, int scroll)
				{
					changeFullscreenResolution(scroll);
				}));
				settingsMenu.Children.Add(fullscreenResolution);

				UIComponent gamma = this.createMenuButton<float>("\\gamma", this.Renderer.Gamma, x => ((int)Math.Round(x * 100.0f)).ToString() + "%");
				gamma.Add(new CommandBinding<Point, int>(gamma.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Renderer.Gamma.Value = Math.Max(0, Math.Min(2, this.Renderer.Gamma + (scroll * 0.1f)));
				}));
				settingsMenu.Children.Add(gamma);

				UIComponent fieldOfView = this.createMenuButton<float>("\\field of view", this.Camera.FieldOfView, x => ((int)Math.Round(MathHelper.ToDegrees(this.Camera.FieldOfView))).ToString() + "�");
				fieldOfView.Add(new CommandBinding<Point, int>(fieldOfView.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Camera.FieldOfView.Value = Math.Max(MathHelper.ToRadians(60.0f), Math.Min(MathHelper.ToRadians(120.0f), this.Camera.FieldOfView + MathHelper.ToRadians(scroll)));
				}));
				settingsMenu.Children.Add(fieldOfView);

				UIComponent motionBlurAmount = this.createMenuButton<float>("\\motion blur amount", this.Renderer.MotionBlurAmount, x => ((int)Math.Round(x * 100.0f)).ToString() + "%");
				motionBlurAmount.Add(new CommandBinding<Point, int>(motionBlurAmount.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Renderer.MotionBlurAmount.Value = Math.Max(0, Math.Min(1, this.Renderer.MotionBlurAmount + (scroll * 0.1f)));
				}));
				settingsMenu.Children.Add(motionBlurAmount);

				UIComponent reflectionsEnabled = this.createMenuButton<bool>("\\reflections enabled", this.Settings.EnableReflections);
				reflectionsEnabled.Add(new CommandBinding<Point, int>(reflectionsEnabled.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Settings.EnableReflections.Value = !this.Settings.EnableReflections;
				}));
				reflectionsEnabled.Add(new CommandBinding<Point>(reflectionsEnabled.MouseLeftUp, delegate(Point mouse)
				{
					this.Settings.EnableReflections.Value = !this.Settings.EnableReflections;
				}));
				settingsMenu.Children.Add(reflectionsEnabled);

				UIComponent bloomEnabled = this.createMenuButton<bool>("\\bloom enabled", this.Renderer.EnableBloom);
				bloomEnabled.Add(new CommandBinding<Point, int>(bloomEnabled.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Renderer.EnableBloom.Value = !this.Renderer.EnableBloom;
				}));
				bloomEnabled.Add(new CommandBinding<Point>(bloomEnabled.MouseLeftUp, delegate(Point mouse)
				{
					this.Renderer.EnableBloom.Value = !this.Renderer.EnableBloom;
				}));
				settingsMenu.Children.Add(bloomEnabled);

				UIComponent dynamicShadows = this.createMenuButton<LightingManager.DynamicShadowSetting>("\\dynamic shadows", this.LightingManager.DynamicShadows);
				int numDynamicShadowSettings = typeof(LightingManager.DynamicShadowSetting).GetFields(BindingFlags.Static | BindingFlags.Public).Length;
				dynamicShadows.Add(new CommandBinding<Point, int>(dynamicShadows.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.LightingManager.DynamicShadows.Value = (LightingManager.DynamicShadowSetting)Enum.ToObject(typeof(LightingManager.DynamicShadowSetting), (((int)this.LightingManager.DynamicShadows.Value) + scroll) % numDynamicShadowSettings);
				}));
				dynamicShadows.Add(new CommandBinding<Point>(dynamicShadows.MouseLeftUp, delegate(Point mouse)
				{
					this.LightingManager.DynamicShadows.Value = (LightingManager.DynamicShadowSetting)Enum.ToObject(typeof(LightingManager.DynamicShadowSetting), (((int)this.LightingManager.DynamicShadows.Value) + 1) % numDynamicShadowSettings);
				}));
				settingsMenu.Children.Add(dynamicShadows);

				// Controls menu
				Animation controlsAnimation = null;

				ListContainer controlsMenu = new ListContainer();
				controlsMenu.Visible.Value = false;
				controlsMenu.Add(new Binding<Vector2, Point>(controlsMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.ScreenSize));
				controlsMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
				this.UI.Root.Children.Add(controlsMenu);
				controlsMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

				Container controlsLabelPadding = new Container();
				controlsLabelPadding.PaddingLeft.Value = 8.0f;
				controlsLabelPadding.Opacity.Value = 0.0f;
				controlsMenu.Children.Add(controlsLabelPadding);

				ListContainer controlsLabelContainer = new ListContainer();
				controlsLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
				controlsLabelPadding.Children.Add(controlsLabelContainer);

				TextElement controlsLabel = new TextElement();
				controlsLabel.FontFile.Value = "Font";
				controlsLabel.Text.Value = "\\controls title";
				controlsLabelContainer.Children.Add(controlsLabel);

				TextElement controlsScrollLabel = new TextElement();
				controlsScrollLabel.FontFile.Value = "Font";
				controlsScrollLabel.Text.Value = "\\scroll for more";
				controlsLabelContainer.Children.Add(controlsScrollLabel);

				Action hideControls = delegate()
				{
					controlsShown = false;

					showPauseMenu();

					if (controlsAnimation != null)
						controlsAnimation.Delete.Execute();
					controlsAnimation = new Animation
					(
						new Animation.Vector2MoveToSpeed(controlsMenu.AnchorPoint, new Vector2(1, 0.5f), 5.0f),
						new Animation.Set<bool>(controlsMenu.Visible, false)
					);
					this.AddComponent(controlsAnimation);
				};

				UIComponent controlsBack = this.CreateButton("\\back", hideControls);
				controlsMenu.Children.Add(controlsBack);

				ListContainer controlsList = new ListContainer();
				controlsList.Orientation.Value = ListContainer.ListOrientation.Vertical;

				Scroller controlsScroller = new Scroller();
				controlsScroller.Add(new Binding<Vector2>(controlsScroller.Size, () => new Vector2(controlsList.Size.Value.X, this.ScreenSize.Value.Y * 0.5f), controlsList.Size, this.ScreenSize));
				controlsScroller.Children.Add(controlsList);
				controlsMenu.Children.Add(controlsScroller);

				UIComponent invertMouseX = this.createMenuButton<bool>("\\invert look x", this.Settings.InvertMouseX);
				invertMouseX.Add(new CommandBinding<Point, int>(invertMouseX.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Settings.InvertMouseX.Value = !this.Settings.InvertMouseX;
				}));
				invertMouseX.Add(new CommandBinding<Point>(invertMouseX.MouseLeftUp, delegate(Point mouse)
				{
					this.Settings.InvertMouseX.Value = !this.Settings.InvertMouseX;
				}));
				controlsList.Children.Add(invertMouseX);

				UIComponent invertMouseY = this.createMenuButton<bool>("\\invert look y", this.Settings.InvertMouseY);
				invertMouseY.Add(new CommandBinding<Point, int>(invertMouseY.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Settings.InvertMouseY.Value = !this.Settings.InvertMouseY;
				}));
				invertMouseY.Add(new CommandBinding<Point>(invertMouseY.MouseLeftUp, delegate(Point mouse)
				{
					this.Settings.InvertMouseY.Value = !this.Settings.InvertMouseY;
				}));
				controlsList.Children.Add(invertMouseY);

				UIComponent mouseSensitivity = this.createMenuButton<float>("\\look sensitivity", this.Settings.MouseSensitivity, x => ((int)Math.Round(x * 100.0f)).ToString() + "%");
				mouseSensitivity.SwallowMouseEvents.Value = true;
				mouseSensitivity.Add(new CommandBinding<Point, int>(mouseSensitivity.MouseScrolled, delegate(Point mouse, int scroll)
				{
					this.Settings.MouseSensitivity.Value = Math.Max(0, Math.Min(5, this.Settings.MouseSensitivity + (scroll * 0.1f)));
				}));
				controlsList.Children.Add(mouseSensitivity);

				Action<Property<PCInput.PCInputBinding>, string, bool, bool> addInputSetting = delegate(Property<PCInput.PCInputBinding> setting, string display, bool allowGamepad, bool allowMouse)
				{
					this.bindings.Add(setting);
					UIComponent button = this.createMenuButton<PCInput.PCInputBinding>(display, setting);
					button.Add(new CommandBinding<Point>(button.MouseLeftUp, delegate(Point mouse)
					{
						PCInput.PCInputBinding originalValue = setting;
						setting.Value = new PCInput.PCInputBinding();
						this.UI.EnableMouse.Value = false;
						input.GetNextInput(delegate(PCInput.PCInputBinding binding)
						{
							if (binding.Key == Keys.Escape)
								setting.Value = originalValue;
							else
							{
								PCInput.PCInputBinding newValue = new PCInput.PCInputBinding();
								newValue.Key = originalValue.Key;
								newValue.MouseButton = originalValue.MouseButton;
								newValue.GamePadButton = originalValue.GamePadButton;

								if (binding.Key != Keys.None)
								{
									newValue.Key = binding.Key;
									newValue.MouseButton = PCInput.MouseButton.None;
								}
								else if (allowMouse && binding.MouseButton != PCInput.MouseButton.None)
								{
									newValue.Key = Keys.None;
									newValue.MouseButton = binding.MouseButton;
								}

								if (allowGamepad)
								{
									if (binding.GamePadButton != Buttons.BigButton)
										newValue.GamePadButton = binding.GamePadButton;
								}
								else
									newValue.GamePadButton = Buttons.BigButton;

								setting.Value = newValue;
							}
							this.UI.EnableMouse.Value = true;
						});
					}));
					controlsList.Children.Add(button);
				};

				addInputSetting(this.Settings.Forward, "\\move forward", false, true);
				addInputSetting(this.Settings.Left, "\\move left", false, true);
				addInputSetting(this.Settings.Backward, "\\move backward", false, true);
				addInputSetting(this.Settings.Right, "\\move right", false, true);
				addInputSetting(this.Settings.Jump, "\\jump", true, true);
				addInputSetting(this.Settings.Parkour, "\\parkour", true, true);
				addInputSetting(this.Settings.RollKick, "\\roll / kick", true, true);
				addInputSetting(this.Settings.TogglePhone, "\\toggle phone", true, true);
				addInputSetting(this.Settings.QuickSave, "\\quicksave", true, true);

				// Mapping LMB to toggle fullscreen makes it impossible to change any other settings.
				// So don't allow it.
				addInputSetting(this.Settings.ToggleFullscreen, "\\toggle fullscreen", true, false);

				// Start new button
				UIComponent startNew = this.CreateButton("\\new game", delegate()
				{
					showDialog("\\alpha disclaimer", "\\play", delegate()
					{
						restorePausedSettings();
						this.currentSave = null;
						this.AddComponent(new Animation
						(
							new Animation.Delay(0.2f),
							new Animation.Set<string>(this.MapFile, GameMain.InitialMap)
						));
					});
				});
				pauseMenu.Children.Add(startNew);
				startNew.Add(new Binding<bool, string>(startNew.Visible, x => x == GameMain.MenuMap, this.MapFile));

				// Resume button
				UIComponent resume = this.CreateButton("\\resume", delegate()
				{
					this.Paused.Value = false;
					restorePausedSettings();
				});
				resume.Visible.Value = false;
				pauseMenu.Children.Add(resume);
				resume.Add(new Binding<bool, string>(resume.Visible, x => x != GameMain.MenuMap, this.MapFile));

				// Save button
				UIComponent saveButton = this.CreateButton("\\save", delegate()
				{
					hidePauseMenu();

					saveMode.Value = true;

					loadSaveMenu.Visible.Value = true;
					if (loadSaveAnimation != null)
						loadSaveAnimation.Delete.Execute();
					loadSaveAnimation = new Animation(new Animation.Vector2MoveToSpeed(loadSaveMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f));
					this.AddComponent(loadSaveAnimation);

					loadSaveShown = true;
					currentMenu.Value = loadSaveList;
				});
				saveButton.Add(new Binding<bool>(saveButton.Visible, () => this.MapFile != GameMain.MenuMap && (this.player.Value != null && this.player.Value.Active), this.MapFile, this.player));

				pauseMenu.Children.Add(saveButton);

				Action showLoad = delegate()
				{
					hidePauseMenu();

					saveMode.Value = false;

					loadSaveMenu.Visible.Value = true;
					if (loadSaveAnimation != null)
						loadSaveAnimation.Delete.Execute();
					loadSaveAnimation = new Animation(new Animation.Vector2MoveToSpeed(loadSaveMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f));
					this.AddComponent(loadSaveAnimation);

					loadSaveShown = true;
					currentMenu.Value = loadSaveList;
				};

				// Load button
				UIComponent load = this.CreateButton("\\load", showLoad);
				pauseMenu.Children.Add(load);

				// Sandbox button
				UIComponent sandbox = this.CreateButton("\\sandbox", delegate()
				{
					showDialog("\\sandbox disclaimer", "\\play anyway", delegate()
					{
						restorePausedSettings();
						this.currentSave = null;
						this.AddComponent(new Animation
						(
							new Animation.Delay(0.2f),
							new Animation.Set<string>(this.MapFile, "sandbox")
						));
					});
				});
				pauseMenu.Children.Add(sandbox);
				sandbox.Add(new Binding<bool, string>(sandbox.Visible, x => x == GameMain.MenuMap, this.MapFile));

				// Cheat menu
#if CHEAT
				Animation cheatAnimation = null;
				bool cheatShown = false;

				ListContainer cheatMenu = new ListContainer();
				cheatMenu.Visible.Value = false;
				cheatMenu.Add(new Binding<Vector2, Point>(cheatMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.ScreenSize));
				cheatMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
				this.UI.Root.Children.Add(cheatMenu);
				cheatMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

				Container cheatLabelPadding = new Container();
				cheatLabelPadding.PaddingLeft.Value = 8.0f;
				cheatLabelPadding.Opacity.Value = 0.0f;
				cheatMenu.Children.Add(cheatLabelPadding);

				ListContainer cheatLabelContainer = new ListContainer();
				cheatLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
				cheatLabelPadding.Children.Add(cheatLabelContainer);

				TextElement cheatLabel = new TextElement();
				cheatLabel.FontFile.Value = "Font";
				cheatLabel.Text.Value = "\\cheat title";
				cheatLabelContainer.Children.Add(cheatLabel);

				TextElement cheatScrollLabel = new TextElement();
				cheatScrollLabel.FontFile.Value = "Font";
				cheatScrollLabel.Text.Value = "\\scroll for more";
				cheatLabelContainer.Children.Add(cheatScrollLabel);

				Action hideCheat = delegate()
				{
					cheatShown = false;

					showPauseMenu();

					if (cheatAnimation != null)
						cheatAnimation.Delete.Execute();
					cheatAnimation = new Animation
					(
						new Animation.Vector2MoveToSpeed(cheatMenu.AnchorPoint, new Vector2(1, 0.5f), 5.0f),
						new Animation.Set<bool>(cheatMenu.Visible, false)
					);
					this.AddComponent(cheatAnimation);
				};

				UIComponent cheatBack = this.CreateButton("\\back", hideCheat);
				cheatMenu.Children.Add(cheatBack);

				ListContainer cheatList = new ListContainer();
				cheatList.Orientation.Value = ListContainer.ListOrientation.Vertical;

				foreach (KeyValuePair<string, string> item in GameMain.maps)
				{
					string m = item.Key;
					UIComponent button = this.CreateButton(item.Value, delegate()
					{
						hideCheat();
						restorePausedSettings();
						this.currentSave = null;
						this.AddComponent(new Animation
						(
							new Animation.Delay(0.2f),
							new Animation.Set<string>(this.MapFile, m)
						));
					});
					cheatList.Children.Add(button);
				}

				Scroller cheatScroller = new Scroller();
				cheatScroller.Children.Add(cheatList);
				cheatScroller.Add(new Binding<Vector2>(cheatScroller.Size, () => new Vector2(cheatList.Size.Value.X, this.ScreenSize.Value.Y * 0.5f), cheatList.Size, this.ScreenSize));
				cheatMenu.Children.Add(cheatScroller);

				// Cheat button
				UIComponent cheat = this.CreateButton("\\cheat", delegate()
				{
					hidePauseMenu();

					cheatMenu.Visible.Value = true;
					if (cheatAnimation != null)
						cheatAnimation.Delete.Execute();
					cheatAnimation = new Animation(new Animation.Vector2MoveToSpeed(cheatMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f));
					this.AddComponent(cheatAnimation);

					cheatShown = true;
					currentMenu.Value = cheatList;
				});
				cheat.Add(new Binding<bool, string>(cheat.Visible, x => x == GameMain.MenuMap, this.MapFile));
				pauseMenu.Children.Add(cheat);
#endif

				// Controls button
				UIComponent controlsButton = this.CreateButton("\\controls", delegate()
				{
					hidePauseMenu();

					controlsMenu.Visible.Value = true;
					if (controlsAnimation != null)
						controlsAnimation.Delete.Execute();
					controlsAnimation = new Animation(new Animation.Vector2MoveToSpeed(controlsMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f));
					this.AddComponent(controlsAnimation);

					controlsShown = true;
					currentMenu.Value = controlsList;
				});
				pauseMenu.Children.Add(controlsButton);

				// Settings button
				UIComponent settingsButton = this.CreateButton("\\options", delegate()
				{
					hidePauseMenu();

					settingsMenu.Visible.Value = true;
					if (settingsAnimation != null)
						settingsAnimation.Delete.Execute();
					settingsAnimation = new Animation(new Animation.Vector2MoveToSpeed(settingsMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f));
					this.AddComponent(settingsAnimation);

					settingsShown = true;

					currentMenu.Value = settingsMenu;
				});
				pauseMenu.Children.Add(settingsButton);

#if DEVELOPMENT
				// Edit mode toggle button
				UIComponent switchToEditMode = this.CreateButton("\\edit mode", delegate()
				{
					pauseMenu.Visible.Value = false;
					this.EditorEnabled.Value = true;
					this.Paused.Value = false;
					if (pauseAnimation != null)
					{
						pauseAnimation.Delete.Execute();
						pauseAnimation = null;
					}
					IO.MapLoader.Load(this, null, this.MapFile, true);
					this.currentSave = null;
				});
				pauseMenu.Children.Add(switchToEditMode);
#endif

				// Credits window
				Animation creditsAnimation = null;
				bool creditsShown = false;

				ListContainer creditsMenu = new ListContainer();
				creditsMenu.Visible.Value = false;
				creditsMenu.Add(new Binding<Vector2, Point>(creditsMenu.Position, x => new Vector2(0, x.Y * 0.5f), this.ScreenSize));
				creditsMenu.AnchorPoint.Value = new Vector2(1, 0.5f);
				this.UI.Root.Children.Add(creditsMenu);
				creditsMenu.Orientation.Value = ListContainer.ListOrientation.Vertical;

				Container creditsLabelPadding = new Container();
				creditsLabelPadding.PaddingLeft.Value = 8.0f;
				creditsLabelPadding.Opacity.Value = 0.0f;
				creditsMenu.Children.Add(creditsLabelPadding);

				ListContainer creditsLabelContainer = new ListContainer();
				creditsLabelContainer.Orientation.Value = ListContainer.ListOrientation.Vertical;
				creditsLabelPadding.Children.Add(creditsLabelContainer);

				TextElement creditsLabel = new TextElement();
				creditsLabel.FontFile.Value = "Font";
				creditsLabel.Text.Value = "\\credits title";
				creditsLabelContainer.Children.Add(creditsLabel);

				TextElement creditsScrollLabel = new TextElement();
				creditsScrollLabel.FontFile.Value = "Font";
				creditsScrollLabel.Text.Value = "\\scroll for more";
				creditsLabelContainer.Children.Add(creditsScrollLabel);

				Action hideCredits = delegate()
				{
					creditsShown = false;

					showPauseMenu();

					if (creditsAnimation != null)
						creditsAnimation.Delete.Execute();
					creditsAnimation = new Animation
					(
						new Animation.Vector2MoveToSpeed(creditsMenu.AnchorPoint, new Vector2(1, 0.5f), 5.0f),
						new Animation.Set<bool>(creditsMenu.Visible, false)
					);
					this.AddComponent(creditsAnimation);
				};

				UIComponent creditsBack = this.CreateButton("\\back", delegate()
				{
					hideCredits();
				});
				creditsMenu.Children.Add(creditsBack);

				TextElement creditsDisplay = new TextElement();
				creditsDisplay.FontFile.Value = "Font";
				creditsDisplay.Text.Value = this.Credits = File.ReadAllText("attribution.txt");

				Scroller creditsScroller = new Scroller();
				creditsScroller.Add(new Binding<Vector2>(creditsScroller.Size, () => new Vector2(creditsDisplay.Size.Value.X, this.ScreenSize.Value.Y * 0.5f), creditsDisplay.Size, this.ScreenSize));
				creditsScroller.Children.Add(creditsDisplay);
				creditsMenu.Children.Add(creditsScroller);

				// Credits button
				UIComponent credits = this.CreateButton("\\credits", delegate()
				{
					hidePauseMenu();

					creditsMenu.Visible.Value = true;
					if (creditsAnimation != null)
						creditsAnimation.Delete.Execute();
					creditsAnimation = new Animation(new Animation.Vector2MoveToSpeed(creditsMenu.AnchorPoint, new Vector2(0, 0.5f), 5.0f));
					this.AddComponent(creditsAnimation);

					creditsShown = true;
					currentMenu.Value = creditsDisplay;
				});
				credits.Add(new Binding<bool, string>(credits.Visible, x => x == GameMain.MenuMap, this.MapFile));
				pauseMenu.Children.Add(credits);

				// Main menu button
				UIComponent mainMenu = this.CreateButton("\\main menu", delegate()
				{
					showDialog
					(
						"\\quit prompt", "\\quit",
						delegate()
						{
							this.currentSave = null;
							this.MapFile.Value = GameMain.MenuMap;
							this.Paused.Value = false;
						}
					);
				});
				pauseMenu.Children.Add(mainMenu);
				mainMenu.Add(new Binding<bool, string>(mainMenu.Visible, x => x != GameMain.MenuMap, this.MapFile));

				// Exit button
				UIComponent exit = this.CreateButton("\\exit", delegate()
				{
					if (this.MapFile.Value != GameMain.MenuMap)
					{
						showDialog
						(
							"\\exit prompt", "\\exit",
							delegate()
							{
								throw new ExitException();
							}
						);
					}
					else
						throw new ExitException();
				});
				pauseMenu.Children.Add(exit);

				bool saving = false;
				this.input.Bind(this.Settings.QuickSave, PCInput.InputState.Down, delegate()
				{
					if (!saving && !this.Paused && this.MapFile != GameMain.MenuMap && this.player.Value != null && this.player.Value.Active)
					{
						saving = true;
						Container notification = new Container();
						notification.Tint.Value = Microsoft.Xna.Framework.Color.Black;
						notification.Opacity.Value = 0.5f;
						TextElement notificationText = new TextElement();
						notificationText.Name.Value = "Text";
						notificationText.FontFile.Value = "Font";
						notificationText.Text.Value = "\\saving";
						notification.Children.Add(notificationText);
						this.UI.Root.GetChildByName("Notifications").Children.Add(notification);
						this.AddComponent(new Animation
						(
							new Animation.Delay(0.01f),
							new Animation.Execute(this.Save),
							new Animation.Set<string>(notificationText.Text, "\\saved"),
							new Animation.Parallel
							(
								new Animation.FloatMoveTo(notification.Opacity, 0.0f, 1.0f),
								new Animation.FloatMoveTo(notificationText.Opacity, 0.0f, 1.0f)
							),
							new Animation.Execute(notification.Delete),
							new Animation.Execute(delegate()
							{
								saving = false;
							})
						));
					}
				});

				// Escape key
				// Make sure we can only pause when there is a player currently spawned
				// Otherwise we could save the current map without the player. And that would be awkward.
				Func<bool> canPause = delegate()
				{
					if (this.EditorEnabled)
						return false;

					if (this.MapFile.Value == GameMain.MenuMap)
						return !this.Paused; // Only allow pausing, don't allow unpausing

					return true;
				};

				Action togglePause = delegate()
				{
					if (dialog != null)
					{
						dialog.Delete.Execute();
						dialog = null;
						return;
					}
					else if (settingsShown)
					{
						hideSettings();
						return;
					}
					else if (controlsShown)
					{
						hideControls();
						return;
					}
					else if (creditsShown)
					{
						hideCredits();
						return;
					}
					else if (loadSaveShown)
					{
						hideLoadSave();
						return;
					}
#if CHEAT
					else if (cheatShown)
					{
						hideCheat();
						return;
					}
#endif

					if (this.MapFile.Value == GameMain.MenuMap)
					{
						if (currentMenu.Value == null)
							savePausedSettings();
					}
					else
					{
						this.Paused.Value = !this.Paused;

						if (this.Paused)
							savePausedSettings();
						else
							restorePausedSettings();
					}
				};

				this.input.Add(new CommandBinding(input.GetKeyDown(Keys.Escape), () => canPause() || dialog != null, togglePause));
				this.input.Add(new CommandBinding(input.GetButtonDown(Buttons.Start), canPause, togglePause));
				this.input.Add(new CommandBinding(input.GetButtonDown(Buttons.B), () => canPause() || dialog != null, togglePause));

#if !DEVELOPMENT
					// Pause on window lost focus
					this.Deactivated += delegate(object sender, EventArgs e)
					{
						if (!this.Paused && this.MapFile.Value != GameMain.MenuMap && !this.EditorEnabled)
						{
							this.Paused.Value = true;
							savePausedSettings();
						}
					};
#endif
				// Gamepad menu code

				int selected = 0;

				Func<UIComponent, int, int, int> nextMenuItem = delegate(UIComponent menu, int current, int delta)
				{
					int end = menu.Children.Count;
					if (current <= 0 && delta < 0)
						return end - 1;
					else if (current >= end - 1 && delta > 0)
						return 0;
					else
						return current + delta;
				};

				Func<UIComponent, bool> isButton = delegate(UIComponent item)
				{
					return item.Visible && item.GetType() == typeof(Container) && (item.MouseLeftUp.HasBindings || item.MouseScrolled.HasBindings);
				};

				Func<UIComponent, bool> isScrollButton = delegate(UIComponent item)
				{
					return item.Visible && item.GetType() == typeof(Container) && item.MouseScrolled.HasBindings;
				};

				this.input.Add(new NotifyBinding(delegate()
				{
					UIComponent menu = currentMenu;
					if (menu != null && menu != creditsDisplay && this.GamePadConnected)
					{
						foreach (UIComponent item in menu.Children)
							item.Highlighted.Value = false;

						int i = 0;
						foreach (UIComponent item in menu.Children)
						{
							if (isButton(item))
							{
								item.Highlighted.Value = true;
								selected = i;
								break;
							}
							i++;
						}
					}
				}, currentMenu));

				Action<int> moveSelection = delegate(int delta)
				{
					UIComponent menu = currentMenu;
					if (menu != null && dialog == null)
					{
						if (menu == loadSaveList)
							delta = -delta;
						else if (menu == creditsDisplay)
						{
							Scroller scroll = (Scroller)menu.Parent;
							scroll.MouseScrolled.Execute(new Point(), delta * -4);
							return;
						}

						Container button = (Container)menu.Children[selected];
						button.Highlighted.Value = false;

						int i = nextMenuItem(menu, selected, delta);
						while (true)
						{
							UIComponent item = menu.Children[i];
							if (isButton(item))
							{
								selected = i;
								break;
							}

							i = nextMenuItem(menu, i, delta);
						}

						button = (Container)menu.Children[selected];
						button.Highlighted.Value = true;

						if (menu.Parent.Value.GetType() == typeof(Scroller))
						{
							Scroller scroll = (Scroller)menu.Parent;
							scroll.ScrollTo(button);
						}
					}
				};

				Func<bool> enableGamepad = delegate()
				{
					return this.Paused || this.MapFile.Value == GameMain.MenuMap;
				};

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickUp), enableGamepad, delegate()
				{
					moveSelection(-1);
				}));

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadUp), enableGamepad, delegate()
				{
					moveSelection(-1);
				}));

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickDown), enableGamepad, delegate()
				{
					moveSelection(1);
				}));

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadDown), enableGamepad, delegate()
				{
					moveSelection(1);
				}));

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.A), enableGamepad, delegate()
				{
					if (dialog != null)
						dialog.GetChildByName("Okay").MouseLeftUp.Execute(new Point());
					else
					{
						UIComponent menu = currentMenu;
						if (menu != null && menu != creditsDisplay )
						{
							UIComponent selectedItem = menu.Children[selected];
							if (isButton(selectedItem) && selectedItem.Highlighted)
								selectedItem.MouseLeftUp.Execute(new Point());
						}
					}
				}));

				Action<int> scrollButton = delegate(int delta)
				{
					UIComponent menu = currentMenu;
					if (menu != null && menu != creditsDisplay && dialog == null)
					{
						UIComponent selectedItem = menu.Children[selected];
						if (isScrollButton(selectedItem) && selectedItem.Highlighted)
							selectedItem.MouseScrolled.Execute(new Point(), delta);
					}
				};

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickLeft), enableGamepad, delegate()
				{
					scrollButton(-1);
				}));

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadLeft), enableGamepad, delegate()
				{
					scrollButton(-1);
				}));

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.LeftThumbstickRight), enableGamepad, delegate()
				{
					scrollButton(1);
				}));

				this.input.Add(new CommandBinding(this.input.GetButtonDown(Buttons.DPadRight), enableGamepad, delegate()
				{
					scrollButton(1);
				}));

				new CommandBinding(this.MapLoaded, delegate()
				{
					if (this.MapFile.Value == GameMain.MenuMap)
					{
						this.CanSpawn = false;
						this.Renderer.InternalGamma.Value = 0.0f;
						this.Renderer.Brightness.Value = 0.0f;
					}
					else
					{
						this.CanSpawn = true;
						this.Renderer.InternalGamma.Value = GameMain.startGamma;
						this.Renderer.Brightness.Value = 1.0f;
					}

					this.respawnTimer = -1.0f;
					this.Renderer.BlurAmount.Value = 0.0f;
					this.Renderer.Tint.Value = new Vector3(1.0f);
					this.mapJustLoaded = true;
				});

#if DEVELOPMENT
					// Editor instructions
					Container editorMsgBackground = new Container();
					this.UI.Root.Children.Add(editorMsgBackground);
					editorMsgBackground.Tint.Value = Color.Black;
					editorMsgBackground.Opacity.Value = 0.2f;
					editorMsgBackground.AnchorPoint.Value = new Vector2(0.5f, 0.0f);
					editorMsgBackground.Add(new Binding<Vector2, Point>(editorMsgBackground.Position, x => new Vector2(x.X * 0.5f, 30.0f), this.ScreenSize));
					TextElement editorMsg = new TextElement();
					editorMsg.FontFile.Value = "Font";
					editorMsg.Text.Value = "\\editor menu";
					editorMsgBackground.Children.Add(editorMsg);
					this.AddComponent(new Animation
					(
						new Animation.Delay(4.0f),
						new Animation.Parallel
						(
							new Animation.FloatMoveTo(editorMsgBackground.Opacity, 0.0f, 2.0f),
							new Animation.FloatMoveTo(editorMsg.Opacity, 0.0f, 2.0f)
						),
						new Animation.Execute(delegate() { this.UI.Root.Children.Remove(editorMsgBackground); })
					));
#else
					// Main menu

					this.MapFile.Value = GameMain.MenuMap;
					savePausedSettings();
#endif

#if ANALYTICS
				bool editorLastEnabled = this.EditorEnabled;
				new CommandBinding<string>(this.LoadingMap, delegate(string newMap)
				{
					if (this.MapFile.Value != null && !editorLastEnabled)
					{
						this.SessionRecorder.RecordEvent("ChangedMap", newMap);
						this.SaveAnalytics();
					}
					this.SessionRecorder.Reset();
					editorLastEnabled = this.EditorEnabled;
				});
#endif
			}
		}

		protected void saveSettings()
		{
			// Save settings
			using (Stream stream = new FileStream(this.settingsFile, FileMode.Create, FileAccess.Write, FileShare.None))
				new XmlSerializer(typeof(Config)).Serialize(stream, this.Settings);
		}

		private bool mapJustLoaded = false;

		private Vector3 lastEditorPosition;
		private Vector2 lastEditorMouse;
		private string lastEditorSpawnPoint;

		protected override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (this.GamePadState.Value.IsConnected != this.LastGamePadState.Value.IsConnected)
			{
				// Re-bind inputs so their string representations are properly displayed
				// We need to show both PC and gamepad bindings

				foreach (Property<PCInput.PCInputBinding> binding in this.bindings)
					binding.Reset();
			}

			if (this.mapJustLoaded)
			{
				// If we JUST loaded a map, wait one frame for any scripts to execute before we spawn a player
				this.mapJustLoaded = false;
				return;
			}

			// Spawn an editor or a player if needed
			if (this.EditorEnabled)
			{
				this.player.Value = null;
				this.Renderer.InternalGamma.Value = 0.0f;
				this.Renderer.Brightness.Value = 0.0f;
				if (this.editor == null || !this.editor.Active)
				{
					this.editor = Factory.Get<EditorFactory>().CreateAndBind(this);
					FPSInput.RecenterMouse();
					this.editor.Get<Editor>().Position.Value = this.lastEditorPosition;
					this.editor.Get<FPSInput>().Mouse.Value = this.lastEditorMouse;
					this.StartSpawnPoint.Value = this.lastEditorSpawnPoint;
					this.Add(this.editor);
				}
				else
				{
					this.lastEditorPosition = this.editor.Get<Editor>().Position;
					this.lastEditorMouse = this.editor.Get<FPSInput>().Mouse;
				}
			}
			else
			{
				if (this.MapFile.Value == null || !this.CanSpawn)
					return;

				this.editor = null;

				bool setupSpawn = this.player.Value == null || !this.player.Value.Active;

				if (setupSpawn)
					this.player.Value = PlayerFactory.Instance;

				bool createPlayer = this.player.Value == null || !this.player.Value.Active;

				if (createPlayer || setupSpawn)
				{
					if (this.loadingSavedGame)
					{
						this.Renderer.InternalGamma.Value = 0.0f;
						this.Renderer.Brightness.Value = 0.0f;
						this.PlayerSpawned.Execute(this.player);
						this.loadingSavedGame = false;
						this.respawnTimer = 0;
					}
					else
					{
						if (this.respawnTimer <= 0)
						{
							this.AddComponent(new Animation
							(
								new Animation.Parallel
								(
									new Animation.Vector3MoveTo(this.Renderer.Tint, GameMain.startTint, 0.5f),
									new Animation.FloatMoveTo(this.Renderer.InternalGamma, GameMain.startGamma, 0.5f),
									new Animation.FloatMoveTo(this.Renderer.Brightness, 1.0f, 0.5f)
								)
							));
						}

						if (this.respawnTimer > this.RespawnInterval || this.respawnTimer < 0)
						{
							if (createPlayer)
							{
								this.player.Value = Factory.Get<PlayerFactory>().CreateAndBind(this);
								this.Add(this.player);
							}

							bool spawnFound = false;

							PlayerFactory.RespawnLocation foundSpawnLocation = default(PlayerFactory.RespawnLocation);
							Vector3 foundSpawnAbsolutePosition = Vector3.Zero;

							if (string.IsNullOrEmpty(this.StartSpawnPoint.Value))
							{
								// Look for an autosaved spawn point
								ListProperty<PlayerFactory.RespawnLocation> respawnLocations = Factory.Get<PlayerDataFactory>().Instance(this).GetOrMakeListProperty<PlayerFactory.RespawnLocation>("RespawnLocations");
								int supportedLocations = 0;
								while (respawnLocations.Count > 0)
								{
									PlayerFactory.RespawnLocation respawnLocation = respawnLocations[respawnLocations.Count - 1];
									Entity respawnMapEntity = respawnLocation.Map.Target;
									if (respawnMapEntity != null && respawnMapEntity.Active)
									{
										Map respawnMap = respawnMapEntity.Get<Map>();
										Vector3 absolutePos = respawnMap.GetAbsolutePosition(respawnLocation.Coordinate);
										if (respawnMap.Active
											&& respawnMap[respawnLocation.Coordinate].ID != 0
											&& respawnMap.GetAbsoluteVector(respawnMap.GetRelativeDirection(Direction.PositiveY).GetVector()).Y > 0.5f
											&& Agent.Query(absolutePos, 0.0f, 20.0f) == null)
										{
											supportedLocations++;
											DynamicMap dynamicMap = respawnMap as DynamicMap;
											if (dynamicMap == null || absolutePos.Y > respawnLocation.OriginalPosition.Y - 1.0f)
											{
												Map.GlobalRaycastResult hit = Map.GlobalRaycast(absolutePos + new Vector3(0, 1, 0), Vector3.Up, 2);
												if (hit.Map == null)
												{
													// We can spawn here
													spawnFound = true;
													foundSpawnLocation = respawnLocation;
													foundSpawnAbsolutePosition = absolutePos;
												}
											}
										}
									}
									respawnLocations.RemoveAt(respawnLocations.Count - 1);
									if (supportedLocations >= 40 || (foundSpawnAbsolutePosition - this.lastPlayerPosition).Length() > this.RespawnDistance)
										break;
								}
							}

							if (spawnFound)
							{
								// Spawn at an autosaved location
								Vector3 absolutePos = foundSpawnLocation.Map.Target.Get<Map>().GetAbsolutePosition(foundSpawnLocation.Coordinate);
								this.player.Value.Get<Transform>().Position.Value = this.Camera.Position.Value = absolutePos + new Vector3(0, 3, 0);

								FPSInput.RecenterMouse();
								Property<Vector2> mouse = this.player.Value.Get<FPSInput>().Mouse;
								mouse.Value = new Vector2(foundSpawnLocation.Rotation, 0.0f);
							}
							else
							{
								// Spawn at a spawn point
								PlayerSpawn spawn = null;
								Entity spawnEntity = null;
								if (!string.IsNullOrEmpty(this.StartSpawnPoint.Value))
								{
									spawnEntity = this.GetByID(this.StartSpawnPoint);
									if (spawnEntity != null)
										spawn = spawnEntity.Get<PlayerSpawn>();
									this.lastEditorSpawnPoint = this.StartSpawnPoint;
									this.StartSpawnPoint.Value = null;
								}

								if (spawnEntity == null)
								{
									spawn = PlayerSpawn.FirstActive();
									spawnEntity = spawn == null ? null : spawn.Entity;
								}

								if (spawnEntity != null)
									this.player.Value.Get<Transform>().Position.Value = this.Camera.Position.Value = spawnEntity.Get<Transform>().Position;

								if (spawn != null)
								{
									spawn.IsActivated.Value = true;
									FPSInput.RecenterMouse();
									Property<Vector2> mouse = this.player.Value.Get<FPSInput>().Mouse;
									mouse.Value = new Vector2(spawn.Rotation, 0.0f);
								}
							}

							this.AddComponent(new Animation
							(
								new Animation.Parallel
								(
									new Animation.Vector3MoveTo(this.Renderer.Tint, Vector3.One, 0.5f),
									new Animation.FloatMoveTo(this.Renderer.InternalGamma, 0.0f, 0.5f),
									new Animation.FloatMoveTo(this.Renderer.Brightness, 0.0f, 0.5f)
								)
							));
							this.respawnTimer = 0;

							this.PlayerSpawned.Execute(this.player);

							this.RespawnInterval = GameMain.DefaultRespawnInterval;
							this.RespawnDistance = GameMain.DefaultRespawnDistance;
						}
						else
							this.respawnTimer += this.ElapsedTime;
					}
				}
				else
					this.lastPlayerPosition = this.player.Value.Get<Transform>().Position;
			}
		}

		protected override void Draw(GameTime gameTime)
		{
			base.Draw(gameTime);

			if (this.renderTarget != null)
			{
				// We just took a screenshot (i.e. we rendered to a target other than the screen).
				// So make it so we're rendering to the screen again, then copy the render target to the screen.

				this.GraphicsDevice.SetRenderTarget(null);

				SpriteBatch spriteBatch = new SpriteBatch(this.GraphicsDevice);
				spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
				spriteBatch.Draw(this.renderTarget, Vector2.Zero, Color.White);
				spriteBatch.End();

				this.renderTarget = null;

				if (this.saveAfterTakingScreenshot)
					this.Save.Execute();
			}
		}

		public override void ResizeViewport(int width, int height, bool fullscreen, bool applyChanges = true)
		{
			base.ResizeViewport(width, height, fullscreen, applyChanges);
			this.Settings.Fullscreen.Value = fullscreen;
			if (fullscreen)
				this.Settings.FullscreenResolution.Value = new Point(width, height);
			else
				this.Settings.Size.Value = new Point(width, height);
			this.saveSettings();
		}

		public void EnterFullscreen()
		{
			if (!this.graphics.IsFullScreen)
			{
				Point res = this.Settings.FullscreenResolution;
				this.ResizeViewport(res.X, res.Y, true);
			}
		}

		public void ExitFullscreen()
		{
			if (this.graphics.IsFullScreen)
			{
				Point res = this.Settings.Size;
				this.ResizeViewport(res.X, res.Y, false);
			}
		}
	}
}