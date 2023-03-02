﻿using System;
using System.Threading; 
using System.Drawing;
using System.Windows.Forms;
using Websocket.Client;
using jbcarreon123.WebNowPlayingPlugin.Actions;
using SuchByte.MacroDeck.Plugins;
using SuchByte.MacroDeck.Variables;
using Fleck;
using System.Collections.Generic;
using SuchByte.MacroDeck.Logging;
using WNPReduxAdapterLibrary;
using SuchByte.MacroDeck.GUI;
using SuchByte.MacroDeck.GUI.CustomControls;
using System.Net.WebSockets;
using System.Reflection;
using System.ComponentModel;

namespace jbcarreon123.WebNowPlayingPlugin
{

    public static class PluginInstance
    {
        public static Main Main { get; set; }
    }

    public class Main : MacroDeckPlugin
    {
        public static Main Instance;
        public static int wsclientcount {
            get { return _wscc; }
            set
            {
                _wscc = value;
                new StatusIcon();
            }
        }
        public static int _wscc = 0;
        public override bool CanConfigure => true;
        public static IWebSocketConnection socket { get; internal set; }

        public Main()
        {
            Instance = this;
            PluginInstance.Main = this;
        }

        public override void OpenConfigurator()
        {
            Config.Config config = new Config.Config();
            config.ShowDialog();
        }

        public override void Enable()
        {
            new StatusIcon();
            Instance ??= this;
            try {
                OpenWS();
                this.Actions = new List<PluginAction>
            {
                new PlayPauseAction(),
                new PreviousAction(),
                new NextAction(),
                new ShuffleAction(),
                new RepeatAction()
            };
            } catch (Exception e) {
                MacroDeckLogger.Error(this, $"There is a error.\r\n{e}");
            }
            // MacroDeckLogger.Info(this, $"Finished loading WebNowPlaying Plugin ({stp.ElapsedMilliseconds}ms)");
        }

        public bool StringToBoolean(string value) {
            if (value == "true" || Convert.ToInt32(value) >= 0) {
                return true;
            } else {
                return false;
            }
        }

        public bool IsClientConnected() {
            return (wsclientcount > 0);
        }

        public void OpenWS()
        {
            if (PluginConfiguration.GetValue(PluginInstance.Main, "mode") == "Normal")
            {
                try
                {
                    var server = new WebSocketServer($"ws://0.0.0.0:{PluginConfiguration.GetValue(this, "port")}");
                    server.Start(socket =>
                    {
                        Main.socket = socket;
                        socket.OnMessage = message =>
                        {
                            if (!(message.IndexOf("error", StringComparison.CurrentCultureIgnoreCase) >= 0))
                            {
                                if (message.IndexOf("title", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_title", message.Replace("TITLE:", ""), VariableType.String, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("artist", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_artist", message.Replace("ARTIST:", ""), VariableType.String, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("album", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_album", message.Replace("ALBUM:", ""), VariableType.String, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("player", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_player", message.Replace("PLAYER:", ""), VariableType.String, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("volume", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_volume", message.Replace("VOLUME:", ""), VariableType.Float, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("state", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_is_playing", (message.Replace("STATE:", "") == "1"), VariableType.Bool, PluginInstance.Main, null);
                                    VariableManager.SetValue("wnp_state", message.Replace("STATE:", ""), VariableType.Integer, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("repeat", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    var repeat = message.Replace("REPEAT:", "");
                                    VariableManager.SetValue("wnp_repeat", (repeat == "2" || repeat == "1"), VariableType.Bool, PluginInstance.Main, null);
                                    VariableManager.SetValue("wnp_repeatall", (repeat == "2"), VariableType.Bool, PluginInstance.Main, null);
                                    VariableManager.SetValue("wnp_repeatone", (repeat == "1"), VariableType.Bool, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("shuffle", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_shuffle", StringToBoolean(message.Replace("SHUFFLE:", "")), VariableType.Bool, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("position", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_position", message.Replace("POSITION:", ""), VariableType.String, PluginInstance.Main, null);
                                }
                                else if (message.IndexOf("duration", StringComparison.CurrentCultureIgnoreCase) >= 0)
                                {
                                    VariableManager.SetValue("wnp_duration", message.Replace("DURATION:", ""), VariableType.String, PluginInstance.Main, null);
                                }
                            }
                        /*
                        if (Convert.ToBoolean(PluginConfiguration.GetValue(this, "passThrough")))
                        {
                            //SendMsg(message);
                        }
                        */
                        };
                        socket.OnOpen = () =>
                        {
                            socket.Send("Version:0.5.0.0");
                            MacroDeckLogger.Trace(PluginInstance.Main, "A websocket client was connected.");
                            wsclientcount++;
                        };
                        socket.OnClose = () =>
                        {
                            MacroDeckLogger.Trace(PluginInstance.Main, "A websocket client was disconnected.");
                            wsclientcount--;
                        };
                    }
                    );
                }
                catch (Exception e)
                {
                    throw new ConnectionNotAvailableException(e.Message);
                }
            }
            else
            {
                WNPRedux.Initialize(Convert.ToInt32(PluginConfiguration.GetValue(PluginInstance.Main, "port")), "2.0.0", Logger);

                var worker = new BackgroundWorker();
                worker.DoWork += new DoWorkEventHandler(worker_DoWork);
                worker.RunWorkerAsync();
            }
        }

        void worker_DoWork(object sender, DoWorkEventArgs e) {
            while (true) {
                var mediainfo = WNPRedux.mediaInfo;

                VariableManager.SetValue("wnp_title", mediainfo.Title, VariableType.String, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_album", mediainfo.Album, VariableType.String, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_artist", mediainfo.Artist, VariableType.String, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_position", mediainfo.Position, VariableType.String, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_pos_percent", mediainfo.PositionPercent, VariableType.Float, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_duration", mediainfo.Duration, VariableType.String, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_player", mediainfo.Player, VariableType.String, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_state", mediainfo.State, VariableType.Integer, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_volume", mediainfo.Volume, VariableType.Integer, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_shuffle", mediainfo.Shuffle, VariableType.Bool, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_repeatone", (mediainfo.RepeatState == WNPRedux.MediaInfo.RepeatMode.ONE), VariableType.Bool, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_repeatall", (mediainfo.RepeatState == WNPRedux.MediaInfo.RepeatMode.ALL), VariableType.Bool, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_is_playing", (mediainfo.State == WNPRedux.MediaInfo.StateMode.PLAYING), VariableType.Bool, PluginInstance.Main, null);
                VariableManager.SetValue("wnp_repeat", (mediainfo.RepeatState == WNPRedux.MediaInfo.RepeatMode.ALL || mediainfo.RepeatState == WNPRedux.MediaInfo.RepeatMode.ONE), VariableType.Bool, PluginInstance.Main, null);

                Thread.Sleep(100);
            }
        }

        public void Logger(WNPRedux.LogType type, string message)
        {
            if (type == WNPRedux.LogType.DEBUG)
                MacroDeckLogger.Info(PluginInstance.Main, message);
            else if (type == WNPRedux.LogType.WARNING)
                MacroDeckLogger.Warning(PluginInstance.Main, message);
            else
                MacroDeckLogger.Error(PluginInstance.Main, message);
        }
    }
}
