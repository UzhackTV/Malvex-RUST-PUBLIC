// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
	[Info("GameStoresRUST", "HOUGAN & Sstine & rostov114 & Mevent # GAMESTORES", "1.0.23")]
	public class GameStoresRUST : RustPlugin
	{
		#region Fields

		[PluginReference] private Plugin
			GameStoresWipeBlock = null,
			ImageLibrary = null,
			NoEscape = null;

		private static GameStoresRUST Instance;

#if CARBON
		private ImageDatabaseModule imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif

		private bool initialization, Initialized, maintenance;

		private string ShopURL = "UNDEFINED";
		private int StartBalance;

		private Dictionary<int, Dictionary<ulong, int>> ListTimeOutCommand = new();

		private HashSet<ulong> ListBannedCommandUserID = new();

		private Dictionary<ulong, PlayerBasket> _playerBaskets = new();

		private Timer _checkInstant;

		#region Images

		private string NoImageID = string.Empty, LoadingImageID = string.Empty;

		private const string ImageBucket = "GameStoresRUSTBucket";
		
		private Coroutine _loadingCoroutine;

		#endregion

		#region Web Requests

		private readonly List<Coroutine> _activeCoroutines = new();

		private readonly List<UnityWebRequest> _activeRequests = new();

		private bool SecureConnection = true;

		private Dictionary<string, string> reqHeaders = new()
		{
			["User-Agent"] = "GameStores Plugin"
		};

		private const string
			MainApiLink = "https://apip.gamestores.app/v1/",
			CFApiLink = "https://api.gamestores.app/v1/",
			PayApiLink = "https://pay.gamestores.app/api/";
		
		private string BestApiLink = string.Empty;

		private int errorsReq;
		private const int MaxErrorsReq = 5;

		#endregion Web Requests

		#endregion

		#region Config

		private Configuration _config;

		private class Configuration
		{
			public class APIConfiguration
			{
				[JsonProperty("Store ID")] public string ShopID = "UNDEFINED";

				[JsonProperty("Server ID")] public string ServerID = "UNDEFINED";

				[JsonProperty("Secret key (don't spread it)")]
				public string SecretKey = "UNDEFINED";
			}

			public class InterfaceConfiguration
			{
				[JsonProperty("Enable cart image")] public bool BucketEnable = true;

				[JsonProperty("Enable display of item names")]
				public bool TextShow = true;

				[JsonProperty("Link to cart image (BUCKET - default image)")]
				public string BucketURL = "BUCKET";

				[JsonProperty("Number of items per line")]
				public int ItemOnString = 7;

				[JsonProperty("Number of lines in the interface")]
				public int StringAmount = 3;

				[JsonProperty("Side of one item")] public int ItemSide = 150;

				[JsonProperty("Space between objects")]
				public int ItemMargin = 5;

				[JsonProperty("Position settings cart image")]
				public InterfacePosition BucketPosition = new()
				{
					AnchorMin = "0 1",
					AnchorMax = "0 1",
					OffsetMin = "8 -40",
					OffsetMax = "43 -6"
				};

				[JsonProperty("Background settings")] public BackgroundSettings Background = new()
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1",
					OffsetMin = "0 0",
					OffsetMax = "0 0",
					Material = "assets/content/ui/uibackgroundblur.mat",
					Sprite = string.Empty,
					Image = string.Empty,
					Color = IColor.Create("#000000", 80),
					DisplayType = "Overlay"
				};

				[JsonProperty("Basket title settings")]
				public ButtonSettings BasketTitle = new()
				{
					AnchorMin = "0.3 0.9",
					AnchorMax = "0.7 1",
					OffsetMin = "0 0",
					OffsetMax = "0 0",
					FontSize = 32,
					IsBold = false,
					Align = TextAnchor.MiddleCenter,
					Color = IColor.Create("#FFFFFF", 60),
					ButtonColor = IColor.Create("#000000", 0),
					Sprite = string.Empty,
					Material = string.Empty,
					Image = string.Empty,
					ImageColor = IColor.Create("#FFFFFF")
				};

				[JsonProperty("Basket description settings")]
				public TextSettings BasketDescription = new()
				{
					AnchorMin = "0 0",
					AnchorMax = "1 0",
					OffsetMin = "0 -0",
					OffsetMax = "0 20",
					FontSize = 14,
					IsBold = false,
					Align = TextAnchor.UpperCenter,
					Color = IColor.Create("#FFFFFF", 40)
				};

				[JsonProperty("Help button settings")] public ButtonSettings HelpButton = new()
				{
					AnchorMin = "0.065 0.9",
					AnchorMax = "0.2 1",
					OffsetMin = "0 0",
					OffsetMax = "0 0",
					FontSize = 28,
					IsBold = false,
					Align = TextAnchor.MiddleLeft,
					Color = IColor.Create("#FFFFFF"),
					ButtonColor = IColor.Create("#000000", 0),
					Sprite = string.Empty,
					Material = string.Empty,
					Image = string.Empty,
					ImageColor = IColor.Create("#FFFFFF")
				};

				[JsonProperty(PropertyName = "Block panel settings")]
				public ImageSettings BlockPanel = new()
				{
					AnchorMin = "0 0",
					AnchorMax = "1 0.9",
					OffsetMin = "0 0",
					OffsetMax = "0 0",
					Color = IColor.Create("#000000", 0),
					Sprite = string.Empty,
					Material = string.Empty,
					Image = string.Empty
				};

				[JsonProperty(PropertyName = "Request processing title settings")]
				public TextSettings RequestProcessingTitle = new()
				{
					AnchorMin = "0 0",
					AnchorMax = "1 1.1",
					OffsetMin = "0 0",
					OffsetMax = "0 0",
					Align = TextAnchor.MiddleCenter,
					Color = IColor.Create("#FFFFFF"),
					IsBold = false,
					FontSize = 34
				};

				[JsonProperty(PropertyName = "Store exit button settings")]
				public ButtonSettings StoreExitButton = new()
				{
					AnchorMin = "0.8 0.9", AnchorMax = "0.935 1", OffsetMin = "0 0", OffsetMax = "0 0",
					ButtonColor = IColor.Create("#000000", 0),
					Material = string.Empty,
					Image = string.Empty,
					Sprite = string.Empty,
					Color = IColor.Create("#FFFFFF"),
					Align = TextAnchor.MiddleRight,
					FontSize = 28,
					IsBold = false,
					ImageColor = IColor.Create("#FFFFFF")
				};

				[JsonProperty(PropertyName = "Item background color")]
				public IColor ItemBackgroundColor = IColor.Create("#FFFFFF", 20);

				#region UI.Components

				public class BackgroundSettings : ImageSettings
				{
					[JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
					public string DisplayType;

					public CuiElement Get(string name, string destroyUI, bool needCursor)
					{
						var image = GetImage(DisplayType, name, destroyUI);

						if (needCursor)
							image.Components.Add(new CuiNeedsCursorComponent());

						return image;
					}
				}

				public class ImageSettings : InterfacePosition
				{
					[JsonProperty(PropertyName = "Sprite")]
					public string Sprite;

					[JsonProperty(PropertyName = "Material")]
					public string Material;

					[JsonProperty(PropertyName = "Image")] public string Image;

					[JsonProperty(PropertyName = "Color")] public IColor Color;

					private ICuiComponent GetImage()
					{
						if (!string.IsNullOrEmpty(Image))
						{
							var rawImage = new CuiRawImageComponent
							{
								Png = Instance?.GetImage(Image),
								Color = Color.Get
							};

							if (!string.IsNullOrEmpty(Sprite))
								rawImage.Sprite = Sprite;

							if (!string.IsNullOrEmpty(Material))
								rawImage.Material = Material;

							return rawImage;
						}

						var image = new CuiImageComponent
						{
							Color = Color.Get
						};

						if (!string.IsNullOrEmpty(Sprite))
							image.Sprite = Sprite;

						if (!string.IsNullOrEmpty(Material))
							image.Material = Material;

						return image;
					}

					public CuiElement GetImage(string parent,
						string name = null,
						string destroyUI = null)
					{
						if (string.IsNullOrEmpty(name))
							name = CuiHelper.GetGuid();

						return new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroyUI,
							Components =
							{
								GetImage(),
								GetPosition()
							}
						};
					}
				}

				public class ButtonSettings : TextSettings
				{
					[JsonProperty(PropertyName = "Button Color")]
					public IColor ButtonColor;

					[JsonProperty(PropertyName = "Sprite")]
					public string Sprite;

					[JsonProperty(PropertyName = "Material")]
					public string Material;

					[JsonProperty(PropertyName = "Image")] public string Image;

					[JsonProperty(PropertyName = "Image Color")]
					public IColor ImageColor;

					public List<CuiElement> Get(
						string msg,
						string cmd,
						string parent,
						string name = null,
						string destroyUI = null,
						string close = null)
					{
						if (string.IsNullOrEmpty(name))
							name = CuiHelper.GetGuid();

						var list = new List<CuiElement>();

						var btn = new CuiButtonComponent
						{
							Color = ButtonColor.Get
						};

						if (!string.IsNullOrEmpty(cmd))
							btn.Command = cmd;

						if (!string.IsNullOrEmpty(close))
							btn.Close = close;

						if (!string.IsNullOrEmpty(Sprite))
							btn.Sprite = Sprite;

						if (!string.IsNullOrEmpty(Material))
							btn.Material = Material;

						if (!string.IsNullOrEmpty(Image))
						{
							list.Add(new CuiElement
							{
								Name = name,
								Parent = parent,
								DestroyUi = destroyUI,
								Components =
								{
									new CuiRawImageComponent
									{
										Png = Instance?.GetImage(Image),
										Color = ImageColor.Get
									},
									GetPosition()
								}
							});

							list.Add(new CuiElement
							{
								Parent = name,
								Components =
								{
									btn,
									new CuiRectTransformComponent()
								}
							});
						}
						else
						{
							list.Add(new CuiElement
							{
								Name = name,
								Parent = parent,
								DestroyUi = destroyUI,
								Components =
								{
									btn,
									GetPosition()
								}
							});
						}

						if (!string.IsNullOrEmpty(msg))
							list.Add(new CuiElement
							{
								Parent = name,
								Components =
								{
									GetTextComponent(msg),
									new CuiRectTransformComponent()
								}
							});

						return list;
					}
				}

				public class TextSettings : InterfacePosition
				{
					[JsonProperty(PropertyName = "Font Size")]
					public int FontSize;

					[JsonProperty(PropertyName = "Is Bold?")]
					public bool IsBold;

					[JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
					public TextAnchor Align;

					[JsonProperty(PropertyName = "Color")] public IColor Color;

					protected CuiTextComponent GetTextComponent(string msg)
					{
						return new CuiTextComponent
						{
							Text = msg,
							FontSize = FontSize,
							Font = IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
							Align = Align,
							Color = Color.Get
						};
					}

					public CuiElement GetText(string msg, string parent, string name = null, string destroyUI = null)
					{
						if (string.IsNullOrEmpty(name))
							name = CuiHelper.GetGuid();

						return new CuiElement
						{
							Name = name,
							Parent = parent,
							DestroyUi = destroyUI,
							Components =
							{
								GetTextComponent(msg),
								GetPosition()
							}
						};
					}
				}

				public class InterfacePosition
				{
					public string AnchorMin;

					public string AnchorMax;

					public string OffsetMin;

					public string OffsetMax;

					protected CuiRectTransformComponent GetPosition()
					{
						return new CuiRectTransformComponent
						{
							AnchorMin = AnchorMin,
							AnchorMax = AnchorMax,
							OffsetMin = OffsetMin,
							OffsetMax = OffsetMax
						};
					}
				}

				public class IColor
				{
					[JsonProperty(PropertyName = "Opacity (0 - 100)",
						NullValueHandling = NullValueHandling.Include)]
					public float Alpha;

					[JsonProperty(PropertyName = "HEX", NullValueHandling = NullValueHandling.Include)]
					public string Hex;

					public static IColor Create(string hex, float alpha = 100)
					{
						return new IColor
						{
							Hex = hex,
							Alpha = alpha
						};
					}

					[JsonIgnore] private string _color;

					[JsonIgnore]
					public string Get
					{
						get
						{
							if (string.IsNullOrEmpty(_color))
								UpdateColor();

							return _color;
						}
					}

					public void UpdateColor()
					{
						_color = GetColor();
					}

					private string GetColor()
					{
						if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

						var str = Hex.Trim('#');
						if (str.Length != 6) throw new Exception(Hex);
						var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
						var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
						var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

						return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
					}
				}

				#endregion
			}

			public class PluginConfiguration
			{
				[JsonProperty("Commands to open the store", ObjectCreationHandling = ObjectCreationHandling.Replace)]
				public string[] Commands = {"store", "bucket"};

				[JsonProperty("Enable show joining players in server tags")]
				public bool ShowJoiningPlayers = false;

				[JsonProperty("Load default images over client")]
				public bool LoadSpriteImages = true;

				[JsonProperty("Use Building Blocked?")]
				public bool UseBuildingBlocked = false;

				[JsonProperty("Use Combat Blocked?")] public bool UseCombatBlocked = false;

				[JsonProperty("Use Raid Blocked?")] public bool UseRaidBlocked = false;

				[JsonProperty("Execute instant commands if the player was not on the server?")]
				public bool ExecuteInstantCommandsWhenNotInServer = false;
			}

			[JsonProperty("API configuration")] public APIConfiguration API = new();

			[JsonProperty("Plugin Configuration")] public PluginConfiguration Plugin = new();

			[JsonProperty("UI Configuration")] public InterfaceConfiguration UI = new();

			public VersionNumber ConfigurationVersion = new(2, 0, 0);
		}

		#region Old Config

		private void UpdateConfig(ConfigurationOld _old)
		{
			_config.API.ServerID = _old.API.ServerID;
			_config.API.ShopID = _old.API.ShopID;
			_config.API.SecretKey = _old.API.SecretKey;

			_config.UI.BucketEnable = _old.Interface.BucketEnable;
			_config.UI.TextShow = _old.Interface.TextShow;
			_config.UI.BucketURL = _old.Interface.BucketURL;
			_config.UI.ItemOnString = _old.Interface.ItemOnString;
			_config.UI.StringAmount = _old.Interface.StringAmount;
			_config.UI.ItemSide = _old.Interface.ItemSide;
			_config.UI.ItemMargin = _old.Interface.ItemMargin;

			_config.UI.BucketPosition.AnchorMin = _old.Interface.BucketPosition.AnchorMin;
			_config.UI.BucketPosition.AnchorMax = _old.Interface.BucketPosition.AnchorMax;
			_config.UI.BucketPosition.OffsetMin = _old.Interface.BucketPosition.OffsetMin;
			_config.UI.BucketPosition.OffsetMax = _old.Interface.BucketPosition.OffsetMax;
		}

		private class ConfigurationOld
		{
			public class APIConfiguration
			{
				[JsonProperty("ИД магазина в сервисе")]
				public string ShopID = "UNDEFINED";

				[JsonProperty("ИД сервера в сервисе")] public string ServerID = "UNDEFINED";

				[JsonProperty("Секретный ключ (не распространяйте его)")]
				public string SecretKey = "UNDEFINED";
			}

			public class InterfaceConfiguration
			{
				[JsonProperty("Включить изображение корзины")]
				public bool BucketEnable = true;

				[JsonProperty("Включить отображение названий предметов")]
				public bool TextShow = true;

				[JsonProperty("Ссылка на изображение корзины (BUCKET - стандартное изображение)")]
				public string BucketURL = "BUCKET";

				[JsonProperty("Количество предметов на строке")]
				public int ItemOnString = 7;

				[JsonProperty("Количество строк в интерфейсе")]
				public int StringAmount = 3;

				[JsonProperty("Сторона одного предмета")]
				public int ItemSide = 150;

				[JsonProperty("Отступ между предметами")]
				public int ItemMargin = 5;

				[JsonProperty("Настройки позиции изображение корзины")]
				public Position BucketPosition = new();

				public class Position
				{
					public string AnchorMin = "0 1";
					public string AnchorMax = "0 1";
					public string OffsetMin = "8 -40";
					public string OffsetMax = "43 -6";
				}
			}

			[JsonProperty("Настройки API плагина")]
			public APIConfiguration API = new();

			[JsonProperty("Настройки интерфейса плагина")]
			public InterfaceConfiguration Interface = new();
		}

		#endregion

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config?.API == null || _config.API.SecretKey == "UNDEFINED")
				{
					PrintWarning("Old config?");

					var oldConfig = Config.ReadObject<ConfigurationOld>();
					if (oldConfig?.API != null && oldConfig.API.SecretKey != "UNDEFINED")
					{
						PrintError("Old config! Update configuration!");

						UpdateConfig(oldConfig);

						NextTick(SaveConfig);
						return;
					}

					PrintError("Your configuration is invalid! Create new!");
					LoadDefaultConfig();
				}
			}
			catch (Exception e)
			{
				LogAction(null, "Error reading config, creating one new config!", true, true);
				LoadDefaultConfig();
				Debug.LogException(e);
			}

			NextTick(SaveConfig);
		}

		protected override void LoadDefaultConfig()
		{
			_config = new Configuration();
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(_config);
		}

		#endregion

		#region Hooks

		#region Init / Unload

		private void Init()
		{
			BestApiLink = MainApiLink;

			Instance = this;
		}

		private void OnServerInitialized()
		{
#if !CARBON
			if (ImageLibrary == null)
			{
				if (TryImageCounter < maxImageCheck)
				{
					LogAction(null, "ImageLibrary is not initialized, try again in 1 sec.", true);
					TryImageCounter++;

					timer.In(1, OnServerInitialized);
					return;
				}

				LogAction(null, "Starting without ImageLibrary", true);
			}
			else
#endif
			{
				InitializeImages();
				if (_config.UI.BucketURL.Contains("http")
#if !CARBON
				    && ImageLibrary != null
#endif
				   )
					InitializeBucketImage();
				else
					InitializePlayers();
			}

			LoadHeaderParams();

			if (!InitializeServerSettings())
				return;
			
			RegisterCommands();

			timer.Once(2, FetchShopUrl);

			InitializeTimeoutCommands();
		}

		private void Unload()
		{
			try
			{
				DestroyInstantTimer();

				if (_loadingCoroutine != null) ServerMgr.Instance.StopCoroutine(_loadingCoroutine);

				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, IconLayer);
					CuiHelper.DestroyUi(player, StoreLayer);
				}

				DestroyRequestCoroutines();

				DisposeActiveRequests();
			}
			finally
			{
				Instance = null;
			}
		}

		#endregion

		#region Player

		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;

			if (_config.UI.BucketEnable)
				InitializeIcon(player);
		}

		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null) return;

			DequeueBasketRequest(player.userID);
		}

		#endregion

		#region Server

		private void OnServerInformationUpdated()
		{
			if (!_config.Plugin.ShowJoiningPlayers)
			{
				Unsubscribe(nameof(OnServerInformationUpdated));
				return;
			}

			NextTick(() =>
			{
				if (SteamServer.GameTags.Length > 122)
				{
					LogAction(null, "-----------------------------", true, true);
					LogAction(null, "Perhaps you specified it incorrectly 'server.tags'!", true, true);
					LogAction(null, "The tag of connecting players WILL NOT be added!", true, true);
					LogAction(null, "-----------------------------", true, true);

					Unsubscribe(nameof(OnServerInformationUpdated));
					return;
				}

				var newTags = new List<string>();
				foreach (var tag in SteamServer.GameTags.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries))
				{
					newTags.Add(tag);

					if (tag.Substring(0, 2) == "qp")
						newTags.Add($"jp{SingletonComponent<ServerMgr>.Instance.connectionQueue.Joining}");
				}

				try
				{
					SteamServer.GameTags = string.Join(",", newTags);
				}
				catch (Exception ex)
				{
					Unsubscribe(nameof(OnServerInformationUpdated));
				}
			});
		}

		#endregion

		#endregion

		#region Commands

		private void CmdChatStore(IPlayer cov, string command, string[] args)
		{
			var player = cov?.Object as BasePlayer;
			if (player == null || player.Connection == null || player.IsSleeping())
				return;

			if (maintenance)
			{
				Reply(player, Maintenance);
				return;
			}

			if (!Initialized)
			{
				HandleInitializationError(player);
				return;
			}

			if (args.Length == 1 && args[0].ToLower() == "hide")
			{
				CuiHelper.DestroyUi(player, IconLayer);
				return;
			}
			

			if (CanRequestBasket(player))
				InitializeStore(player, 0);
		}

		[ConsoleCommand("UI_GameStoresRUST")]
		private void CmdConsoleCommand(ConsoleSystem.Arg args)
		{
			var player = args.Player();
			if (player == null) return;

			if (HandleCommandBan(player, args)) return;

			switch (args.Args[0].ToLower())
			{
				case "page":
				{
					var page = args.GetInt(1);

					InitializeStore(player, page, false);
					break;
				}

				case "help":
				{
					ShowHelpUI(player);
					break;
				}

				case "take":
				{
					if (!args.HasArgs(3)) return;

					if (!int.TryParse(args.Args[1], out var index) ||
					    !int.TryParse(args.Args[2], out var basketID)) return;

					ProcessTakeCommand(player, index, basketID);
					break;
				}
			}
		}

		[ConsoleCommand("closemenu")]
		private void CmdCloseMenu(ConsoleSystem.Arg args)
		{
			var player = args.Player();
			if (player == null) return;

			DequeueBasketRequest(player.userID);
		}

		#endregion

		#region Interface

		private const string
			StoreLayer = "UI_GameStoresRUST_Store",
			IconLayer = "UI_GameStoresRUST_Bucket",
			HelpLayer = StoreLayer + ".Help";

		private void InitializeStore(BasePlayer player, int page, bool first = true)
		{
			var container = new CuiElementContainer();

			if (first)
				CreateStoreUiContainer(ref container, player);

			CreateBlockPanel(ref container, player);

			if (first)
				CreateStoreExitButtonUI(player, ref container);

			CuiHelper.AddUi(player, container);

			if (first)
				RequestBasket(player, page);
			else
				ShowBasketItemsUI(player, page);
		}

		private void ShowNotify(BasePlayer player, string text, float destroyTime = 0f)
		{
			CuiHelper.AddUi(player, new CuiElementContainer
			{
				{
					new CuiLabel
					{
						RectTransform =
						{
							AnchorMin = "0 0.80", AnchorMax = "1 0.90", OffsetMax = "0 0"
						},
						Text =
						{
							FadeIn = 1f, Text = text, Align = TextAnchor.UpperCenter,
							Font = "robotocondensed-regular.ttf",
							FontSize = 16
						}
					},
					StoreLayer, StoreLayer + ".Notify", StoreLayer + ".Notify"
				}
			});

			if (destroyTime > 0f)
				timer.In(destroyTime, () => CuiHelper.DestroyUi(player, StoreLayer + ".Notify"));
		}

		private void ShowHelpUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, StoreLayer);

			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				CursorEnabled = true,
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
				Image = {Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat"}
			}, "Overlay", HelpLayer, HelpLayer);

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0.6", AnchorMax = "1 0.78", OffsetMax = "0 0"},
				Text =
				{
					Text = Msg(player, MsgUserManual), Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf", FontSize = 34
				}
			}, HelpLayer);

			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0.3", AnchorMax = "1 0.72", OffsetMax = "0 0"},
				Text =
				{
					Text = Msg(player, MsgUserManualDescription),
					Font = "robotocondensed-regular.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter
				}
			}, HelpLayer);

			var addText = StartBalance > 0 ? Msg(player, MsgUserManualBalance, StartBalance) : string.Empty;
			container.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0.1", AnchorMax = "1 0.48", OffsetMax = "0 0"},
				Text =
				{
					Text = $"{addText}\n{ShopURL.ToUpper()}", Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf", FontSize = 32
				}
			}, HelpLayer);
			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
				Button = {Color = "0 0 0 0", Close = HelpLayer, Command = "chat.say /store"},
				Text = {Text = string.Empty}
			}, HelpLayer);

			CuiHelper.AddUi(player, container);
		}

		#region UI.Components

		private int basketItemsPerPage => _config.UI.ItemOnString * _config.UI.StringAmount;

		private void ShowBasketItemsUI(BasePlayer player,
			int page)
		{
			if (!_playerBaskets.TryGetValue(player.userID, out var playerBasket))
				return;

			var wItems = playerBasket.GetItems(page, basketItemsPerPage);

			var secondContainer = new CuiElementContainer();

			secondContainer.Add(new CuiLabel
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.14", OffsetMax = "0 0"},
				Text = {Text = (page + 1).ToString(), Align = TextAnchor.MiddleCenter, FontSize = 34}
			}, StoreLayer + ".BlockPanel");

			secondContainer.Add(new CuiButton
			{
				RectTransform =
					{AnchorMin = "0.4 0.14", AnchorMax = "0.4 0.14", OffsetMin = "-40 -125", OffsetMax = "125 40"},
				Button = {Color = "0 0 0 0", Command = page > 0 ? $"UI_GameStoresRUST page {page - 1}" : string.Empty},
				Text =
				{
					Text = "<", Color = page > 0 ? "1 1 1 1" : "1 1 1 0.2", Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf", FontSize = 80
				}
			}, StoreLayer + ".BlockPanel");

			secondContainer.Add(new CuiButton
			{
				RectTransform =
					{AnchorMin = "0.6 0.14", AnchorMax = "0.6 0.14", OffsetMin = "-125 -125", OffsetMax = "40 40"},
				Button =
				{
					Color = "0 0 0 0",
					Command = (page + 1) * basketItemsPerPage < playerBasket.GetCount()
						? $"UI_GameStoresRUST page {page + 1}"
						: string.Empty
				},
				Text =
				{
					Text = ">",
					Color = (page + 1) * basketItemsPerPage < playerBasket.GetCount() ? "1 1 1 1" : "1 1 1 0.2",
					Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 80
				}
			}, StoreLayer + ".BlockPanel");

			for (var i = 0; i < basketItemsPerPage; i++)
			{
				UI_RecountPosition(out var xSwitch, out var ySwitch, i, basketItemsPerPage);

				secondContainer.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0.495 0.55",
							AnchorMax = "0.495 0.55",
							OffsetMin = $"{xSwitch} {ySwitch - _config.UI.ItemSide}",
							OffsetMax = $"{xSwitch + _config.UI.ItemSide} {ySwitch}"
						},
						Button = {Color = _config.UI.ItemBackgroundColor.Get, Command = string.Empty},
						Text = {Text = string.Empty}
					}, StoreLayer + ".BlockPanel", StoreLayer + $".BlockPanel.{i}");
			}

			for (var index = 0; index < wItems.Count; index++)
			{
				var item = wItems[index];

				ShowBasketItemUI(player, item, ref secondContainer, index);
			}

			CuiHelper.AddUi(player, secondContainer);
			CuiHelper.DestroyUi(player, StoreLayer + ".BlockPanel.Text");
		}

		private void ShowBasketItemUI(BasePlayer player, WItem item, ref CuiElementContainer secondContainer,
			int index)
		{
			if (item.IsBlueprint) ItemBlueprintImageUI(ref secondContainer, index);

			if (_config.Plugin.LoadSpriteImages && item.IsValid && item.itemDefinition != null)
			{
				secondContainer.Add(new CuiElement
				{
					Parent = StoreLayer + $".BlockPanel.{index}",
					Components =
					{
						new CuiImageComponent {ItemId = item.itemDefinition.itemid},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
					}
				});
			}
			else
			{
				if (!item.ImageUrl.Contains("http"))
					secondContainer.Add(new CuiElement
					{
						Parent = StoreLayer + $".BlockPanel.{index}",
						Components =
						{
							new CuiRawImageComponent {Png = item.ImageUrl},
							new CuiRectTransformComponent
								{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
						}
					});
				else
					secondContainer.Add(new CuiElement
					{
						Parent = StoreLayer + $".BlockPanel.{index}",
						Components =
						{
							new CuiRawImageComponent {Url = item.ImageUrl},
							new CuiRectTransformComponent
								{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
						}
					});
			}

			if (item.Amount > 1)
				secondContainer.Add(new CuiLabel
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 5", OffsetMax = "-5 0"},
						Text =
						{
							Text = "x" + item.Amount, Align = TextAnchor.LowerRight,
							Font = "robotocondensed-regular.ttf", FontSize = 24
						}
					}, StoreLayer + $".BlockPanel.{index}");

			secondContainer.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
					Button =
					{
						Color = "0 0 0 0", Close = StoreLayer + $".BlockPanel.{index}.Open",
						Command =
							$"UI_GameStoresRUST take {index} {item.basketId}"
					},
					Text = {Text = string.Empty}
				}, StoreLayer + $".BlockPanel.{index}", StoreLayer + $".BlockPanel.{index}.Open");

			if (_config.UI.TextShow)
				secondContainer.Add(new CuiLabel
					{
						RectTransform =
							{AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -22.5", OffsetMax = "0 0"},
						Text =
						{
							Text = item.Name, Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-regular.ttf", FontSize = 16, Color = "1 1 1 0.8"
						}
					}, StoreLayer + $".BlockPanel.{index}");

			if (!CanRequestGetItem(Convert.ToInt32(item.basketId)))
			{
				ShowNotify(player, Msg(player, MsgTakeRequestProcessing));

				secondContainer.Add(new CuiButton
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
					Button =
					{
						FadeIn = 1f, Color = "1 1 1 0.2",
						Close = StoreLayer + ".BlockPanel." + index + ".Open",
						Command = $"UI_GameStoresRUST take {index} {item.basketId}"
					},
					Text =
					{
						Text = Msg(player, MsgTakeWait), Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-bold.ttf", Color = "1 1 1 0.4", FontSize = 24
					}
				}, StoreLayer + ".BlockPanel." + index, StoreLayer + ".BlockPanel." + index + ".Open");
			}
		}

		private void ItemBlueprintImageUI(ref CuiElementContainer secondContainer, int index)
		{
			if (_config.Plugin.LoadSpriteImages)
			{
				secondContainer.Add(new CuiElement
				{
					Parent = StoreLayer + $".BlockPanel.{index}",
					Components =
					{
						new CuiImageComponent {ItemId = ItemManager.blueprintBaseDef.itemid},
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
					}
				});
			}
			else
			{
#if CARBON
				var blueprintImageComponent = new CuiRawImageComponent {Png = GetImage("blueprintbase")};
#else
				var blueprintImageComponent = ImageLibrary?.IsLoaded == true
					? new CuiRawImageComponent {Png = GetImage("blueprintbase")}
					: new CuiRawImageComponent {Url = "https://gamestores.app/img/games/rust/blueprintbase.png"};
#endif
				secondContainer.Add(new CuiElement
				{
					Parent = StoreLayer + $".BlockPanel.{index}",
					Components =
					{
						blueprintImageComponent,
						new CuiRectTransformComponent
							{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
					}
				});
			}
		}

		private void CreateStoreExitButtonUI(BasePlayer player, ref CuiElementContainer container)
		{
			container.AddRange(_config.UI.StoreExitButton.Get(Msg(player, MsgExit), "closemenu", StoreLayer,
				close: StoreLayer));
		}

		private void CreateStoreUiContainer(ref CuiElementContainer container, BasePlayer player)
		{
			container.Add(_config.UI.Background.Get(StoreLayer, StoreLayer, true));

			container.AddRange(_config.UI.BasketTitle.Get(Msg(player, MsgBasket), string.Empty, StoreLayer));

			container.Add(_config.UI.BasketDescription.GetText(Msg(player, MsgBasketDescription), StoreLayer));

			container.AddRange(_config.UI.HelpButton.Get(Msg(player, MsgHelp), "UI_GameStoresRUST help",
				StoreLayer));
		}

		private void CreateBlockPanel(ref CuiElementContainer container, BasePlayer player)
		{
			container.Add(_config.UI.BlockPanel.GetImage(StoreLayer, StoreLayer + ".BlockPanel",
				StoreLayer + ".BlockPanel"));

			container.Add(_config.UI.RequestProcessingTitle.GetText(Msg(player, MsgRequestProcessing),
				StoreLayer + ".BlockPanel", StoreLayer + ".BlockPanel.Text"));
		}

		private void InitializeIcon(BasePlayer player)
		{
			UpdateUI(player, container =>
			{
				if (_config.UI.BucketURL.Contains("http"))
					CreateBucketIconWithImageLibrary(ref container);
				else
					CreateBucketIconWithoutImageLibrary(ref container);
			});
		}

		private void CreateBucketIconWithImageLibrary(ref CuiElementContainer container)
		{
#if CARBON
			var bucketImageComponent = new CuiRawImageComponent {Png = GetImage(ImageBucket)};
#else
			var bucketImageComponent = ImageLibrary is {IsLoaded: true}
				? new CuiRawImageComponent {Png = GetImage(ImageBucket)}
				: new CuiRawImageComponent {Url = _config.UI.BucketURL};
#endif

			container.Add(new CuiElement
			{
				Parent = "Overlay",
				Name = IconLayer,
				DestroyUi = IconLayer,
				Components =
				{
					bucketImageComponent,
					new CuiRectTransformComponent
					{
						AnchorMin = _config.UI.BucketPosition.AnchorMin,
						AnchorMax = _config.UI.BucketPosition.AnchorMax,
						OffsetMin = _config.UI.BucketPosition.OffsetMin,
						OffsetMax = _config.UI.BucketPosition.OffsetMax
					}
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
				Button = {Color = "0 0 0 0", Command = "chat.say /store"},
				Text = {Text = string.Empty}
			}, IconLayer);
		}

		private void CreateBucketIconWithoutImageLibrary(ref CuiElementContainer container)
		{
			container.Add(new CuiButton
			{
				RectTransform =
				{
					AnchorMin = _config.UI.BucketPosition.AnchorMin,
					AnchorMax = _config.UI.BucketPosition.AnchorMax,
					OffsetMin = _config.UI.BucketPosition.OffsetMin,
					OffsetMax = _config.UI.BucketPosition.OffsetMax
				},
				Button = {Color = "1 1 1 0.6", Sprite = "assets/icons/open.png", Command = "chat.say /store"},
				Text = {Text = string.Empty}
			}, "Overlay", IconLayer, IconLayer);
		}

		private void UI_RecountPosition(out double xSwitch, out double ySwitch, int count, int max)
		{
			var stringAmount = Math.Ceiling((double) max / _config.UI.ItemOnString);
			var currentString = Math.Floor((double) count / _config.UI.ItemOnString);
			var currentPosition = count % _config.UI.ItemOnString;


			var topYPosition = 0 + (float) stringAmount / 2 * _config.UI.ItemSide +
			                   ((float) stringAmount / 2 - 1) * _config.UI.ItemMargin;
			var topXPosition = 0 - (float) _config.UI.ItemOnString / 2 * _config.UI.ItemSide -
			                   ((float) _config.UI.ItemOnString / 2 - 1) * _config.UI.ItemMargin;

			var curYPosition = topYPosition - currentString * _config.UI.ItemSide - currentString *
				(_config.UI.ItemMargin + (_config.UI.TextShow ? 20 : 0));
			var curXPosition = topXPosition + currentPosition * _config.UI.ItemSide +
			                   currentPosition * _config.UI.ItemMargin;

			xSwitch = curXPosition;
			ySwitch = curYPosition;
		}

		#endregion

		#region UI.Helpers

		private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback)
		{
			if (player == null) return;

			var container = new CuiElementContainer();

			callback?.Invoke(container);

			CuiHelper.AddUi(player, container);
		}

		#endregion

		#endregion

		#region Utils

		#region Player Basket

		#region Classes

		private class PlayerBasket
		{
			public Dictionary<int, WItem> BasketItems = new();

			public PlayerBasket(List<object> data)
			{
				foreach (Dictionary<string, object> product in data)
				{
					if (!product.TryGetValue("basketId", out var basketIdObj) ||
					    !int.TryParse(basketIdObj.ToString(), out var basketId))
						continue;

					var item = new WItem(product);

					BasketItems[basketId] = item;

					var leftTime = Instance?.WB_GetLeftTime(item.productId, item.ItemID, item.IsBlueprint) ?? 0;
					if (leftTime > 0)
					{
						item.IsBlocked = true;
						item.LeftTime = leftTime;
					}
				}
			}

			public void Clear()
			{
				BasketItems.Clear();
			}

			public List<WItem> GetItems(int page, int count)
			{
				return new List<WItem>(BasketItems.Values.Skip(page * count).Take(count));
			}

			public int GetCount()
			{
				return BasketItems.Count;
			}
		}

		#endregion

		#endregion

		#region Blocks

		private bool NE_IsCombatBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsCombatBlocked", player));
		}

		private bool NE_IsRaidBlocked(BasePlayer player)
		{
			return Convert.ToBoolean(NoEscape?.Call("IsRaidBlocked", player));
		}

		private double WB_GetLeftTime(string productId, int itemID, bool isBlueprint)
		{
			return Convert.ToDouble(GameStoresWipeBlock?.Call("GetLeftTime", productId, itemID, isBlueprint));
		}

		#endregion

		#region Plugin Loading

		private void LoadHeaderParams()
		{
			reqHeaders.Add("X-Plugin-Version", Version.ToString());
			reqHeaders.Add("storeId", _config.API.ShopID);
			reqHeaders.Add("secretKey", _config.API.SecretKey);
			reqHeaders.Add("serverId", _config.API.ServerID);
		}
		
		private void RegisterCommands()
		{
			AddCovalenceCommand(_config.Plugin.Commands, nameof(CmdChatStore));
		}

		private bool InitializeServerSettings()
		{
			if (_config.API.ServerID == "UNDEFINED")
			{
				LogAction(null, "Incorrect ServerID, automatic command issuance is disabled");
				PrintError("Incorrect ServerID, receiving auto commands (Products) disabled");
				return false;
			}

			if (_config.API.ServerID != "0")
				StartInstantTimer();

			return true;
		}

		private void InitializeTimeoutCommands()
		{
			ListTimeOutCommand[0] = new Dictionary<ulong, int>();
			ListTimeOutCommand[1] = new Dictionary<ulong, int>();
			ListTimeOutCommand[2] = new Dictionary<ulong, int>();

			var totalTick = 0;
			var tick = 0;

			timer.Repeat(2, 0, () =>
			{
				if (totalTick == 30)
				{
					totalTick = 0;
					ListBannedCommandUserID.Clear();
				}

				ListTimeOutCommand[0].Clear();
				if (tick == 3 || tick == 6) ListTimeOutCommand[1].Clear();

				if (tick == 6)
				{
					tick = 0;
					ListTimeOutCommand[2].Clear();
				}

				tick++;
				totalTick++;
			});
		}

		private void InitializePlayers()
		{
			for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
				OnPlayerConnected(BasePlayer.activePlayerList[i]);
		}

		private IEnumerator WaitForLoad()
		{
			while (Instance?.HasImage(ImageBucket) != true)
			{
				PrintError("Image of bucket is loading!");
				yield return new WaitForSeconds(1);
			}

			PrintWarning("Image of bucket loaded correctly!");
			InitializePlayers();

			yield return 0;
		}

		#endregion

		#region Image Loading

		#region Image Helpers

		private string GetImage(string name)
		{
#if CARBON
			return imageDatabase.GetImageString(name);
#else
			return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
		}

		private void AddImage(string url, string fileName, ulong imageId = 0)
		{
#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				[fileName] = url
			});
#else
			ImageLibrary?.Call("AddImage", url, fileName, imageId);
#endif
		}

		private bool HasImage(string name)
		{
#if CARBON
			return Convert.ToBoolean(imageDatabase.HasImage(name));
#else
			return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name));
#endif
		}

		#endregion

		private const int maxImageCheck = 3;
		private int TryImageCounter;

		private void InitializeImages()
		{
#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				["NONE"] = "https://gspics.org/images/2024/03/29/0NOj7D.png",
				["LOADING"] = "https://gspics.org/images/2024/03/29/0NOxFI.png"
			});
#else
			if (ImageLibrary?.IsLoaded == true)
			{
				NoImageID = GetImage("NONE");
				LoadingImageID = GetImage("LOADING");
			}
#endif
		}

		private void InitializeBucketImage()
		{
			AddImage(_config.UI.BucketURL, ImageBucket);

			_loadingCoroutine = ServerMgr.Instance.StartCoroutine(WaitForLoad());
		}

		#endregion

		#region Take Item

		private void ProcessTake(BasePlayer player, Dictionary<string, object> obj)
		{
			LogAction(player, "Начало обработки товара");
			var itemInfo = new WItem(obj);

			try
			{
				if (!itemInfo.IsValid)
				{
					LogAction(player, $"Ошибка получения предмета: {itemInfo.Name}");
					return;
				}

				if (itemInfo.IsItem)
					ProcessItem(player, itemInfo);

				if (itemInfo.IsCommand)
					ProcessCommand(player, itemInfo);

				if (itemInfo.IsBlueprint)
					ProcessBlueprint(player, itemInfo);
			}
			catch (Exception e)
			{
				HandleItemProcessingError(player, itemInfo, e);
			}
		}

		private void ProcessItem(BasePlayer player, WItem itemInfo)
		{
			LogAction(player, $"Попытка получения предмета: {itemInfo.itemDefinition.shortname} [{itemInfo.Amount}]");

			var item = ItemManager.Create(itemInfo.itemDefinition, itemInfo.Amount);

			if (!player.inventory.GiveItem(item))
			{
				HandleItemGiveError(player, item, itemInfo);
			}
			else
			{
				LogAction(player, "Предмет выдан игроку в инвентарь");
				ShowNotify(player, Msg(player, MsgTakeGiveInventory, itemInfo.itemDefinition.displayName.english));
			}
		}

		private void ProcessInstantCommand(WItem itemInfo)
		{
			ExecuteCommands(null, itemInfo.commands, itemInfo.SteamID.ToString());
		}

		private void ProcessCommand(BasePlayer player, WItem itemInfo)
		{
			if (player == null)
			{
				ExecuteCommands(null, itemInfo.commands, itemInfo.SteamID.ToString());
				return;
			}

			LogAction(player, "Attempting to execute commands");

			ExecuteCommands(player, itemInfo.commands, player.UserIDString, player.displayName);

			ShowNotify(player, Msg(player, MsgTakeGiveCommand, itemInfo.Name));
		}

		private void ExecuteCommands(BasePlayer player, List<object> commands, string steamId, string username = null)
		{
			foreach (string command in commands)
			{
				var selfCommand = ReplacePlaceholders(command, steamId, username);

				ExecuteCommandSequence(selfCommand, player);
			}
		}

		private string ReplacePlaceholders(string command, string steamId, string username = null)
		{
			return command
				.Replace("%steamid%", steamId, StringComparison.OrdinalIgnoreCase)
				.Replace("%username%", username ?? string.Empty, StringComparison.OrdinalIgnoreCase);
		}

		private void ProcessBlueprint(BasePlayer player, WItem itemInfo)
		{
			LogAction(player, $"Попытка получения рецепта {itemInfo.itemDefinition.shortname}");

			var create = ItemManager.Create(ItemManager.blueprintBaseDef);
			create.blueprintTarget = itemInfo.itemDefinition.itemid;

			if (!player.inventory.GiveItem(create))
			{
				HandleItemGiveError(player, create, itemInfo);
			}
			else
			{
				LogAction(player, "Рецепт выдан игроку в инвентарь");

				ShowNotify(player,
					Msg(player, MsgTakeGiveBlueprintInventory, itemInfo.itemDefinition.displayName.english));
			}
		}

		private void ExecuteCommandSequence(string command, BasePlayer player = null)
		{
			LogAction(player, $"Исполнение команды: {command}");
			Server.Command(command);
		}

		#endregion

		#region Instant

		private void StartInstantTimer()
		{
			_checkInstant = timer.Every(60, HandleInstantCommands);
		}

		private void DestroyInstantTimer()
		{
			_checkInstant?.Destroy();
		}

		private void TakeInstant(int basketID)
		{
			LogAction(null, "---------------------------------");
			LogAction(null, $"[AutoCommands] Запрос на получение товара [{basketID}]");

			RequestGetItem(basketID, true);
		}

		#region Handle

		private void HandleInstantCommands()
		{
			Request("baskets.instantCommands", null, (code, response) =>
			{
				switch (code)
				{
					case 200:
						HandleInstantRequestSuccess(response);
						break;
					default:
						LogAction(null, "Ошибка выполнения запроса для выдачи автоматических команд");
						break;
				}
			});
		}

		private void HandleInstantRequestSuccess(string response)
		{
			var firstInfo =
				JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
			if (firstInfo.ContainsKey("result"))
			{
				if (firstInfo["result"].ToString() == "success")
				{
					var data = firstInfo["data"] as List<object>;
					if (data == null)
					{
						LogAction(null, $"Ошибка получения данных от сервера: {response}");
						return;
					}

					var i = 1;
					foreach (Dictionary<string, object> product in data)
					{
						if (!product.TryGetValue("basketId", out var basketId)) continue;

						var basketID = Convert.ToInt32(basketId);

						if (!_config.Plugin.ExecuteInstantCommandsWhenNotInServer)
							if (product.TryGetValue("steamId", out var value))
							{
								var steamId = Convert.ToString(value);
								if (!string.IsNullOrEmpty(steamId) && steamId.IsSteamId() &&
								    covalence.Players.FindPlayerById(steamId) == null)
									continue;
							}

						timer.In(i, () => TakeInstant(basketID));

						i++;
					}

					return;
				}

				if (firstInfo["result"].ToString() == "fail")
					if (firstInfo.TryGetValue("message", out var value))
					{
						var message = Convert.ToString(value);
						if (!string.IsNullOrEmpty(message))
						{
							LogAction(null, $"Ошибка получения данных от сервера: {message}", true, true);
							return;
						}
					}
			}

			LogAction(null, "Ошибка получения данных от сервера");
		}

		private void HandleTakeInstantResponse(int code, string response, int basketID)
		{
			switch (code)
			{
				case 0:
					LogAction(null, $"[AutoCommands] API не ответило на запрос: {basketID}");
					PrintError("Api does not responded to a request");
					break;
				case 200:
					HandleTakeInstantSuccess(response, basketID);
					break;
				case 404:
					LogAction(null, $"[AutoCommands] Api не ответило на запрос: {basketID}");
					LogAction(null, "Response code: 404, please check your configurations", true);
					break;
			}
		}

		private void HandleTakeInstantSuccess(string response, int basketID)
		{
			var responseData =
				JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
			if (responseData == null)
			{
				LogAction(null, $"[AutoCommands] Ошибка получения данных от сервера: {response}");
				return;
			}

			if (!responseData.TryGetValue("data", out var value))
			{
				LogAction(null, $"[AutoCommands] Ошибка получения товара, отсутствует Data [{basketID}]");
				return;
			}

			if (value is not Dictionary<string, object> data) return;

			var itemInfo = new WItem(data);
			if (!itemInfo.IsValid)
			{
				LogAction(null,
					$"[AutoCommands] Ошибка получения товара [{basketID}], отсутствует информация о товаре [{itemInfo.Name}]");
				return;
			}

			if (covalence?.Players?.FindPlayerById(itemInfo.SteamID.ToString()) == null)
			{
				LogAction(null,
					$"[AutoCommands] Ошибка получения товара [{basketID}], отсутствует игрок [{itemInfo.SteamID}]");
				return;
			}

			var leftTime = WB_GetLeftTime(itemInfo.productId,
				itemInfo.ItemID, itemInfo.IsBlueprint);
			if (leftTime > 0)
			{
				LogAction(null,
					$"[AutoCommands] Ошибка получения товара [{basketID}], на предмет действует блокировка после вайпа [{leftTime}] секунд");
				return;
			}

			Request("baskets.makeIssued", new Dictionary<string, string>
			{
				{"steamId", itemInfo.SteamID.ToString()},
				{"basketId", basketID.ToString()}
			}, (code, newResponse) =>
			{
				if (code != 200 || JsonConvert.DeserializeObject<JObject>(newResponse)["result"].ToString() !=
				    "success")
				{
					LogAction(null,
						$"[AutoCommands] Ошибка выполнения запроса для выдачи товара! code: {code}, response: {newResponse}");
					return;
				}

				if (!itemInfo.IsCommand)
				{
					LogAction(null, $"[AutoCommands] Ошибка получения (товар не является командой) [{basketID}]");
					return;
				}

				ProcessInstantCommand(itemInfo);
			});
		}

		#endregion

		#endregion

		#region Handle Response

		#region Basket

		private void HandleBasketUnavailable(BasePlayer player)
		{
			UpdateUI(player, secondContainer =>
			{
				secondContainer.Add(new CuiLabel
				{
					RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
					Text =
					{
						Text = Msg(player, MsgBasketUnavailable), Align = TextAnchor.MiddleCenter,
						Font = "robotocondensed-regular.ttf", FontSize = 20
					}
				}, StoreLayer + ".BlockPanel", StoreLayer + ".BlockPanel.Text", StoreLayer + ".BlockPanel.Text");
			});
		}

		private void HandleBasketResponse(BasePlayer player, string response, int page)
		{
#if !TESTING
			if (player == null) return;
#endif

			var firstInfo =
				JsonConvert.DeserializeObject<Dictionary<string, object>>(response,
					new KeyValuesConverter());
			if (!firstInfo.TryGetValue("result", out var value)) return;

			if (value.ToString() == "fail")
			{
				if ((firstInfo.ContainsKey("playerNotFound") && Convert.ToBoolean(firstInfo["playerNotFound"])) ||
				    (firstInfo.ContainsKey("message") &&
				     firstInfo["message"]?.ToString()?.Contains("Игрок не найден") == true))
					UpdateUI(player, secondContainer =>
					{
						secondContainer.Add(new CuiLabel
							{
								RectTransform = {AnchorMin = "0 0.1", AnchorMax = "1 1", OffsetMax = "0 0"},
								Text =
								{
									Text = Msg(player, MsgBasketNoAuth), Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf", FontSize = 20
								}
							}, StoreLayer + ".BlockPanel", StoreLayer + ".BlockPanel.Text",
							StoreLayer + ".BlockPanel.Text");
					});
			}
			else
			{
				var data = firstInfo["data"] as List<object>;
				if (data == null)
				{
					PrintError("Unkown error #1");
					CuiHelper.DestroyUi(player, StoreLayer);
					return;
				}

				if (data.Count == 0)
				{
					UpdateUI(player, secondContainer =>
					{
						secondContainer.Add(new CuiLabel
							{
								RectTransform = {AnchorMin = "0 0.1", AnchorMax = "1 1", OffsetMax = "0 0"},
								Text =
								{
									Text = Msg(player, MsgBasketEmpty), Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-regular.ttf", FontSize = 34
								}
							}, StoreLayer + ".BlockPanel", StoreLayer + ".BlockPanel.Text",
							StoreLayer + ".BlockPanel.Text");
					});
					return;
				}

				_playerBaskets.Remove(player.userID);

				_playerBaskets[player.userID] = new PlayerBasket(data);

				ShowBasketItemsUI(player, page);
			}
		}

		#endregion

		#region Item

		private void HandleItemGiveError(BasePlayer player, Item item, WItem itemInfo)
		{
			LogAction(player,
				$"У игрока не было места для получения предмета, предмет выброшен {itemInfo.itemDefinition.shortname} [{itemInfo.Amount} {player.transform.position}]");

			item.Drop(player.GetDropPosition(), player.GetDropVelocity());
			ShowNotify(player, Msg(player, MsgTakeGiveFeet, item.info.displayName.english));
		}

		private void HandleItemProcessingError(BasePlayer player, WItem itemInfo, Exception e)
		{
			LogAction(player, $"Ошибка получения товара [{itemInfo.basketId}][{itemInfo.Name}]");
			LogToFile("!Errors", $"{DateTime.Now.ToShortTimeString()}| Error: '{e}'", this);
			PrintError($"Error receiving product [{itemInfo.basketId}], info saved to log");
		}

		#endregion

		#region Commands

		private void HandleInitializationError(BasePlayer player)
		{
			player.ChatMessage(Msg(player, MsgPluginnotinitialized));
		}

		#endregion

		private void HandleFetchShopUrlResponse(int code, string response)
		{
			LogAction(null, "-----------------------------", true);
			LogAction(null, $" GameStores {Version} (c) 2024", true);

			try
			{
				if (response.Length < 1)
				{
					LogToFile("!Errors", $"{DateTime.Now.ToShortTimeString()}| Response: '{response}'", this);
					LogAction(null, " Incorrect API response! Saved to log!", true, true);
					LogAction(null, "-----------------------------", true);

					SetReserveApiLink();

					initialization = false;
					return;
				}

				var infoResult = JsonConvert.DeserializeObject<InfoResult>(response);
				if (infoResult?.data == null)
				{
					LogAction(null, "     Wrong Secret Key", true, true);
					LogAction(null, "-----------------------------", true);

					Puts($"result: {response}");
					
					initialization = false;
					return;
				}

				if (_config.API.ServerID != "0" && !infoResult.data.servers.Contains(_config.API.ServerID))
				{
					LogAction(null, "     Wrong Server ID", true, true);
					LogAction(null, "-----------------------------", true);

					initialization = false;
					return;
				}

				ShopURL = infoResult.data.link;
				StartBalance = infoResult.data.defaultBalance;

				LogAction(null, "      Initialized - OK", true);
				LogAction(null, "-----------------------------", true);

				foreach (var pl in BasePlayer.activePlayerList) OnPlayerConnected(pl);

				Initialized = true;
				initialization = false;
			}
			catch (JsonException e)
			{
				LogToFile("Errors", $"{DateTime.Now.ToShortTimeString()}| JsonError | Response: '{response}'", this);

				LogAction(null, " JSON Error! Saved to log!", true, true);
				LogAction(null, "-----------------------------", true);

				SetReserveApiLink();

				initialization = false;
			}
		}

		#endregion

		#region Bans

		private bool HandleCommandBan(BasePlayer player, ConsoleSystem.Arg args)
		{
			if (ListBannedCommandUserID.Contains(player.userID))
			{
				var msg = Msg(player, MsgPlayerFloodBlock);

				player.ChatMessage(msg);

				ShowNotify(player, msg);

				if (args.GetString(0) == "take")
				{
					var index = args.GetInt(1);
					var basketID = args.GetInt(2);

					UpdateUI(player, container =>
					{
						container.Add(new CuiButton
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
								Button =
								{
									FadeIn = 1f, Color = "1 0.5 0.5 0.2",
									Close = StoreLayer + ".BlockPanel." + index + ".Open",
									Command = $"UI_GameStoresRUST take {index} {basketID}"
								},
								Text =
								{
									Text = Msg(player, MsgTakeGiveError), Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-bold.ttf", Color = "1 0.7 0.7 1", FontSize = 22
								}
							}, StoreLayer + ".BlockPanel." + index,
							StoreLayer + ".BlockPanel." + index + ".Open",
							StoreLayer + ".BlockPanel." + index + ".Open");
					});
				}

				return true;
			}

			IncrementCommandTimeout(player, 0);
			IncrementCommandTimeout(player, 1);
			IncrementCommandTimeout(player, 2);

			if (IsCommandBanned(player)) ListBannedCommandUserID.Add(player.userID);

			if (!args.HasArgs())
			{
				player.SendConsoleCommand("chat.say /store");
				return true;
			}

			return false;
		}

		private void IncrementCommandTimeout(BasePlayer player, int index)
		{
			if (!ListTimeOutCommand[index].ContainsKey(player.userID))
				ListTimeOutCommand[index][player.userID] = 1;
			else
				ListTimeOutCommand[index][player.userID]++;
		}

		private bool IsCommandBanned(BasePlayer player)
		{
			return ListTimeOutCommand[0][player.userID] >= 10 ||
			       ListTimeOutCommand[1][player.userID] >= 20 ||
			       ListTimeOutCommand[2][player.userID] >= 30;
		}

		#endregion

		#region Command Helpers

		private void ProcessTakeCommand(BasePlayer player, int index, int basketID)
		{
			if (!_playerBaskets.TryGetValue(player.userID, out var playerBasket) ||
			    !playerBasket.BasketItems.TryGetValue(basketID, out var item))
			{
				ShowNotify(player,  Msg(player, MsgItemnotfound));
				return;
			}
			
			if (_config.Plugin.UseBuildingBlocked && player.IsBuildingBlocked())
			{
				ShowNotify(player, Msg(player, MsgBuildingBlocked));
				return;
			}

			if (_config.Plugin.UseCombatBlocked && NE_IsCombatBlocked(player))
			{
				ShowNotify(player, Msg(player, MsgCombatBlocked));
				return;
			}

			if (_config.Plugin.UseRaidBlocked && NE_IsRaidBlocked(player))
			{
				ShowNotify(player, Msg(player, MsgRaidBlocked));
				return;
			}
			
			if (player.IsDead() || player.IsWounded())
			{
				ShowNotify(player, Msg(player, MsgDeadOrWounded));
				return;
			}
			
			if (item.IsBlocked)
			{
				var time = FormatTime(player, item.LeftTime);

				ShowNotify(player, Msg(player, MsgTakeItemBlocked, time));
				
				UpdateUI(player, container =>
				{
					container.Add(new CuiButton
						{
							RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
							Button =
							{
								FadeIn = 1f, Color = "1 0.5 0.5 0.2",
								Close = StoreLayer + ".BlockPanel." + index + ".Open",
								Command = $"UI_GameStoresRUST take {index} {basketID}"
							},
							Text =
							{
								Text = Msg(player, MsgTakeGiveBlocked, time), Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf", Color = "1 0.7 0.7 1", FontSize = 22
							}
						}, StoreLayer + ".BlockPanel." + index,
						StoreLayer + ".BlockPanel." + index + ".Open",
						StoreLayer + ".BlockPanel." + index + ".Open");
				});
				return;
			}

			ShowNotify(player, Msg(player, MsgTakeRequestProcessing));

			if (!CanRequestGetItem(basketID))
			{
				UpdateUI(player, container =>
				{
					container.Add(new CuiButton
						{
							RectTransform =
							{
								AnchorMin = "0 0", AnchorMax = "1 1"
							},
							Button =
							{
								Color = "1 1 1 0.2", Close = StoreLayer + ".BlockPanel." + index + ".Open",
								Command = $"UI_GameStoresRUST take {index} {basketID}"
							},
							Text =
							{
								Text = Msg(player, MsgTakeWait), Align = TextAnchor.MiddleCenter,
								Font = "robotocondensed-bold.ttf", Color = "1 1 1 0.4", FontSize = 24
							}
						}, StoreLayer + ".BlockPanel." + index, 
						StoreLayer + ".BlockPanel." + index + ".Open", 
						StoreLayer + ".BlockPanel." + index + ".Open");
				});
				return;
			}

			UpdateUI(player, container =>
			{
				container.Add(new CuiButton
					{
						RectTransform =
						{
							AnchorMin = "0 0", AnchorMax = "1 1"
						},
						Button =
						{
							Color = "1 1 1 0.2", Close = StoreLayer + ".BlockPanel." + index + ".Open",
							Command = $"UI_GameStoresRUST take {index} {basketID}"
						},
						Text =
						{
							Text = Msg(player, MsgTakeWait), Align = TextAnchor.MiddleCenter,
							Font = "robotocondensed-bold.ttf", Color = "1 1 1 0.4", FontSize = 24
						}
					}, StoreLayer + ".BlockPanel." + index, 
					StoreLayer + ".BlockPanel." + index + ".Open", 
					StoreLayer + ".BlockPanel." + index + ".Open");
			});

			LogAction(null, "---------------------------------");
			LogAction(player, $"Запрос на получение предмета: {basketID}");

			RequestGetItem(player, basketID, index);
		}

		private void HandleTakeResponse(BasePlayer player, int index, int basketID, int responseCode, string response)
		{
			switch (responseCode)
			{
				case 0:
					LogAction(player, $"API не ответило на запрос: {basketID}");
					PrintError("Api does not responded to a request");

					if (player != null)
					{
						player.ChatMessage(Msg(player, MsgUnexpectederror));
						CuiHelper.DestroyUi(player, StoreLayer);
					}

					break;
				case 200:
					var data =
						JsonConvert.DeserializeObject<Dictionary<string, object>>(response,
							new KeyValuesConverter());
					if (data == null || !data.ContainsKey("data"))
					{
						LogAction(player, $"Ошибка получения товара, отсутствует Data [{basketID}]");

						ShowNotify(player, Msg(player, MsgTakeGiveErrorNotify));

						UpdateUI(player, container =>
						{
							container.Add(new CuiButton
								{
									RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
									Button =
									{
										FadeIn = 1f, Color = "1 0.5 0.5 0.2",
										Close = StoreLayer + ".BlockPanel." + index + ".Open",
										Command = $"UI_GameStoresRUST take {index} {basketID}"
									},
									Text =
									{
										Text = Msg(player, MsgTakeGiveError), Align = TextAnchor.MiddleCenter,
										Font = "robotocondensed-bold.ttf", Color = "1 0.7 0.7 1", FontSize = 22
									}
								}, StoreLayer + ".BlockPanel." + index,
								StoreLayer + ".BlockPanel." + index + ".Open",
								StoreLayer + ".BlockPanel." + index + ".Open");
						});
						return;
					}

					LogAction(player, $"Товар отмечен полученным [{basketID}]");

					Request("baskets.makeIssued", new Dictionary<string, string>
						{
							{"steamId", player.UserIDString},
							{"basketId", basketID.ToString()}
						},
						(code, newResponse) =>
						{
							if (code != 200 ||
							    JsonConvert.DeserializeObject<JObject>(newResponse)["result"].ToString() !=
							    "success")
							{
								ShowNotify(player, Msg(player, MsgTakeGiveErrorNotify));

								UpdateUI(player, container =>
								{
									container.Add(new CuiButton
										{
											RectTransform =
												{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
											Button =
											{
												FadeIn = 1f, Color = "1 0.5 0.5 0.2",
												Close = StoreLayer + ".BlockPanel." + index + ".Open",
												Command = $"UI_GameStoresRUST take {index} {basketID}"
											},
											Text =
											{
												Text = Msg(player, MsgTakeGiveError),
												Align = TextAnchor.MiddleCenter,
												Font = "robotocondensed-bold.ttf", Color = "1 0.7 0.7 1",
												FontSize = 22
											}
										}, StoreLayer + ".BlockPanel." + index,
										StoreLayer + ".BlockPanel." + index + ".Open",
										StoreLayer + ".BlockPanel." + index + ".Open");
								});
								return;
							}

							ProcessTake(player, data["data"] as Dictionary<string, object>);
						}, player);

					UpdateUI(player, container =>
					{
						container.Add(new CuiButton
							{
								RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
								Button = {FadeIn = 1f, Color = "0.5 1 0.5 0.2"},
								Text =
								{
									Text = Msg(player, MsgTakeGiveSuccess), Align = TextAnchor.MiddleCenter,
									Font = "robotocondensed-bold.ttf", Color = "0.7 1 0.7 1", FontSize = 22
								}
							}, StoreLayer + ".BlockPanel." + index, StoreLayer + ".BlockPanel." + index + ".Open",
							StoreLayer + ".BlockPanel." + index + ".Open");
					});

					if (_playerBaskets.ContainsKey(player.userID) &&
					    _playerBaskets[player.userID].BasketItems.ContainsKey(basketID))
						_playerBaskets[player.userID].BasketItems.Remove(basketID);
					break;
				case 404:
					LogAction(player, $"Сайт не ответил на запрос: {basketID}");
					player.ChatMessage(Msg(player, MsgUnexpectederror));
					CuiHelper.DestroyUi(player, StoreLayer);
					LogAction(null, "Response code: 404, please check your configurations", true);
					break;
			}
		}

		#endregion

		private void FetchShopUrl()
		{
			initialization = true;
			timer.In(5, () => { initialization = false; });

			Request($"store.pluginInfo", null, HandleFetchShopUrlResponse);
		}

		#region Networking

		private const int timeout = 30; // 30 seconds

		private void Request(string method, Dictionary<string, string> args, Action<int, string> callback,
			BasePlayer player = null)
		{
			if (!SecureConnection && BestApiLink.Contains("https://"))
				BestApiLink = BestApiLink.Replace("https://", "http://");

			string queryParams = $"?store_id={_config.API.ShopID}&server_id={_config.API.ServerID}";
            _activeCoroutines.Add(ServerMgr.Instance.StartCoroutine(WebPostRequestAsync(BestApiLink + method + queryParams, args,
				(code, response) =>
				{
					if (Instance == null) return;

					var anyError = false;

					switch (code)
					{
						case 200:
							// success
							break;
						case 404:
							LogAction(null, "Please check your configuration! [404] #2", true);
							break;
						default:
						{
							anyError = true;

							LogAction(null, "Time out waiting for GS API #1");

							errorsReq++;

							if (errorsReq >= MaxErrorsReq)
							{
								errorsReq = 0;

								TryChangeLink();
							}

							break;
						}
					}

					if (SecureConnection && response.Contains("The authentication or decryption has failed."))
					{
						LogAction(null,
							"HTTPS request is broken (broken CA certificate?). Changed to non secure connection!", true,
							true);

						SecureConnection = false;
						BestApiLink = BestApiLink.Replace("https://", "http://");

						Request(method, args, callback, player);
						return;
					}

					if (Initialized && !anyError)
						try
						{
							var maintenanceResult = JsonConvert.DeserializeObject<MaintenanceResult>(response);
							if (maintenanceResult is {maintenance: true})
							{
								LogAction(null, "-----------------------------", true, true);
								LogAction(null, $"Maintenance: {maintenanceResult.message} ", true, true);
								LogAction(null, "-----------------------------", true, true);

								if (player != null) CuiHelper.DestroyUi(player, StoreLayer);

								maintenance = true;
							}
							else
							{
								maintenance = false;
							}
						}
						catch (Exception ex)
						{
							LogToFile("Errors",
								$"{DateTime.Now.ToShortTimeString()}| JsonError | Response: '{response}'",
								this);
							LogAction(null, " JSON Error! Saved to log!", true, true);

							return;
						}

					callback?.Invoke(code, response);
				})));
		}

		private void RequestToPay(string method, Dictionary<string, string> args, Action<int, string> callback)
        {
            _activeCoroutines.Add(ServerMgr.Instance.StartCoroutine(WebPostRequestAsync(PayApiLink + method, args,
                (code, response) =>
                {
                    if (Instance == null) return;

                    var anyError = false;
                    switch (code)
                    {
                        case 200:
                            // success
                            break;
                        case 400:
                            // success
                            break;
                        case 404:
                            LogAction(null, "Please check your configuration! [404] #2", true);
                            break;
                        default:
                            {
                                anyError = true;
                                LogAction(null, "Time out waiting for GS API #1");
                                break;
                            }
                    }
                    if (Initialized && !anyError)
                        try
                        {
                            var maintenanceResult = JsonConvert.DeserializeObject<MaintenanceResult>(response);
                            if (maintenanceResult is { maintenance: true })
                            {
                                LogAction(null, "-----------------------------", true, true);
                                LogAction(null, $"Maintenance: {maintenanceResult.message} ", true, true);
                                LogAction(null, "-----------------------------", true, true);

                                maintenance = true;
                            }
                            else
                            {
                                maintenance = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToFile("Errors",
                                $"{DateTime.Now.ToShortTimeString()}| Error req to pay | JsonError | Response: '{response}'",
                                this);
                            LogAction(null, " Error req to pay! Saved to log!", true, true);

                            return;
                        }

                    callback?.Invoke(code, response);
                })));
        }

		private void CheckRequest(string url, string method, Action<int, string> callback)
		{
			string queryParams = $"?store_id={_config.API.ShopID}&server_id={_config.API.ServerID}";
            _activeCoroutines.Add(ServerMgr.Instance.StartCoroutine(WebPostRequestAsync(url + method + queryParams, null,
				(code, response) => callback?.Invoke(code, response))));
		}

		private IEnumerator WebPostRequestAsync(string url, Dictionary<string, string> bodyFields,
			Action<int, string> onRequestComplete = null)
		{
			using var webRequest = UnityWebRequest.Post(url, bodyFields ?? new Dictionary<string, string>());

			webRequest.timeout = timeout;

			_activeRequests.Add(webRequest);

			foreach (var check in reqHeaders)
				webRequest.SetRequestHeader(check.Key, check.Value);

			yield return webRequest.SendWebRequest();

			_activeRequests.Remove(webRequest);

			onRequestComplete?.Invoke((int) webRequest.responseCode,
				webRequest.downloadHandler != null ? webRequest.downloadHandler.text : string.Empty);
		}

		#region Networking Helpers

		private void DestroyRequestCoroutines()
		{
			foreach (var coroutine in _activeCoroutines)
				if (coroutine != null)
					ServerMgr.Instance.StopCoroutine(coroutine);

			_activeCoroutines.Clear();
		}

		private void DisposeActiveRequests()
		{
			for (var index = _activeRequests.Count - 1; index >= 0; index--)
				try
				{
					_activeRequests[index]?.Dispose();
				}
				catch
				{
					// ignored
				}
		}

		#endregion

		#endregion

		private void LogAction(BasePlayer player, string text, bool printToConsole = false, bool printError = false)
		{
#if TESTING
			// SayDebug($"[LogAction] {text}");
#endif

			LogToFile("!global",
				$"{DateTime.Now.ToShortTimeString()} {(player != null ? "[" + player.userID + "]" : " ")}{text}", this);

			if (printToConsole)
				if (printError)
					PrintError($"{text}");
				else
					PrintWarning($"{text}");
			if (player != null)
				LogPlayerAction(player, text);
		}

		private void LogPlayerAction(BasePlayer player, string text)
		{
			LogToFile($"{player.userID}",
				$"{DateTime.Now.ToShortTimeString()} {text}", this);
		}

		private static double CurrentTime()
		{
			return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
		}

		#endregion

		#region Lang

		private const string
			MsgTimeDAYS = "TIME.DAYS",
			MsgTimeHOURS = "TIME.HOURS",
			MsgTimeMINUTES = "TIME.MINUTES",
			MsgTimeSECONDS = "TIME.SECONDS",
			MsgBuildingBlocked = "MsgBuildingBlocked",
			MsgCombatBlocked = "MsgCombatBlocked",
			MsgRaidBlocked = "MsgRaidBlocked",
			MsgDeadOrWounded = "MsgDeadOrWounded",
			Maintenance = "Maintenance",
			MsgPluginnotinitialized = "PluginNotInitialized",
			MsgItemnotfound = "ItemNotFound",
			MsgUnexpectederror = "UnexpectedError",
			MsgWaitpreviousaction = "WaitPreviousAction",
			MsgBasket = "BASKET",
			MsgExit = "EXIT",
			MsgBasketDescription = "BASKET.DESCRIPTION",
			MsgBasketEmpty = "BASKET.EMPTY",
			MsgHelp = "HELP",
			MsgRequestProcessing = "REQUEST.PROCESSING",
			MsgBasketUnavailable = "BASKET.UNAVAILABLE",
			MsgBasketNoAuth = "BASKET.NO.AUTH",
			MsgUserManual = "USER.MANUAL",
			MsgUserManualDescription = "USER.MANUAL.DESCRIPTION",
			MsgUserManualBalance = "USER.MANUAL.BALANCE",
			MsgTakeItemBlocked = "TAKE.ITEM.BLOCKED",
			MsgTakeRequestProcessing = "TAKE.REQUEST.PROCESSING",
			MsgTakeLastRequestProcessing = "TAKE.LAST.REQUEST.PROCESSING",
			MsgTakeWait = "TAKE.WAIT",
			MsgTakeGiveBlocked = "TAKE.GIVE.BLOCKED",
			MsgTakeGiveError = "TAKE.GIVE.ERROR",
			MsgTakeGiveSuccess = "TAKE.GIVE.SUCCESS",
			MsgTakeGiveErrorNotify = "TAKE.GIVE.ERROR.NOTIFY",
			MsgTakeGiveFeet = "TAKE.GIVE.FEET",
			MsgTakeGiveInventory = "TAKE.GIVE.INVENTORY",
			MsgTakeGiveCommand = "TAKE.GIVE.COMMAND",
			MsgTakeGiveBlueprintFeet = "TAKE.GIVE.BLUEPRINT.FEET",
			MsgTakeGiveBlueprintInventory = "TAKE.GIVE.BLUEPRINT.INVENTORY",
			MsgManyRequests = "MANY.REQUESTS",
			MsgPlayerFloodBlock = "PlayerFloodBlock";

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{
					Maintenance,
					"We are currently undergoing technical work, please try later."
				},

				{
					MsgBuildingBlocked,
					"You will not be able to open a store if you are not allowed to build in this zone!"
				},
				{MsgCombatBlocked, "You will not be able to open a store if you you are raid blocked!"},
				{MsgRaidBlocked, "You will not be able to open a store if you you are combat blocked!"},
				{MsgDeadOrWounded, "You will not be able to open a store if you you are dead or wounded!"},

				// Global
				{MsgTimeDAYS, "d."},
				{MsgTimeHOURS, "h."},
				{MsgTimeMINUTES, "m."},
				{MsgTimeSECONDS, "s."},


				// Chat messages
				{MsgPluginnotinitialized, "The store plugin is not launched correctly"},
				{
					MsgPlayerFloodBlock,
					"You have made too many requests. Relax a little, soon the ability to perform the action will return to you."
				},
				{MsgItemnotfound, "Item not found, try restarting your cart"},
				{MsgUnexpectederror, "Unexpected error from server side, sorry!"},
				{MsgWaitpreviousaction, "Please wait for the <<end>> of the previous step!"},

				// UI
				{MsgBasket, "SERVER BASKET"},
				{MsgExit, MsgExit},
				{MsgBasketDescription, "This is your shopping cart, you can pick them up at any time"},
				{MsgBasketEmpty, "Basket empty"},
				{MsgHelp, "HELP"},
				{MsgRequestProcessing, "Please wait, we are processing your request ..."},
				{MsgBasketUnavailable, "Cart is temporarily unavailable, please try again later"},
				{
					MsgBasketNoAuth, "You are not authorized in the store!\n" +
					                 "You can find a link to authorization in the 'HELP' section"
				},


				{MsgUserManual, "USER'S MANUAL"},
				{
					MsgUserManualDescription, "Here you can pick up the products purchased from our store!\n" +
					                          "You can replenish your account in various ways: e-wallets, cards, etc.\n" +
					                          "Authorization in the store takes place using your STEAM account"
				},

				{
					MsgUserManualBalance,
					"\n<size=18>At the first authorization, you will receive a gift of <b>{0} rubles</b>!</size>"
				},
				{
					MsgTakeItemBlocked, "You cannot pick up this item from your cart!\n" +
					                    "Before its unlocking, there are: {0}"
				},
				{MsgTakeRequestProcessing, "Please wait, your request is being processed!"},
				{MsgTakeLastRequestProcessing, "Please wait, your previous request is still being processed!"},
				{MsgTakeWait, "WAIT"},
				{MsgTakeGiveBlocked, "BLOCKED\n{0}"},
				{MsgTakeGiveError, "ERROR\nRECEIVING"},
				{
					MsgTakeGiveErrorNotify, "An unexpected error occured, please try again later!\n" +
					                        "Your item is safe, don't worry!"
				},
				{MsgTakeGiveSuccess, "SUCCESSFULLY\nRECEIVED"},
				{
					MsgTakeGiveFeet, "You have successfully obtained an item  › <size=20>{0}</size> ‹\n" +
					                 "You don't have enough space in your inventory, <b>the item was thrown</b> at your feet!"
				},
				{MsgTakeGiveInventory, "You have successfully obtained an item  › <size=20>{0}</size> ‹"},
				{
					MsgTakeGiveCommand, "You have successfully obtained an item  › <size=20>{0}</size> ‹\n" +
					                    "New privileges are now available to you!"
				},
				{
					MsgTakeGiveBlueprintFeet,
					"You have successfully obtained the item blueprint  › <size=20>{0}</size> ‹\n" +
					"You don't have enough space in your inventory, <b>the item was thrown</b> at your feet!"
				},
				{
					MsgTakeGiveBlueprintInventory,
					"You have successfully obtained the item blueprint  › <size=20>{0}</size> ‹"
				},
				{
					MsgManyRequests, "You are making too many server requests!\n" +
					                 "Wait <b>one second</b>!"
				}
			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				{
					Maintenance,
					"В данный момент у нас проходят технические работы, попробуйте пожалуйста позже."
				},

				{MsgBuildingBlocked, "Вы не можете открыть магазин, находясь в зоне блокировки строительства!"},
				{MsgCombatBlocked, "Вы не можете открыть магазин, находясь в зоне блокировки боя!"},
				{MsgRaidBlocked, "Вы не можете открыть магазин, находясь в зоне блокировки рейда!"},
				{MsgDeadOrWounded, "Вы не можете получить товар будучи мертвым или нокнутым!"},

				// Global
				{MsgTimeDAYS, "дн."},
				{MsgTimeHOURS, "ч."},
				{MsgTimeMINUTES, "мин."},
				{MsgTimeSECONDS, "сек."},

				// Chat messages
				{MsgPluginnotinitialized, "Плагин магазина запущен некорректно"},
				{
					MsgPlayerFloodBlock,
					"Вы сделали слишком много запросов. Отдохните немного, скоро возможность выполнять действие к вам вернется."
				},
				{MsgItemnotfound, "Предмет не найден, попробуйте перезапустить корзину"},
				{MsgUnexpectederror, "Непредвиденная ошибка со стороны сервера, просим прощения!"},
				{MsgWaitpreviousaction, "Пожалуйста, дождитесь <<окончания>> предыдущего действия!"},

				// UI
				{MsgBasket, "КОРЗИНА СЕРВЕРА"},
				{MsgExit, "ВЫХОД"},
				{MsgBasketDescription, "Это ваша корзина с покупками, вы можете забрать их в любой момент"},
				{MsgBasketEmpty, "Ваша корзина пуста"},
				{MsgHelp, "ПОМОЩЬ"},
				{MsgRequestProcessing, "Подождите, мы обрабатываем ваш запрос..."},
				{MsgBasketUnavailable, "Корзина временно недоступна, попробуйте позже"},
				{
					MsgBasketNoAuth, "Вы не авторизованы в магазине!\n" +
					                 "Ссылку на авторизацию вы можете найти в разделе 'ПОМОЩЬ'"
				},


				{MsgUserManual, "ИНСТРУКЦИЯ ПОЛЬЗОВАТЕЛЯ"},
				{
					MsgUserManualDescription, "Здесь вы можете забрать товары приобретенные у нас в магазине!\n" +
					                          "Пополнить счёт можно различными способами: электронные кошельки, карты и т.д.\n" +
					                          "Авторизация в магазине происходит при помощи вашего аккаунта STEAM"
				},

				{
					MsgUserManualBalance,
					"\n<size=18>При первой авторизации вы получите в подарок <b>{0} рублей</b>!</size>"
				},
				{
					MsgTakeItemBlocked, "Вы не можете забрать этот предмет из корзины!\n" +
					                    "До его разблокировки осталось: {0}"
				},
				{MsgTakeRequestProcessing, "Подождите, ваш запрос обрабатывается!"},
				{MsgTakeLastRequestProcessing, "Подождите, ваш предыдущий запрос ещё обрабатывается!"},
				{MsgTakeWait, "ПОДОЖДИТЕ"},
				{MsgTakeGiveBlocked, "ЗАБЛОКИРОВАНО\n{0}"},
				{MsgTakeGiveError, "ОШИБКА\nПОЛУЧЕНИЯ"},
				{
					MsgTakeGiveErrorNotify, "Произошла непредвиденная ошибка, попробуйте позже!\n" +
					                        "Ваш предмет в безопасности, не переживайте!"
				},
				{MsgTakeGiveSuccess, "УСПЕШНО\nПОЛУЧЕНО"},
				{
					MsgTakeGiveFeet, "Вы успешно получили предмет  › <size=20>{0}</size> ‹\n" +
					                 "У вас недостаточно места в инвентаре, <b>предмет брошен</b> под ноги!"
				},
				{MsgTakeGiveInventory, "Вы успешно получили предмет  › <size=20>{0}</size> ‹"},
				{
					MsgTakeGiveCommand, "Вы успешно получили предмет  › <size=20>{0}</size> ‹\n" +
					                    "Теперь вам доступны новые привилегии!"
				},
				{
					MsgTakeGiveBlueprintFeet, "Вы успешно получили рецепт предмета  › <size=20>{0}</size> ‹\n" +
					                          "У вас недостаточно места в инвентаре, <b>предмет брошен</b> под ноги!"
				},
				{MsgTakeGiveBlueprintInventory, "Вы успешно получили рецепт предмета  › <size=20>{0}</size> ‹"},
				{
					MsgManyRequests, "Вы делаете слишком много запросов к серверу!\n" +
					                 "Подождите <b>одну секунду</b>!"
				}
			}, this, "ru");
		}

		private string Msg(BasePlayer player, string key, params object[] args)
		{
			return string.Format(lang.GetMessage(key, this, player != null ? player.UserIDString : null), args);
		}

		private void Reply(BasePlayer player, string key, params object[] args)
		{
			player.ChatMessage(Msg(player, key, args));
		}

		private string FormatTime(BasePlayer player, double leftTime)
		{
			var span = TimeSpan.FromSeconds(leftTime);

			var text = string.Empty;
			if (span.Days >= 1)
				text += $"{span.Days} " + Msg(player, MsgTimeDAYS) + " ";
			if (span.Hours >= 1)
				text += $"{span.Hours} " + Msg(player, MsgTimeHOURS) + " ";
			if (span.Minutes >= 1)
				text += $"{span.Minutes} " + Msg(player, MsgTimeMINUTES) + " ";
			if (span.Seconds >= 1)
				text += $"{span.Seconds} " + Msg(player, MsgTimeSECONDS);
			
			return text;
		}
		
		#endregion

		#region Additional

		private static readonly Dictionary<int, string> IDToShortName = new()
		{
			[-1461508848] = "rifle.ak", [2115555558] = "ammo.handmade.shell", [-533875561] = "ammo.pistol",
			[1621541165] = "ammo.pistol.fire", [-422893115] = "ammo.pistol.hv", [815896488] = "ammo.rifle",
			[805088543] = "ammo.rifle.explosive", [449771810] = "ammo.rifle.incendiary", [1152393492] = "ammo.rifle.hv",
			[1578894260] = "ammo.rocket.basic", [1436532208] = "ammo.rocket.fire", [542276424] = "ammo.rocket.hv",
			[1594947829] = "ammo.rocket.smoke", [-1035059994] = "ammo.shotgun", [1818890814] = "ammo.shotgun.fire",
			[1819281075] = "ammo.shotgun.slug", [1685058759] = "antiradpills", [93029210] = "apple",
			[-1565095136] = "apple.spoiled", [-1775362679] = "arrow.bone", [-1775249157] = "arrow.fire",
			[-1280058093] = "arrow.hv", [-420273765] = "arrow.wooden", [563023711] = "autoturret",
			[790921853] = "axe.salvaged", [-337261910] = "bandage", [498312426] = "barricade.concrete",
			[504904386] = "barricade.metal", [-1221200300] = "barricade.sandbags", [510887968] = "barricade.stone",
			[-814689390] = "barricade.wood", [1024486167] = "barricade.woodwire", [2021568998] = "battery.small",
			[97329] = "bbq", [1046072789] = "trap.bear", [97409] = "bed", [-1480119738] = "tool.binoculars",
			[1611480185] = "black.raspberries", [-1386464949] = "bleach", [93832698] = "blood",
			[-1063412582] = "blueberries", [-1887162396] = "blueprintbase", [-55660037] = "rifle.bolt",
			[919780768] = "bone.club", [-365801095] = "bone.fragments", [68998734] = "botabag",
			[-853695669] = "bow.hunting", [271534758] = "box.wooden.large", [-770311783] = "box.wooden",
			[-1192532973] = "bucket.water", [-307490664] = "building.planner", [707427396] = "burlap.shirt",
			[707432758] = "burlap.shoes", [-2079677721] = "cactusflesh", [-1342405573] = "tool.camera",
			[-139769801] = "campfire", [-1043746011] = "can.beans", [2080339268] = "can.beans.empty",
			[-171664558] = "can.tuna", [1050986417] = "can.tuna.empty", [-1693683664] = "candycaneclub",
			[523409530] = "candycane", [1300054961] = "cctv.camera", [-2095387015] = "ceilinglight",
			[1428021640] = "chainsaw", [94623429] = "chair", [1436001773] = "charcoal", [1711323399] = "chicken.burned",
			[1734319168] = "chicken.cooked", [-1658459025] = "chicken.raw", [-726947205] = "chicken.spoiled",
			[-341443994] = "chocholate", [1540879296] = "xmasdoorwreath", [94756378] = "cloth", [3059095] = "coal",
			[3059624] = "corn", [2045107609] = "clone.corn", [583366917] = "seed.corn", [2123300234] = "crossbow",
			[1983936587] = "crude.oil", [1257201758] = "cupboard.tool", [-1144743963] = "diving.fins",
			[-1144542967] = "diving.mask", [-1144334585] = "diving.tank", [1066729526] = "diving.wetsuit",
			[-1598790097] = "door.double.hinged.metal", [-933236257] = "door.double.hinged.toptier",
			[-1575287163] = "door.double.hinged.wood", [-2104481870] = "door.hinged.metal",
			[-1571725662] = "door.hinged.toptier", [1456441506] = "door.hinged.wood", [1200628767] = "door.key",
			[-778796102] = "door.closer", [1526866730] = "xmas.door.garland", [1925723260] = "dropbox",
			[1891056868] = "ducttape", [1295154089] = "explosive.satchel", [498591726] = "explosive.timed",
			[1755466030] = "explosives", [726730162] = "facialhair.style01", [-1034048911] = "fat.animal",
			[252529905] = "femalearmpithair.style01", [471582113] = "femaleeyebrow.style01",
			[-1138648591] = "femalepubichair.style01", [305916740] = "female_hairstyle_01",
			[305916742] = "female_hairstyle_03", [305916744] = "female_hairstyle_05", [1908328648] = "fireplace.stone",
			[-2078972355] = "fish.cooked", [-533484654] = "fish.raw", [1571660245] = "fishingrod.handmade",
			[1045869440] = "flamethrower", [1985408483] = "flameturret", [97513422] = "flare",
			[1496470781] = "flashlight.held", [1229879204] = "weapon.mod.flashlight", [-1722829188] = "floor.grill",
			[1849912854] = "floor.ladder.hatch", [-1266285051] = "fridge", [-1749787215] = "boots.frog",
			[28178745] = "lowgradefuel", [-505639592] = "furnace", [1598149413] = "furnace.large",
			[-1779401418] = "gates.external.high.stone", [-57285700] = "gates.external.high.wood", [98228420] = "gears",
			[1422845239] = "geiger.counter", [277631078] = "generator.wind.scrap", [115739308] = "burlap.gloves",
			[-522149009] = "gloweyes", [3175989] = "glue", [718197703] = "granolabar", [384204160] = "grenade.beancan",
			[-1308622549] = "grenade.f1", [-217113639] = "fun.guitar", [-1580059655] = "gunpowder",
			[-1832205789] = "male_hairstyle_01", [305916741] = "female_hairstyle_02",
			[936777834] = "attire.hide.helterneck", [-1224598842] = "hammer", [-1976561211] = "hammer.salvaged",
			[-1406876421] = "hat.beenie", [-1397343301] = "hat.boonie", [1260209393] = "bucket.helmet",
			[-1035315940] = "burlap.headwrap", [-1381682752] = "hat.candle", [696727039] = "hat.cap",
			[-2128719593] = "coffeecan.helmet", [-1178289187] = "deer.skull.mask", [1351172108] = "heavy.plate.helmet",
			[-450738836] = "hat.miner", [-966287254] = "attire.reindeer.headband", [340009023] = "riot.helmet",
			[124310981] = "hat.wolf", [1501403549] = "wood.armor.helmet", [698310895] = "hatchet",
			[523855532] = "hazmatsuit", [2045246801] = "clone.hemp", [583506109] = "seed.hemp",
			[-148163128] = "attire.hide.boots", [-132588262] = "attire.hide.skirt", [-1666761111] = "attire.hide.vest",
			[-465236267] = "weapon.mod.holosight", [-1211618504] = "hoodie", [2133577942] = "hq.metal.ore",
			[-1014825244] = "humanmeat.burned", [-991829475] = "humanmeat.cooked", [-642008142] = "humanmeat.raw",
			[661790782] = "humanmeat.spoiled", [-1440143841] = "icepick.salvaged", [569119686] = "bone.armor.suit",
			[1404466285] = "heavy.plate.jacket", [-1616887133] = "jacket.snow", [-1167640370] = "jacket",
			[-1284735799] = "jackolantern.angry", [-1278649848] = "jackolantern.happy", [776005741] = "knife.bone",
			[108061910] = "ladder.wooden.wall", [255101535] = "trap.landmine", [-51678842] = "lantern",
			[-789202811] = "largemedkit", [516382256] = "weapon.mod.lasersight", [50834473] = "leather",
			[-975723312] = "lock.code", [1908195100] = "lock.key", [-1097452776] = "locker", [146685185] = "longsword",
			[-1716193401] = "rifle.lr300", [193190034] = "lmg.m249", [371156815] = "pistol.m92", [3343606] = "mace",
			[825308669] = "machete", [830965940] = "mailbox", [1662628660] = "male.facialhair.style02",
			[1662628661] = "male.facialhair.style03", [1662628662] = "male.facialhair.style04",
			[-1832205788] = "male_hairstyle_02", [-1832205786] = "male_hairstyle_04",
			[1625090418] = "malearmpithair.style01", [-1269800768] = "maleeyebrow.style01",
			[429648208] = "malepubichair.style01", [-1832205787] = "male_hairstyle_03",
			[-1832205785] = "male_hairstyle_05", [107868] = "map", [997973965] = "mask.balaclava",
			[-46188931] = "mask.bandana", [-46848560] = "metal.facemask", [-2066726403] = "bearmeat.burned",
			[-2043730634] = "bearmeat.cooked", [1325935999] = "bearmeat", [-225234813] = "deermeat.burned",
			[-202239044] = "deermeat.cooked", [-322501005] = "deermeat.raw", [-1851058636] = "horsemeat.burned",
			[-1828062867] = "horsemeat.cooked", [-1966381470] = "horsemeat.raw", [968732481] = "meat.pork.burned",
			[991728250] = "meat.pork.cooked", [-253819519] = "meat.boar", [-1714986849] = "wolfmeat.burned",
			[-1691991080] = "wolfmeat.cooked", [179448791] = "wolfmeat.raw", [431617507] = "wolfmeat.spoiled",
			[688032252] = "metal.fragments", [-1059362949] = "metal.ore", [1265861812] = "metal.plate.torso",
			[374890416] = "metal.refined", [1567404401] = "metalblade", [-1057402571] = "metalpipe",
			[-758925787] = "mining.pumpjack", [-1411620422] = "mining.quarry", [88869913] = "fish.minnows",
			[-2094080303] = "smg.mp5", [843418712] = "mushroom", [-1569356508] = "weapon.mod.muzzleboost",
			[-1569280852] = "weapon.mod.muzzlebrake", [449769971] = "pistol.nailgun",
			[590532217] = "ammo.nailgun.nails", [3387378] = "note", [1767561705] = "burlap.trousers",
			[106433500] = "pants", [-1334615971] = "heavy.plate.pants", [-135651869] = "attire.hide.pants",
			[-1595790889] = "roadsign.kilt", [-459156023] = "pants.shorts", [106434956] = "paper",
			[-578028723] = "pickaxe", [-586116979] = "jar.pickle", [-1379225193] = "pistol.eoka",
			[-930579334] = "pistol.revolver", [548699316] = "pistol.semiauto", [142147109] = "planter.large",
			[148953073] = "planter.small", [102672084] = "attire.hide.poncho", [640562379] = "pookie.bear",
			[-1732316031] = "xmas.present.large", [-2130280721] = "xmas.present.medium",
			[-1725510067] = "xmas.present.small", [1974032895] = "propanetank", [-225085592] = "pumpkin",
			[509654999] = "clone.pumpkin", [466113771] = "seed.pumpkin", [2033918259] = "pistol.python",
			[2069925558] = "target.reactive", [-1026117678] = "box.repair.bench", [1987447227] = "research.table",
			[540154065] = "researchpaper", [1939428458] = "riflebody", [-288010497] = "roadsign.jacket",
			[-847065290] = "roadsigns", [3506021] = "rock", [649603450] = "rocket.launcher", [3506418] = "rope",
			[569935070] = "rug.bear", [113284] = "rug", [1916127949] = "water.salt", [-1775234707] = "salvaged.cleaver",
			[-388967316] = "salvaged.sword", [2007564590] = "santahat", [-1705696613] = "scarecrow",
			[670655301] = "hazmatsuit_scientist", [1148128486] = "hazmatsuit_scientist_peacekeeper",
			[-141135377] = "weapon.mod.small.scope", [109266897] = "scrap", [-527558546] = "searchlight",
			[-1745053053] = "rifle.semiauto", [1223860752] = "semibody", [-419069863] = "sewingkit",
			[-1617374968] = "sheetmetal", [2057749608] = "shelves", [24576628] = "shirt.collared",
			[-1659202509] = "shirt.tanktop", [2107229499] = "shoes.boots", [191795897] = "shotgun.double",
			[-1009492144] = "shotgun.pump", [2077983581] = "shotgun.waterpipe", [378365037] = "guntrap",
			[-529054135] = "shutter.metal.embrasure.a", [-529054134] = "shutter.metal.embrasure.b",
			[486166145] = "shutter.wood.a", [1628490888] = "sign.hanging.banner.large", [1498516223] = "sign.hanging",
			[-632459882] = "sign.hanging.ornate", [-626812403] = "sign.pictureframe.landscape",
			[385802761] = "sign.pictureframe.portrait", [2117976603] = "sign.pictureframe.tall",
			[1338515426] = "sign.pictureframe.xl", [-1455694274] = "sign.pictureframe.xxl",
			[1579245182] = "sign.pole.banner.large", [-587434450] = "sign.post.double",
			[-163742043] = "sign.post.single", [-1224714193] = "sign.post.town", [644359987] = "sign.post.town.roof",
			[-1962514734] = "sign.wooden.huge", [-705305612] = "sign.wooden.large", [-357728804] = "sign.wooden.medium",
			[-698499648] = "sign.wooden.small", [1213686767] = "weapon.mod.silencer",
			[386382445] = "weapon.mod.simplesight", [1859976884] = "skull_fire_pit", [960793436] = "skull.human",
			[1001265731] = "skull.wolf", [1253290621] = "sleepingbag", [470729623] = "small.oil.refinery",
			[1051155022] = "stash.small", [865679437] = "fish.troutsmall", [927253046] = "smallwaterbottle",
			[109552593] = "smg.2", [-2092529553] = "smgbody", [691633666] = "snowball", [-2055888649] = "snowman",
			[621575320] = "shotgun.spas12", [-2118132208] = "spear.stone", [-1127699509] = "spear.wooden",
			[-685265909] = "spikes.floor", [552706886] = "spinner.wheel", [1835797460] = "metalspring",
			[-892259869] = "sticks", [-1623330855] = "stocking.large", [-1616524891] = "stocking.small",
			[789892804] = "stone.pickaxe", [-1289478934] = "stonehatchet", [-892070738] = "stones",
			[-891243783] = "sulfur", [889398893] = "sulfur.ore", [-1625468793] = "supply.signal",
			[1293049486] = "surveycharge", [1369769822] = "fishtrap.small", [586484018] = "syringe.medical",
			[110115790] = "table", [1490499512] = "targeting.computer", [3552619] = "tarp", [1471284746] = "techparts",
			[456448245] = "smg.thompson", [110547964] = "torch", [1588977225] = "xmas.decoration.baubels",
			[918540912] = "xmas.decoration.candycanes", [-471874147] = "xmas.decoration.gingerbreadmen",
			[205978836] = "xmas.decoration.lights", [-1044400758] = "xmas.decoration.pinecone",
			[-2073307447] = "xmas.decoration.star", [435230680] = "xmas.decoration.tinsel", [-864578046] = "tshirt",
			[1660607208] = "tshirt.long", [260214178] = "tunalight", [-1847536522] = "vending.machine",
			[-496055048] = "wall.external.high.stone", [-1792066367] = "wall.external.high",
			[562888306] = "wall.frame.cell.gate", [-427925529] = "wall.frame.cell",
			[995306285] = "wall.frame.fence.gate", [-378017204] = "wall.frame.fence",
			[447918618] = "wall.frame.garagedoor", [313836902] = "wall.frame.netting",
			[1175970190] = "wall.frame.shopfront", [525244071] = "wall.frame.shopfront.metal",
			[-1021702157] = "wall.window.bars.metal", [-402507101] = "wall.window.bars.toptier",
			[-1556671423] = "wall.window.bars.wood", [61936445] = "wall.window.glass.reinforced", [112903447] = "water",
			[1817873886] = "water.catcher.large", [1824679850] = "water.catcher.small", [-1628526499] = "water.barrel",
			[547302405] = "waterjug", [1840561315] = "water.purifier", [-460592212] = "xmas.window.garland",
			[3655341] = "wood", [1554697726] = "wood.armor.jacket", [-1883959124] = "wood.armor.pants",
			[-481416622] = "workbench1", [-481416621] = "workbench2", [-481416620] = "workbench3",
			[-1151126752] = "xmas.lightstring", [-1926458555] = "xmas.tree"
		};

		#region Classes

		private class InfoResult
		{
			public string result;
			public string message = string.Empty;
			public bool maintenance = false;

			public Data data = null;

			public class Data
			{
				public int defaultBalance;
				public string link;
				public List<string> servers;
			}
		}

		private class MaintenanceResult
		{
			public string result;
			public string message = string.Empty;
			public bool maintenance = false;
		}

		private class WItem
		{
			#region Fields

			public bool IsValid;

			public string basketId;
			public string productId;
			public string Name;
			public int ItemID;
			public ItemDefinition itemDefinition;
			public int Amount;
			public List<object> commands = new();
			public string ImageUrl;

			public ulong SteamID;

			public bool IsBlueprint;
			public bool IsCommand;
			public bool IsItem;

			#region Blocks

			public bool IsBlocked;

			public double LeftTime;

			#endregion

			#endregion

			#region Constructors

			public WItem(Dictionary<string, object> data)
			{
				try
				{
					if (data.TryGetValue("basketId", out var idBasket)) basketId = Convert.ToString(idBasket);

					if (data.TryGetValue("productId", out var idProduct)) productId = Convert.ToString(idProduct);

					if (data.TryGetValue("name", out var name)) Name = Convert.ToString(name);

					if (data.TryGetValue("type", out var type))
						switch (Convert.ToString(type))
						{
							case "bp":
								IsBlueprint = true;
								break;
							case "command":
								IsCommand = true;
								break;
							case "item":
								IsItem = true;
								break;
						}

					if (data.TryGetValue("steamId", out var steamId))
						SteamID = Convert.ToUInt64(steamId);

					if (data.TryGetValue("data", out var product))
						if (product is Dictionary<string, object> productData)
						{
							if (IsItem || IsBlueprint)
							{
								ItemID = Convert.ToInt32(productData["itemId"]);

								if (ItemID == 0)
									return;
							}

							if (IsCommand)
							{
								commands = productData["commands"] as List<object>;
								if (commands is {Count: 0})
									return;
							}
						}

					if (data.TryGetValue("img", out var imgUrl)) ImageUrl = Convert.ToString(imgUrl);

					if (data.TryGetValue("amount", out var value)) Amount = Convert.ToInt32(value);

					if (IsItem || IsBlueprint)
					{
						itemDefinition = ItemManager.FindItemDefinition(ItemID);
						if (itemDefinition == null && IDToShortName.TryGetValue(ItemID, out var shortname))
							itemDefinition = ItemManager.FindItemDefinition(shortname);

						if (itemDefinition == null)
							return;
					}

					IsValid = true;

#if !CARBON
					if (Instance?.ImageLibrary?.IsLoaded == true)
#endif
					{
						if (Instance.HasImage($"IconGS.{basketId}"))
						{
							var probablyId = Instance.GetImage($"IconGS.{basketId}");
							if (!string.IsNullOrEmpty(probablyId) && probablyId != Instance.NoImageID &&
							    probablyId != Instance.LoadingImageID)
								ImageUrl = probablyId;
							return;
						}

						if (!string.IsNullOrEmpty(ImageUrl))
							Instance.AddImage(ImageUrl.Replace("https", "http"),
								$"IconGS.{basketId}");
					}
				}
				catch (NullReferenceException e)
				{
					Interface.Oxide.LogError(JsonConvert.SerializeObject(data));
				}
			}

			#endregion
		}

        #endregion

        #endregion

        #region API

        #region Create Payment

        private Regex _checkCreatePaymentMessage = new(@"/^([^'`string.Empty]{1,20})$");

        [ConsoleCommand("gs.createpayment")]
        private void CmdCreatePayment(ConsoleSystem.Arg args)
		{
            if (!Initialized) return;
			if (args.Player() != null) return;

			ulong steamId = args.GetUInt64(0, 0UL);
			int amount = args.GetInt(1, 0);
			string methodName = args.GetString(2, "Custom");

			if (steamId < 70000000000000000UL)
			{
				LogAction(null, "gsCreatePayment | Incorrect steamId", true);
				return;
			}

			if (amount < 1 || amount > 1000000)
			{
				LogAction(null, "gsCreatePayment | Wrong amount", true);
				return;
			}

			if (methodName != "Custom" && _checkCreatePaymentMessage.IsMatch(methodName))
			{
				LogAction(null, "gsCreatePayment | Wrong method name", true);
				return;
			}

			var reqParams = new Dictionary<string, string>
			{
				["store_id"] = _config.API.ShopID,
				["secret_key"] = _config.API.SecretKey,
				["steam_id"] = steamId.ToString(),
				["create_player"] = "true",
				["amount"] = amount.ToString(),
				["method_name"] = methodName
			};

            RequestToPay("integrations/payments/custom", reqParams, (code, response) =>
            {
                switch (code)
                {
                    case 400:
                        HandleCreatePaymentSuccess(steamId.ToString(), response);
                        break;
                    case 200:
                        HandleCreatePaymentSuccess(steamId.ToString(), response);
                        break;
                    default:
                        LogAction(null, $"gsCreatePayment | Error occurred while trying to change player's balance {steamId}. Code: {code}, Server response: {response}");
                        return;
                }
            });
        }

        #endregion

        #region Player Balance

        #region Change Player Balance

        private Regex _checkBalanceMessage = new(@"/^([^'`string.Empty]{1,64})$");

		private const string 
			API_ChangePlayerBalance_Action_Plus = "plus",
			API_ChangePlayerBalance_Action_Minus = "minus";
		
		private void API_ChangePlayerBalance(ulong steamId, int amount, string type, string message = null,
			Action<bool, string> callback = null)
		{
			#region Amount Check

			if (amount is < 1 or > 1000000)
			{
				HandlePlayerBalanceError(callback, steamId, "wrong_amount", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}: количество должно быть в диапазоне от 1 до 1000000"); 
				return;
			}

			#endregion

			#region Type Check

			int typeInt;
			switch (type)
			{
				case API_ChangePlayerBalance_Action_Plus:
					typeInt = 1;
					break;

				case API_ChangePlayerBalance_Action_Minus:
					typeInt = 2;
					break;

				default:
					HandlePlayerBalanceError(callback, steamId, "wrong_type", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}: неверный тип. Возможные значения: plus, minus");
					return;
			}

			#endregion

			#region Message Check

			if (!string.IsNullOrEmpty(message) && _checkBalanceMessage.IsMatch(message))
			{
				HandlePlayerBalanceError(callback, steamId, "wrong_message", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}: сообщение не может содержать специальные символы"); 
				return;
			}

			#endregion

			#region Request Parameters

			var reqParams = new Dictionary<string, string>
			{
				["steamId"]= steamId.ToString(),
				["type"] = typeInt == 1 ? API_ChangePlayerBalance_Action_Plus : API_ChangePlayerBalance_Action_Minus,
				["amount"] = amount.ToString()
			};

			if (!string.IsNullOrEmpty(message)) reqParams.Add("message", message);

			#endregion

			Request("players.item.balance.change", reqParams, (code, response) =>
			{
				switch (code)
				{
					case 400:
					case 200:
						HandlePlayerBalanceChangeSuccess(steamId, response, callback);
						break;

					default:
						HandlePlayerBalanceError(callback, steamId, "request_error", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}. Код: {code}, ответ от сервера: {response}"); 
						return;
				}
			});
		}

		#region Handlers

		private void HandlePlayerBalanceChangeSuccess(ulong steamId, string response, Action<bool, string> callback)
		{
			try
			{
				var json = JObject.Parse(response);
				var result = json["result"].ToString();

				switch (result)
				{
					case "success":
					{
						var balance = json["data"]?["balance"]?.ToString();
						if (balance != null && float.TryParse(balance, out var balanceFloat))
						{
							callback?.Invoke(true, Math.Round(balanceFloat).ToString());

							LogAction(null, $"Баланс игрока {steamId} изменен: {balanceFloat}");
							return;
						}

						HandlePlayerBalanceError(callback, steamId, "parse_error", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}: Ошибка парсинга JSON");
						break;
					}

					case "fail" when json["playerNotFound"]?.ToString().ToLower() == "true":
						HandlePlayerBalanceError(callback, steamId, "player_not_found", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}: Игрок не найден"); 
						break;

					default:
						HandlePlayerBalanceError(callback, steamId, "request_error", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}. Неизвестная ошибка при сериализации ответа: {response}"); 
						break;
				}
			}
			catch (JsonException ex)
			{
				HandlePlayerBalanceError(callback, steamId, "exception", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}: ошибка при сериализации ответа: {ex.Message}");
			}
		}

        private void HandleCreatePaymentSuccess(string steamId, string response)
        {
            try
            {
                var json = JObject.Parse(response);
                var result = json["result"].ToString();

				switch (result)
				{
					case "success":
						{
							
                            var playerBalance = json["data"]?["player_balance"]?.ToString();
							var storeBalance = json["data"]?["store_balance"]?.ToString();
							var paymentId = json["data"]?["payment_id"]?.ToString();                          
							LogAction(null, $"gsCreatePayment | Recharge for player {steamId} created. ID: {paymentId} | Player balance: {playerBalance} | Store balance: {storeBalance}", true);
							
							break;
						}
					case "fail" when json["code"]?.ToString() == "102":
						LogAction(null, $"gsCreatePayment | Error creating recharge for player {steamId}. Insufficient funds on the store balance.", true, true);
						break;
					default:
						LogAction(null, $"gsCreatePayment | Error creating recharge for player {steamId}. Unknown error occurred during response serialization: {response}", true, true);
						break;
				}
			}
            catch (JsonException ex)
            {
                LogAction(null,
                    $"gsCreatePayment | Error creating recharge for player {steamId}. Unknown error occurred during response serialization: {ex.Message}");
            }
        }

        #endregion

        #endregion

        #region Get Player Balance

        private void API_GetPlayerBalance(ulong steamId,
			Action<bool, string> callback = null)
		{
			#region Request Parameters

			var reqParams = new Dictionary<string, string>
			{
				["steamId"] = steamId.ToString()
			};

			#endregion

			Request("players.item.balance", reqParams, (code, response) =>
			{
				switch (code)
				{
					case 400:
					case 200:
						HandlePlayerBalanceGetSuccess(steamId, response, callback);
						break;

					default:
						HandlePlayerBalanceError(callback, steamId, "request_error", $"Ошибка выполнения запроса для изменения баланса игрока {steamId}. Код: {code}, ответ от сервера: {response}");
						return;
				}
			});
		}

		#region Handlers

		private void HandlePlayerBalanceGetSuccess(ulong steamId, string response, Action<bool, string> callback)
		{
			try
			{
				var json = JObject.Parse(response);
				var result = json["result"].ToString();

				switch (result)
				{
					case "success":
					{
						var balance = json["data"]?["balance"]?.ToString();
						if (balance != null && float.TryParse(balance, out var balanceFloat))
						{
							callback?.Invoke(true, Math.Round(balanceFloat).ToString());

							LogAction(null, $"Баланс игрока {steamId} получен: {balanceFloat}");
							return;
						}

						HandlePlayerBalanceError(callback, steamId, "parse_error", $"Ошибка выполнения запроса для получения баланса игрока {steamId}: Ошибка парсинга JSON");
						break;
					}

					case "fail" when json["playerNotFound"]?.ToString().ToLower() == "true":
						HandlePlayerBalanceError(callback, steamId, "player_not_found", $"Ошибка выполнения запроса для получения баланса игрока {steamId}: Игрок не найден");
						break;

					default:
						HandlePlayerBalanceError(callback, steamId, "request_error", $"Ошибка выполнения запроса для получения баланса игрока {steamId}. Неизвестная ошибка при сериализации ответа: {response}");
						break;
				}
			}
			catch (JsonException ex)
			{
				HandlePlayerBalanceError(callback, steamId, "exception", $"Ошибка выполнения запроса для получения баланса игрока {steamId}: ошибка при сериализации ответа: {ex.Message}");
			}
		}

		#endregion

		#endregion

		#region Handlers

		private void HandlePlayerBalanceError(Action<bool, string> callback, ulong steamId, string errorCode,
			string errorMessage)
		{
			callback?.Invoke(false, errorCode);
			LogAction(null, errorMessage);
		}

		#endregion

		#endregion

		#endregion

		#region Queues

		#region Basket

		private class BasketRequest
		{
			public ulong SteamId;

			public BasePlayer Player;

			public int Page;
		}

		private LinkedList<BasketRequest> basketQueue = new();
		private int basketConcurrentRequestCount, basketMaxConcurrentRequests = 10;

		public void RequestBasket(BasePlayer player, int page = 0)
		{
			var basketRequest = new BasketRequest
			{
				SteamId = player.userID,
				Player = player,
				Page = page
			};

			basketQueue.AddLast(basketRequest);

			if (basketConcurrentRequestCount >= basketMaxConcurrentRequests)
			{
				HandleMaxConcurrentRequestsReached(player);
			}
			else
			{
				basketConcurrentRequestCount++;

				StartBasketOpeningProcess(basketRequest);
			}
		}

		private void StartBasketOpeningProcess(BasketRequest basketRequest)
		{
			var playerUserID = basketRequest.SteamId;

			Request("baskets.bySteamId", new Dictionary<string, string>
			{
				{"steamId", playerUserID.ToString()}
			}, (code, response) =>
			{
				DequeueBasketRequest(playerUserID);

				switch (code)
				{
					default:
						HandleBasketUnavailable(basketRequest.Player);
						break;
					case 400:
					case 200:
						HandleBasketResponse(basketRequest.Player, response, basketRequest.Page);
						break;
				}
			}, basketRequest.Player);
		}

		private void TriggerNextBasketRequest()
		{
			if (basketQueue.Count <= 0) return;

			var nextBasketRequest = basketQueue.First?.Value;
			if (nextBasketRequest == null) return;

			StartBasketOpeningProcess(nextBasketRequest);
		}

		private bool CanRequestBasket(BasePlayer player)
		{
			return basketQueue.All(x => x.SteamId != player.userID);
		}

		private bool TryGetRequestBasket(BasePlayer player, out BasketRequest basketRequest)
		{
			foreach (var request in basketQueue)
				if (request.SteamId == player.userID)
				{
					basketRequest = request;
					return true;
				}

			basketRequest = null;
			return false;
		}

		private void DequeueBasketRequest(ulong steamID)
		{
			foreach (var basketRequest in basketQueue)
			{
				if (basketRequest.SteamId != steamID)
					continue;

				basketQueue.Remove(basketRequest);

				basketConcurrentRequestCount--;
				break;
			}

			TriggerNextBasketRequest();
		}

		#endregion

		#region Get Item

		private class GetItemRequest
		{
			public BasePlayer Player;

			public string RequestId;

			public bool Instant;

			public int BasketId;

			public int Index;
		}

		private LinkedList<GetItemRequest> getItemQueue = new();

		private int
			getItemConcurrentRequestCount,
			getItemMaxConcurrentRequests = 10;

		private void RequestGetItem(BasePlayer player, int basketID, int index = 0)
		{
			if (!CanRequestGetItem(basketID)) return;

			var getItemRequest = new GetItemRequest
			{
				Player = player,
				Index = index,
				RequestId = CuiHelper.GetGuid(),
				BasketId = basketID
			};

			getItemQueue.AddLast(getItemRequest);

			if (getItemConcurrentRequestCount >= getItemMaxConcurrentRequests)
			{
				HandleMaxConcurrentRequestsReached(player);
			}
			else
			{
				getItemConcurrentRequestCount++;

				StartGetItemProcess(getItemRequest);
			}
		}

		private void RequestGetItem(int basketID, bool instant = false)
		{
			if (!CanRequestGetItem(basketID))
				return;

			var getItemRequest = new GetItemRequest
			{
				RequestId = CuiHelper.GetGuid(),
				Instant = instant,
				BasketId = basketID
			};

			getItemQueue.AddLast(getItemRequest);

			if (getItemConcurrentRequestCount < getItemMaxConcurrentRequests)
			{
				getItemConcurrentRequestCount++;

				StartGetItemProcess(getItemRequest);
				return;
			}

			HandleMaxConcurrentRequestsReached(basketID);
		}

		private void StartGetItemProcess(GetItemRequest getItemRequest)
		{
			Request("baskets.item", new Dictionary<string, string>
			{
				{"basketId", getItemRequest.BasketId.ToString()}
			}, (code, response) =>
			{
				DequeueGetItemRequest(getItemRequest.RequestId);

				if (getItemRequest.Instant)
					HandleTakeInstantResponse(code, response, getItemRequest.BasketId);
				else
					HandleTakeResponse(getItemRequest.Player, getItemRequest.Index, getItemRequest.BasketId, code,
						response);
			});
		}

		private void TriggerNextGetItemRequest()
		{
			if (getItemQueue.Count <= 0) return;

			var nextGetItemqRequest = getItemQueue.First?.Value;
			if (nextGetItemqRequest == null) return;

			StartGetItemProcess(nextGetItemqRequest);
		}

		private bool CanRequestGetItem(int basketID)
		{
			return getItemQueue.All(x => x.BasketId != basketID);
		}

		private bool TryGetRequestItem(int basketID, out GetItemRequest getItemRequest)
		{
			foreach (var request in getItemQueue)
				if (request.BasketId == basketID)
				{
					getItemRequest = request;
					return true;
				}

			getItemRequest = null;
			return false;
		}

		private void DequeueGetItemRequest(string requestId)
		{
			foreach (var itemRequest in getItemQueue)
			{
				if (itemRequest.RequestId != requestId) continue;

				getItemQueue.Remove(itemRequest);

				getItemConcurrentRequestCount--;
				break;
			}

			TriggerNextGetItemRequest();
		}

		#endregion

		#region Handlers

		private void HandleMaxConcurrentRequestsReached(BasePlayer player)
		{
			ShowNotify(player, Msg(player, MsgManyRequests), 1);

			LogAction(player, "Max concurrent requests limit reached for player");
		}

		private void HandleMaxConcurrentRequestsReached(int basketID)
		{
			LogAction(null, $"Max concurrent requests limit reached for basket {basketID}");
		}

		#endregion

		#endregion

		#region Stable Connection

		private Timer RestartTimer;

		private void SetReserveApiLink()
		{
			if (BestApiLink.Contains(MainApiLink))
			{
				LogAction(null, "Trying use reserve api link", true);
				BestApiLink = CFApiLink;

				if (!Initialized) FetchShopUrl();

				timer.In(5 * 60, TryChangeLink);
			}
			else
			{
				LogAction(null, "Plugin will automatically restart after 10 minutes", true);

				RestartTimer = timer.In(5 * 60, () =>
				{
					LogAction(null, "Restarting...", true);
					Interface.Oxide.ReloadPlugin(Title);
				});
			}
		}

		private void TryChangeLink()
		{
			var newLink = CFApiLink;
			if (BestApiLink.Contains(CFApiLink))
				newLink = MainApiLink;

			CheckRequest(newLink, "store.pluginInfo", (code, response) =>
			{
				switch (code)
				{
					case 200:
					{
						if (response.Length < 1)
							return;

						var firstInfo =
							JsonConvert.DeserializeObject<Dictionary<string, object>>(response,
								new KeyValuesConverter());
						if (firstInfo == null || !firstInfo.ContainsKey("data"))
						{
							initialization = false;
						}
						else
						{
							if (RestartTimer != null)
							{
								LogAction(null, "Restart was canceled");
								RestartTimer.Destroy();
								RestartTimer = null;
							}

							LogAction(null, "API link was changed", true);

							BestApiLink = newLink;

							if (!Initialized) FetchShopUrl();

							if (BestApiLink.Contains(CFApiLink))
								timer.In(10 * 60, TryChangeLink);
						}

						break;
					}
				}
			});
		}

		#endregion

		#region Testing Functions

#if TESTING
		private static void SayDebug(string message)
		{
			Debug.Log($"[GameStores] {message}");
		}
#endif

		#endregion
	}
}