using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Threading;
using static Enums;

namespace test_unity_udp_csharp_server
{
    public class GameServer : INetEventListener
    {
        public int port = 9050;
        public string connectionKey = "testejean";
        public int saveEveryXMilliseconds = 10000;
        public DateTime lastSaveTime = new DateTime();

        public readonly int messageMaxLength = 200;
        public readonly char messageTypeSeparator = '#';
        public readonly char messageValuesSeparator = '!';

        public NetServer server = null;
        public List<Player> playersOnline = new List<Player>();
        public List<GameAccount> accounts = new List<GameAccount>();

        private int _lastLatency = 0;

        public void Start()
        {
            accounts = Repository.LoadGameAccounts();

            server = new NetServer(this, 100, connectionKey);

            //server.SimulateLatency = true;
            //server.SimulatePacketLoss = true;

            server.DisconnectTimeout = 10 * 1000;
            server.Start(port);

            NonBlockingConsole.WriteLine("Server started at {0}", port);
            NonBlockingConsole.WriteLine("Press ESC to stop.");

            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                server.PollEvents();
                Thread.Sleep(15);
            }

            this.SaveGameState();
            server.Stop();
        }

        public void SaveGameState(bool forceSave = false)
        {
            if (lastSaveTime == DateTime.MinValue || lastSaveTime.AddMilliseconds(saveEveryXMilliseconds) < DateTime.UtcNow || forceSave)
            {
                lastSaveTime = DateTime.UtcNow;
                NonBlockingConsole.WriteLine("Saving game state...");
                Repository.SaveGameAccounts(accounts, playersOnline);
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            NonBlockingConsole.WriteLine("[Server] Peer connected: " + peer.EndPoint);
            var peers = server.GetPeers();
            foreach (var netPeer in peers)
            {
                NonBlockingConsole.WriteLine("ConnectedPeersList: id={0}, ep={1}", netPeer.Id, netPeer.EndPoint);
            }
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
        {
            NonBlockingConsole.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + disconnectReason);
        }

        public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            NonBlockingConsole.WriteLine("[Server] error: " + socketErrorCode);
        }

        public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            NonBlockingConsole.WriteLine("[Server] ReceiveUnconnected {0}. From: {1}. Data: {2}", messageType, remoteEndPoint, reader.GetString(100));
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            if (latency != _lastLatency)
            {
                _lastLatency = latency;
                NonBlockingConsole.WriteLine("Latency updated: {0}", latency.ToString());
            }
        }

        public void OnNetworkReceive(NetPeer fromPeer, NetDataReader dataReader)
        {
            string completeMessage = dataReader.GetString(messageMaxLength);
            string[] arrMessageParts = completeMessage.Split(messageTypeSeparator);
            string messageType = arrMessageParts[0];
            string[] arrValues = arrMessageParts[1].Split(messageValuesSeparator);


            if (messageType == ToServerMessageType.LOGIN.ToString("D"))
            {
                OnLogin(fromPeer, arrValues);
            }
            else if (messageType == ToServerMessageType.MOVE.ToString("D"))
            {
                OnUserMove(fromPeer, arrValues);
            }
            else if (messageType == ToServerMessageType.END_MOVE.ToString("D"))
            {
                OnUserEndMove(fromPeer, arrValues);
            }
            else if (messageType == ToServerMessageType.GET_OTHER_PLAYERS.ToString("D"))
            {
                OnGetOtherPlayers(fromPeer, arrValues);
            }
            else if (messageType == ToServerMessageType.CREATE_ACCOUNT.ToString("D"))
            {
                OnCreateAccount(fromPeer, arrValues);
            }
            else
            {
                NonBlockingConsole.WriteLine("[WARNING] Received a message with unknown type: " + completeMessage);
            }

            this.SaveGameState();
        }

        public void OnCreateAccount(NetPeer fromPeer, string[] values)
        {
            string username = values[0];
            string password = values[1];
            string playerName = values[2];
            Enums.CharacterClass characterClass = Converters.StringToCharacterClass(values[3]);

            GameAccount oldAccount = accounts.Where(a => a.username == username).FirstOrDefault();
            if (oldAccount == null)
            {
                oldAccount = accounts.Where(a => a.player.name == playerName).FirstOrDefault();
                if (oldAccount == null)
                {
                    GameAccount newAccount = new GameAccount()
                    {
                        username = username,
                        password = password,
                        player = new Player(playerName, characterClass)
                    };

                    accounts.Add(newAccount);

                    OnLoginSuccess(fromPeer, newAccount);

                    SaveGameState(true);
                }
                else
                {
                    NonBlockingConsole.WriteLine("player name already used: [" + playerName + "]");

                    string message = FormatMessageContent(FromServerMessageType.CREATE_ACCOUNT_FAIL, "player name already used");
                    this.SendMessage(fromPeer, message);
                }
            }
            else
            {
                NonBlockingConsole.WriteLine("username already used: [" + username + "]");

                string message = FormatMessageContent(FromServerMessageType.CREATE_ACCOUNT_FAIL, "username already used");
                this.SendMessage(fromPeer, message);
            }

        }

        public void OnGetOtherPlayers(NetPeer fromPeer, string[] values)
        {
            string id = values[0];
            Player player = playersOnline.Where(p => p.id == id).FirstOrDefault();
            if (player != null)
            {
                SendConnectedUsersTo(player);
            }
        }

        public void OnLogin(NetPeer fromPeer, string[] values)
        {
            string username = values[0];
            string password = values[1];

            GameAccount accountLogged = accounts.Where(a => a.password == password && a.username == username).FirstOrDefault();
            if (accountLogged == null)
            {
                NonBlockingConsole.WriteLine("login failed for username [" + username + "]");

                string message = FormatMessageContent(FromServerMessageType.LOGIN_FAIL, "wrong credentials");
                this.SendMessage(fromPeer, message);
            }
            else
            {
                OnLoginSuccess(fromPeer, accountLogged);
            }
        }

        private void OnLoginSuccess(NetPeer fromPeer, GameAccount accountLogged)
        {
            NonBlockingConsole.WriteLine("login success for username [" + accountLogged.username + "]");

            //send login success message
            string loginSuccessMessage = FormatMessageContent(FromServerMessageType.LOGIN_SUCCESS,
                accountLogged.player.id,
                accountLogged.player.name,
                Converters.FloatToString(accountLogged.player.x),
                Converters.FloatToString(accountLogged.player.y),
                Converters.CharacterClassToString(accountLogged.player.characterClass),
                Converters.DateTimeToString(accountLogged.lastLogin)
            );

            this.SendMessage(fromPeer, loginSuccessMessage);

            //update account info
            accountLogged.lastLogin = DateTime.UtcNow;
            accountLogged.player.peer = fromPeer;

            //send other connected users
            //this.SendConnectedUsersTo(accountLogged.player);

            //add player to the list of online players
            playersOnline.Add(accountLogged.player);

            //send message to the other players about this one
            string userConnectedMessage = FormatMessageContent(FromServerMessageType.USER_CONNECTED,
                accountLogged.player.id,
                accountLogged.player.name,
                Converters.FloatToString(accountLogged.player.x),
                Converters.FloatToString(accountLogged.player.y),
                Converters.CharacterClassToString(accountLogged.player.characterClass)
            );

            this.SendMessageToEveryoneButMe(accountLogged.player, userConnectedMessage);
        }

        private void SendConnectedUsersTo(Player from)
        {
            List<Player> otherPlayers = playersOnline.Where(p => p.id != from.id).ToList();
            foreach (Player player in otherPlayers)
            {
                string userConnectedMessage = FormatMessageContent(FromServerMessageType.USER_CONNECTED,
                    player.id,
                    player.name,
                    Converters.FloatToString(player.x),
                    Converters.FloatToString(player.y),
                    Converters.CharacterClassToString(player.characterClass)
                );
                this.SendMessage(from.peer, userConnectedMessage);
            }
        }

        public void OnUserMove(NetPeer fromPeer, string[] values)
        {
            string id = values[0];
            int moveX = Converters.StringToInt(values[1]);
            int moveY = Converters.StringToInt(values[2]);

            NonBlockingConsole.WriteLine(id + " Moving [x: " + moveX + ", y: " + moveY + "]");

            Player me = playersOnline.Where(p => p.id == id).FirstOrDefault();
            if (me != null)
            {
                string startMovementMessage = FormatMessageContent(FromServerMessageType.MOVE, values);
                this.SendMessageToEveryoneButMe(me, startMovementMessage);
            }
        }

        public void OnUserEndMove(NetPeer fromPeer, string[] values)
        {
            string id = values[0];
            float posX = Converters.StringToFloat(values[1]);
            float posY = Converters.StringToFloat(values[2]);

            NonBlockingConsole.WriteLine(id + " Stopped at [x: " + posX + ", y: " + posY + "]");

            Player player = playersOnline.Where(p => p.id == id).FirstOrDefault();
            if (player != null)
            {
                player.x = posX;
                player.y = posY;

                string endMovementMessage = FormatMessageContent(FromServerMessageType.END_MOVE, values);
                this.SendMessageToEveryoneButMe(player, endMovementMessage);
            }
        }

        public void SendMessage(NetPeer peer, string message)
        {
            string completeMessage = message;
            NetDataWriter writer = new NetDataWriter();
            writer.Put(completeMessage);
            peer.Send(writer, SendOptions.ReliableOrdered);
        }

        public void SendMessageToEveryoneButMe(Player me, string message)
        {
            NetDataWriter writer = new NetDataWriter();
            writer.Put(message);

            List<Player> playersToSend = playersOnline.Where(c => c.id != me.id).ToList<Player>();
            foreach (Player client in playersToSend)
            {
                client.peer.Send(writer, SendOptions.ReliableOrdered);
            }
        }

        public string FormatMessageContent(FromServerMessageType type, params string[] args)
        {
            string message = type.ToString("D") + messageTypeSeparator;
            int argsCount = args.Count();

            if (argsCount == 1)
            {
                message = message + args[0];
            }
            else if (argsCount > 1)
            {
                message = message + String.Join(messageValuesSeparator.ToString(), args);
            }

            return message;
        }
    }
}
